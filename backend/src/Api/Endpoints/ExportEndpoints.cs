using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Api.Import;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

/// <summary>
/// ส่งออกข้อมูลของห้องเป็น Excel ไฟล์เดียว 2 ชีท (รายชื่อนักเรียน กับ ตารางคะแนน)
/// เป็นข้อมูลขาออกอย่างเดียว ครูเอาไปดู เก็บ หรือส่งต่อได้ ไม่ใช่ไฟล์สำหรับนำเข้ากลับ
/// การนำเข้ามีทางเดียวคือไฟล์ของครูทั้งภาคเรียน (BookImportEndpoints) เพราะครูมีไฟล์แบบนั้นแบบเดียว
/// </summary>
public static class ExportEndpoints
{
    const string UnsafeFileNameChars = "\\/:*?\"<>|";

    public static void MapExport(this WebApplication app)
    {
        var g = app.MapGroup("/api/classrooms/{id:int}").RequireAuthorization(AuthSetup.Teacher);

        g.MapGet("/export", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var classroom = await db.FindClassroom(user, id);
            if (classroom is null) return Problems.NotFound("ห้องเรียนนี้");

            var items = await db.ItemsOf(user)
                .Where(i => i.ClassroomId == id)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new ExistingItem(i.Id, i.Name, i.MaxScore))
                .ToListAsync();
            var enrollments = await EnrollmentsOf(db, user, id);

            var itemIds = items.Select(i => i.Id).ToList();
            var scores = await db.Scores
                .Where(s => itemIds.Contains(s.ItemId))
                .Select(s => new { s.ItemId, s.StudentId, s.Value })
                .ToListAsync();

            var bytes = ImportTemplates.Classroom(items, enrollments,
                scores.ToDictionary(s => (s.ItemId, s.StudentId), s => s.Value));
            return Results.File(bytes, ImportTemplates.ContentType, FileName(classroom.Name));
        });
    }

    static Task<List<ExistingEnrollment>> EnrollmentsOf(AppDbContext db, ClaimsPrincipal user, int classroomId) =>
        db.EnrollmentsOf(user)
            .Where(e => e.ClassroomId == classroomId)
            .OrderBy(e => e.No)
            .Select(e => new ExistingEnrollment(e.StudentId, e.No, e.Student.StudentCode, e.Student.FirstName, e.Student.LastName))
            .ToListAsync();

    /// ชื่อไฟล์ที่ดาวน์โหลด · ตัดอักขระที่ Windows ใช้ตั้งชื่อไฟล์ไม่ได้ออก (ชื่อห้องมี / เช่น ม.1/1)
    static string FileName(string classroom)
    {
        var safe = new string(classroom.Select(c => UnsafeFileNameChars.Contains(c) ? '-' : c).ToArray());
        return $"คะแนน {safe}.xlsx";
    }
}
