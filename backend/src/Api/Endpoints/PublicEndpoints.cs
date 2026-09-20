using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public record QuickScoreRequest(int ClassroomId, int No, string StudentCode);

/// <summary>
/// ดูคะแนนด่วนโดยไม่ต้องล็อกอิน — เลือกห้อง + เลขที่ แล้วพิมพ์รหัสนักเรียน
///
/// ข้อแลกเปลี่ยนที่ตกลงกับครูแล้ว: ทั้ง 3 อย่างเพื่อนร่วมห้องรู้กันได้ จึงดูคะแนนเพื่อนได้
/// ระดับเดียวกับประกาศคะแนนติดบอร์ด · แลกกับความเร็วที่ครูขอ
/// รั้วที่ใส่ไว้: อ่านอย่างเดียว · ผิดตรงไหนก็ตอบข้อความเดียวกัน · rate limit ·
/// ไม่สร้าง cookie · ครูปิดได้รายภาคเรียน (Term.PublicScores)
/// ไม่มี dropdown ชื่อ เพราะจะทำให้ใครก็เปิดดูรายชื่อเด็กทั้งห้องได้โดยไม่ต้องรู้อะไรเลย
/// </summary>
public static class PublicEndpoints
{
    // ตั้งใจใช้ข้อความเดียวทุกกรณี ไม่บอกว่าผิดช่องไหน กันคนไล่เดาทีละช่อง
    const string Mismatch = "ข้อมูลไม่ตรงกับในระบบ กรุณาตรวจสอบห้อง เลขที่ และรหัสนักเรียนอีกครั้ง";

    public static void MapPublic(this WebApplication app)
    {
        var g = app.MapGroup("/api/public");

        // ห้องสำหรับ dropdown พร้อมเลขที่ที่มีจริง · ไม่ส่งชื่อนักเรียนออกไปเด็ดขาด
        g.MapGet("/classrooms", async (AppDbContext db) =>
            Results.Ok(await db.Classrooms
                .Where(c => c.Term.PublicScores)
                .OrderByDescending(c => c.Term.CreatedAt)
                .ThenBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    Term = c.Term.Name,
                    Nos = c.Enrollments.OrderBy(e => e.No).Select(e => e.No).ToList(),
                })
                .ToListAsync()));

        g.MapPost("/scores", async (QuickScoreRequest req, AppDbContext db) =>
        {
            var code = req.StudentCode?.Trim() ?? "";
            if (Validate.StudentCode(code) is not null) return Problems.Invalid(Mismatch);

            var enrollment = await db.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Classroom).ThenInclude(c => c.Term)
                .FirstOrDefaultAsync(e =>
                    e.ClassroomId == req.ClassroomId
                    && e.No == req.No
                    && e.Student.StudentCode == code
                    && e.Classroom.Term.PublicScores);
            if (enrollment is null) return Problems.Invalid(Mismatch);

            var studentId = enrollment.StudentId;
            var items = await db.Items
                // รายการที่ครูดูคนเดียวไม่โผล่ในหน้าสาธารณะเช่นกัน
                .Where(i => i.ClassroomId == req.ClassroomId && !i.TeacherOnly)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new
                {
                    i.Name,
                    i.MaxScore,
                    Value = db.Scores
                        .Where(s => s.ItemId == i.Id && s.StudentId == studentId)
                        .Select(s => s.Value)
                        .FirstOrDefault(),
                })
                .ToListAsync();

            return Results.Ok(new
            {
                Name = $"{enrollment.Student.FirstName} {enrollment.Student.LastName}",
                Classroom = enrollment.Classroom.Name,
                Term = enrollment.Classroom.Term.Name,
                Items = items,
                // รวมเฉพาะรายการที่มีคะแนนแล้ว รายการที่ครูยังไม่ได้สอบไม่ถูกนับเป็นคะแนนที่หายไป
                Total = items.Sum(i => i.Value ?? 0),
                Full = items.Where(i => i.Value != null).Sum(i => i.MaxScore),
            });
        }).RequireRateLimiting(AuthSetup.LoginLimit);
    }
}
