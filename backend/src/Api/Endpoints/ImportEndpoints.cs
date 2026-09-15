using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Api.Import;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

/// <summary>
/// นำเข้า Excel แบบ all-or-nothing แยก 2 แบบ: รายชื่อนักเรียน กับ คะแนน
/// - template: ไฟล์ของห้องนี้พร้อมข้อมูลปัจจุบัน
/// - preview: ตรวจทั้งไฟล์ ไม่เขียนอะไร
/// - commit: ตรวจซ้ำกับข้อมูลล่าสุด ผิดแม้จุดเดียวไม่บันทึกอะไรเลย ผ่านหมดเขียนใน transaction เดียว
/// ครูส่งไฟล์เดิมมาอีกรอบตอน commit แทนการเก็บผล preview ไว้ที่ server ที่อาจเก่าไปแล้ว
/// </summary>
public static class ImportEndpoints
{
    /// เผื่อ header ของ multipart นอกจากตัวไฟล์
    const long MultipartOverhead = 64 * 1024;

    /// รายการเปลี่ยนแปลงที่ส่งไปแสดงในหน้า preview สูงสุด
    const int PreviewMaxChanges = 500;

    const string ChooseFile = "กรุณาเลือกไฟล์ Excel (.xlsx)";

    const string UnsafeFileNameChars = "\\/:*?\"<>|";

    static readonly RequestSizeLimitAttribute UploadLimit = new(Limits.ImportMaxBytes + MultipartOverhead);

    static IResult TooLarge => Results.Problem(SheetReader.TooLarge, statusCode: StatusCodes.Status413PayloadTooLarge);

    static IResult ChangedMeanwhile => Problems.Conflict(
        "ข้อมูลของห้องนี้ถูกแก้พร้อมกัน ไม่ได้บันทึกอะไรเลย · กรุณากดตรวจไฟล์อีกครั้ง");

    public static void MapImport(this WebApplication app)
    {
        var g = app.MapGroup("/api/classrooms/{id:int}/import").RequireAuthorization(AuthSetup.Teacher);

        // ---------------------------------------------------------------- รายชื่อนักเรียน
        g.MapGet("/students/template", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var classroom = await db.FindClassroom(user, id);
            if (classroom is null) return Problems.NotFound("ห้องเรียนนี้");

            var bytes = ImportTemplates.Students(await EnrollmentsOf(db, user, id));
            return Results.File(bytes, ImportTemplates.ContentType, FileName("รายชื่อนักเรียน", classroom.Name));
        });

        g.MapPost("/students/preview", async (int id, HttpRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindClassroom(user, id) is null) return Problems.NotFound("ห้องเรียนนี้");
            var (result, problem) = await AnalyzeStudents(request, db, user, id);
            return problem ?? Results.Ok(Preview(result!));
        }).WithMetadata(UploadLimit);

        g.MapPost("/students/commit", async (int id, HttpRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindClassroom(user, id) is null) return Problems.NotFound("ห้องเรียนนี้");
            var (result, problem) = await AnalyzeStudents(request, db, user, id);
            if (problem is not null) return problem;
            if (!result!.IsValid) return StillInvalid(result.ErrorCount);

            try
            {
                await CommitStudents(db, id, result.Plan!);
            }
            catch (DbUpdateException)
            {
                return ChangedMeanwhile;
            }
            return Results.Ok(new { result.Plan!.Summary });
        }).WithMetadata(UploadLimit);

        // ---------------------------------------------------------------- คะแนน
        g.MapGet("/scores/template", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var classroom = await db.FindClassroom(user, id);
            if (classroom is null) return Problems.NotFound("ห้องเรียนนี้");

            var snapshot = await ScoreSnapshotOf(db, user, id);
            var bytes = ImportTemplates.Scores(snapshot.Items, snapshot.Enrollments, snapshot.Scores);
            return Results.File(bytes, ImportTemplates.ContentType, FileName("คะแนน", classroom.Name));
        });

        g.MapPost("/scores/preview", async (int id, HttpRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindClassroom(user, id) is null) return Problems.NotFound("ห้องเรียนนี้");
            var (result, problem) = await AnalyzeScores(request, db, user, id);
            return problem ?? Results.Ok(Preview(result!));
        }).WithMetadata(UploadLimit);

        g.MapPost("/scores/commit", async (int id, HttpRequest request, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindClassroom(user, id) is null) return Problems.NotFound("ห้องเรียนนี้");
            var (result, problem) = await AnalyzeScores(request, db, user, id);
            if (problem is not null) return problem;
            if (!result!.IsValid) return StillInvalid(result.ErrorCount);

            try
            {
                await CommitScores(db, user, id, result.Plan!);
            }
            catch (DbUpdateException)
            {
                return ChangedMeanwhile;
            }
            return Results.Ok(new { result.Plan!.Summary });
        }).WithMetadata(UploadLimit);
    }

    static async Task<(ImportResult<StudentPlan>? Result, IResult? Problem)> AnalyzeStudents(
        HttpRequest request, AppDbContext db, ClaimsPrincipal user, int classroomId)
    {
        var (sheet, problem) = await ReadUpload(request);
        if (problem is not null) return (null, problem);

        var codes = StudentSheetParser.StudentCodes(sheet!);
        var enrollments = await EnrollmentsOf(db, user, classroomId);
        // รหัส unique ทั้งโรงเรียน ต้องหาจากทุกห้อง ไม่ใช่แค่ห้องนี้ ไม่งั้นจะสร้างเด็กคนเดิมซ้ำ
        var students = codes.Length == 0
            ? []
            : await db.Students
                .Where(s => codes.Contains(s.StudentCode))
                .Select(s => new ExistingStudent(s.Id, s.StudentCode, s.FirstName, s.LastName))
                .ToListAsync();

        var snapshot = new StudentSnapshot(enrollments, students.ToDictionary(s => s.Code, StringComparer.Ordinal));
        return (StudentSheetParser.Parse(sheet!, snapshot), null);
    }

    static async Task<(ImportResult<ScorePlan>? Result, IResult? Problem)> AnalyzeScores(
        HttpRequest request, AppDbContext db, ClaimsPrincipal user, int classroomId)
    {
        var (sheet, problem) = await ReadUpload(request);
        if (problem is not null) return (null, problem);
        return (ScoreSheetParser.Parse(sheet!, await ScoreSnapshotOf(db, user, classroomId)), null);
    }

    /// ด่านของการอัปโหลด: ต้องเป็น multipart, ไฟล์เดียว, .xlsx, ไม่เกินขนาด แล้วส่งต่อให้ SheetReader
    static async Task<(Sheet? Sheet, IResult? Problem)> ReadUpload(HttpRequest request)
    {
        if (!request.HasFormContentType) return (null, Problems.Invalid(ChooseFile));

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync();
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, TooLarge);
        }
        catch (InvalidDataException)
        {
            return (null, Problems.Invalid("อ่านไฟล์ที่อัปโหลดไม่ได้ ลองเลือกไฟล์ใหม่อีกครั้ง"));
        }

        if (form.Files.Count == 0) return (null, Problems.Invalid(ChooseFile));
        if (form.Files.Count > 1) return (null, Problems.Invalid("อัปโหลดได้ครั้งละ 1 ไฟล์"));

        var file = form.Files[0];
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return (null, Problems.Invalid(SheetReader.NotXlsx));
        if (file.Length > Limits.ImportMaxBytes) return (null, TooLarge);

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var (sheet, error) = SheetReader.Read(buffer.ToArray());
        return error is null ? (sheet, null) : (null, Problems.Invalid(error));
    }

    static IResult StillInvalid(int errorCount) => Problems.Conflict(
        $"ไฟล์นี้มีจุดผิด {errorCount} จุด ไม่ได้บันทึกอะไรเลย · ข้อมูลในระบบอาจเปลี่ยนไปหลังตรวจ กรุณากดตรวจไฟล์อีกครั้ง");

    static object Preview<TPlan>(ImportResult<TPlan> result) where TPlan : class, IImportPlan => new
    {
        result.ErrorCount,
        result.Errors,
        Summary = result.Plan?.Summary,
        HasChanges = result.Plan?.HasChanges ?? false,
        Changes = result.Plan?.Changes.Take(PreviewMaxChanges) ?? Enumerable.Empty<ImportChange>(),
        ChangeCount = result.Plan?.Changes.Count ?? 0,
    };

    static Task<List<ExistingEnrollment>> EnrollmentsOf(AppDbContext db, ClaimsPrincipal user, int classroomId) =>
        db.EnrollmentsOf(user)
            .Where(e => e.ClassroomId == classroomId)
            .OrderBy(e => e.No)
            .Select(e => new ExistingEnrollment(e.StudentId, e.No, e.Student.StudentCode, e.Student.FirstName, e.Student.LastName))
            .ToListAsync();

    static async Task<ScoreSnapshot> ScoreSnapshotOf(AppDbContext db, ClaimsPrincipal user, int classroomId)
    {
        var items = await db.ItemsOf(user)
            .Where(i => i.ClassroomId == classroomId)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new ExistingItem(i.Id, i.Name, i.MaxScore))
            .ToListAsync();
        var enrollments = await EnrollmentsOf(db, user, classroomId);

        var itemIds = items.Select(i => i.Id).ToList();
        var scores = await db.Scores
            .Where(s => itemIds.Contains(s.ItemId))
            .Select(s => new { s.ItemId, s.StudentId, s.Value })
            .ToListAsync();

        return new ScoreSnapshot(items, enrollments, scores.ToDictionary(s => (s.ItemId, s.StudentId), s => s.Value));
    }

    /// SaveChanges ครั้งเดียว EF ห่อด้วย transaction ให้อยู่แล้ว
    static async Task CommitStudents(AppDbContext db, int classroomId, StudentPlan plan)
    {
        var enrollments = await db.Enrollments.Where(e => e.ClassroomId == classroomId).ToDictionaryAsync(e => e.StudentId);

        foreach (var s in plan.Students)
        {
            if (s.StudentId is not { } studentId)
            {
                var student = new Student { StudentCode = s.Code, FirstName = s.FirstName, LastName = s.LastName };
                db.Enrollments.Add(new Enrollment { ClassroomId = classroomId, Student = student, No = s.No });
            }
            else if (enrollments.TryGetValue(studentId, out var enrollment))
            {
                enrollment.No = s.No;
            }
            else
            {
                db.Enrollments.Add(new Enrollment { ClassroomId = classroomId, StudentId = studentId, No = s.No });
            }
        }

        await db.SaveChangesAsync();
    }

    /// ต้องบันทึก 2 รอบ (สร้างรายการใหม่ให้ได้ id ก่อน audit จะอ้างถึง) จึงห่อด้วย transaction เอง
    static async Task CommitScores(AppDbContext db, ClaimsPrincipal user, int classroomId, ScorePlan plan)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var teacherId = user.UserId();
        var now = DateTime.UtcNow;

        var order = await db.Items.Where(i => i.ClassroomId == classroomId).MaxAsync(i => (int?)i.SortOrder) ?? 0;
        var createdItems = new Dictionary<string, AssessmentItem>(StringComparer.Ordinal);
        foreach (var newItem in plan.NewItems)
        {
            var item = new AssessmentItem { ClassroomId = classroomId, Name = newItem.Name, MaxScore = newItem.MaxScore, SortOrder = ++order };
            db.Items.Add(item);
            createdItems[newItem.Name] = item;
        }
        await db.SaveChangesAsync();

        var itemIds = plan.Scores.Select(s => s.ItemId).OfType<int>().Distinct().ToList();
        var existing = await db.Scores
            .Where(s => itemIds.Contains(s.ItemId))
            .ToDictionaryAsync(s => (s.ItemId, s.StudentId));

        foreach (var change in plan.Scores)
        {
            var itemId = change.ItemId ?? createdItems[change.ItemName].Id;
            if (!existing.TryGetValue((itemId, change.StudentId), out var score))
            {
                score = new Score { ItemId = itemId, StudentId = change.StudentId };
                db.Scores.Add(score);
            }
            ScoreWriter.Set(db, score, change.NewValue, teacherId, now);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    static string FileName(string kind, string classroom) =>
        $"{kind} {string.Concat(classroom.Select(c => UnsafeFileNameChars.Contains(c) ? '-' : c))}.xlsx";
}
