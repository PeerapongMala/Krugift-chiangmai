using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Api.Import;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

/// <summary>
/// นำเข้า "ไฟล์ครู" — ไฟล์เดียวหลายชีท ชีทละห้อง ในชีทมีทั้งรายชื่อและคะแนนทุกรายการ
/// preview: ตรวจทุกชีท ไม่เขียนอะไร · commit: ตรวจซ้ำเฉพาะชีทที่ครูเลือก ผิดจุดเดียวไม่บันทึกอะไรเลย
/// สร้างห้องเรียน นักเรียน รายการคะแนน และคะแนน ให้ครบในครั้งเดียว (ห้องชื่อเดิมถือว่าเป็นห้องเดียวกัน)
/// </summary>
public static class BookImportEndpoints
{
    const long MultipartOverhead = 64 * 1024;
    const string ChooseFile = "กรุณาเลือกไฟล์ Excel (.xlsx)";

    static readonly RequestSizeLimitAttribute UploadLimit = new(Limits.ImportMaxBytes + MultipartOverhead);

    public static void MapBookImport(this WebApplication app)
    {
        var g = app.MapGroup("/api/terms/{termId:int}/import/book").RequireAuthorization(AuthSetup.Teacher);

        g.MapPost("/preview", async (int termId, HttpRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (!await db.TermsOf(user).AnyAsync(t => t.Id == termId)) return Problems.NotFound("ภาคเรียนนี้");

            var (book, problem) = await ReadUpload(request);
            if (problem is not null) return problem;

            var parsed = ParseAll(book!, sheets: null);
            return Results.Ok(new
            {
                parsed.Sheets,
                parsed.ErrorCount,
                Errors = parsed.Errors.Take(Limits.ImportMaxErrors),
            });
        }).WithMetadata(UploadLimit);

        g.MapPost("/commit", async (int termId, HttpRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (!await db.TermsOf(user).AnyAsync(t => t.Id == termId)) return Problems.NotFound("ภาคเรียนนี้");

            var (book, problem) = await ReadUpload(request);
            if (problem is not null) return problem;

            var chosen = request.Form["sheets"].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (chosen.Count == 0) return Problems.Invalid("กรุณาเลือกชีทที่จะนำเข้าอย่างน้อย 1 ชีท");

            var missing = chosen.Where(name => book!.All(s => s.Name != name)).ToList();
            if (missing.Count > 0) return Problems.Invalid($"ไม่พบชีท {string.Join(", ", missing)} ในไฟล์นี้");

            var parsed = ParseAll(book!, chosen);
            if (parsed.ErrorCount > 0)
                return Problems.Invalid($"ไฟล์ยังมีจุดที่ต้องแก้ {parsed.ErrorCount} จุด ไม่ได้บันทึกอะไรเลย · กรุณากดตรวจไฟล์อีกครั้ง");

            try
            {
                await Commit(db, user, termId, parsed.Plans);
            }
            catch (DbUpdateException)
            {
                return Problems.Conflict("ข้อมูลถูกแก้พร้อมกัน ไม่ได้บันทึกอะไรเลย · กรุณากดตรวจไฟล์อีกครั้ง");
            }

            return Results.Ok(new { Summary = new BookPlan(parsed.Plans).Summary });
        }).WithMetadata(UploadLimit);
    }

    record SheetReport(string Name, string Classroom, int Students, int Items, int Scores, bool IsRoomSheet, int ErrorCount);

    record ParsedBook(List<SheetReport> Sheets, List<ImportError> Errors, int ErrorCount, List<BookSheetPlan> Plans);

    /// ตรวจทุกชีท (หรือเฉพาะชีทที่เลือก) · ชีทที่ไม่ใช่ตารางห้องเรียน เช่น ชีทสรุป ข้ามไปเฉย ๆ ไม่ใช่จุดผิด
    static ParsedBook ParseAll(IReadOnlyList<BookSheet> book, IReadOnlyList<string>? sheets)
    {
        var reports = new List<SheetReport>();
        var allErrors = new List<ImportError>();
        var plans = new List<BookSheetPlan>();

        foreach (var sheet in book)
        {
            if (sheets is not null && !sheets.Contains(sheet.Name)) continue;

            if (!TeacherBookParser.IsRoomSheet(sheet))
            {
                reports.Add(new SheetReport(sheet.Name, "", 0, 0, 0, IsRoomSheet: false, ErrorCount: 0));
                continue;
            }

            var errors = new ImportErrors();
            var plan = TeacherBookParser.Parse(sheet, errors);
            var result = errors.Result<BookPlan>(null);

            // ใส่ชื่อชีทไว้หน้าข้อความ ครูจะได้รู้ว่าจุดผิดอยู่ชีทไหน (ตารางจุดผิดบอกแค่แถวกับคอลัมน์)
            allErrors.AddRange(result.Errors.Select(e => e with { Message = $"ชีท {sheet.Name}: {e.Message}" }));

            reports.Add(new SheetReport(sheet.Name, plan?.ClassroomName ?? "", plan?.Students.Count ?? 0,
                plan?.Items.Count ?? 0, plan?.Scores.Count ?? 0, IsRoomSheet: true, result.ErrorCount));

            if (result.ErrorCount == 0 && plan is not null) plans.Add(plan);
        }

        return new ParsedBook(reports, allErrors, reports.Sum(r => r.ErrorCount), plans);
    }

    static async Task<(IReadOnlyList<BookSheet>? Book, IResult? Problem)> ReadUpload(HttpRequest request)
    {
        if (!request.HasFormContentType) return (null, Problems.Invalid(ChooseFile));

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync();
        }
        catch (Exception)
        {
            return (null, Results.Problem(SheetReader.TooLarge, statusCode: StatusCodes.Status413PayloadTooLarge));
        }

        if (form.Files.Count != 1) return (null, Problems.Invalid(ChooseFile));
        var file = form.Files[0];
        if (file.Length == 0) return (null, Problems.Invalid("ไฟล์ว่างเปล่า"));
        if (file.Length > Limits.ImportMaxBytes)
            return (null, Results.Problem(SheetReader.TooLarge, statusCode: StatusCodes.Status413PayloadTooLarge));
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return (null, Problems.Invalid(SheetReader.NotXlsx));

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory);

        var (book, error) = BookReader.Read(memory.ToArray());
        return error is not null ? (null, Problems.Invalid(error)) : (book, null);
    }

    /// เขียนทุกชีทใน transaction เดียว · ห้อง/นักเรียน/รายการที่มีอยู่แล้วใช้ของเดิม ไม่สร้างซ้ำ
    static async Task Commit(AppDbContext db, ClaimsPrincipal user, int termId, List<BookSheetPlan> plans)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var teacherId = user.UserId();
        var now = DateTime.UtcNow;

        foreach (var plan in plans)
        {
            var classroom = await db.Classrooms.FirstOrDefaultAsync(c => c.TermId == termId && c.Name == plan.ClassroomName);
            if (classroom is null)
            {
                classroom = new Classroom { TermId = termId, Name = plan.ClassroomName };
                db.Classrooms.Add(classroom);
                await db.SaveChangesAsync();
            }

            var students = await UpsertStudents(db, classroom.Id, plan);
            var items = await UpsertItems(db, classroom.Id, plan);
            await UpsertScores(db, plan, students, items, teacherId, now);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// ไฟล์ครูเป็นตัวตั้ง: ชื่อในไฟล์ทับของเดิม (ครูแก้ในไฟล์แล้วนำเข้าใหม่ได้เลย)
    static async Task<Dictionary<string, int>> UpsertStudents(AppDbContext db, int classroomId, BookSheetPlan plan)
    {
        var codes = plan.Students.Select(s => s.Code).ToList();
        var existing = await db.Students.Where(s => codes.Contains(s.StudentCode)).ToDictionaryAsync(s => s.StudentCode);
        var enrollments = await db.Enrollments.Where(e => e.ClassroomId == classroomId).ToDictionaryAsync(e => e.StudentId);

        foreach (var row in plan.Students)
        {
            if (!existing.TryGetValue(row.Code, out var student))
            {
                student = new Student { StudentCode = row.Code, FirstName = row.FirstName, LastName = row.LastName };
                db.Students.Add(student);
                existing[row.Code] = student;
            }
            student.Title = row.Title;
            student.FirstName = row.FirstName;
            student.LastName = row.LastName;
            student.Nickname = row.Nickname;
        }
        await db.SaveChangesAsync();

        foreach (var row in plan.Students)
        {
            var studentId = existing[row.Code].Id;
            if (enrollments.TryGetValue(studentId, out var enrollment)) enrollment.No = row.No;
            else db.Enrollments.Add(new Enrollment { ClassroomId = classroomId, StudentId = studentId, No = row.No });
        }
        await db.SaveChangesAsync();

        return plan.Students.ToDictionary(s => s.Code, s => existing[s.Code].Id);
    }

    static async Task<List<AssessmentItem>> UpsertItems(AppDbContext db, int classroomId, BookSheetPlan plan)
    {
        var existing = await db.Items.Where(i => i.ClassroomId == classroomId).ToListAsync();
        var order = existing.Count == 0 ? 0 : existing.Max(i => i.SortOrder);
        var result = new List<AssessmentItem>(plan.Items.Count);

        foreach (var row in plan.Items)
        {
            var item = existing.FirstOrDefault(i => i.Name == row.Name);
            if (item is null)
            {
                item = new AssessmentItem { ClassroomId = classroomId, Name = row.Name, MaxScore = row.MaxScore, SortOrder = ++order };
                db.Items.Add(item);
                existing.Add(item);
            }
            else
            {
                item.MaxScore = row.MaxScore;
            }
            result.Add(item);
        }

        await db.SaveChangesAsync();
        return result;
    }

    static async Task UpsertScores(AppDbContext db, BookSheetPlan plan, Dictionary<string, int> students,
        List<AssessmentItem> items, int teacherId, DateTime now)
    {
        var itemIds = items.Select(i => i.Id).ToList();
        var existing = await db.Scores.Where(s => itemIds.Contains(s.ItemId)).ToDictionaryAsync(s => (s.ItemId, s.StudentId));

        foreach (var row in plan.Scores)
        {
            var itemId = items[row.ItemIndex].Id;
            var studentId = students[plan.Students[row.Row].Code];

            if (!existing.TryGetValue((itemId, studentId), out var score))
            {
                score = new Score { ItemId = itemId, StudentId = studentId };
                db.Scores.Add(score);
                existing[(itemId, studentId)] = score;
            }
            ScoreWriter.Set(db, score, row.Value, teacherId, now);
        }
    }
}
