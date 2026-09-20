using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

/// <param name="Expected">
/// ค่าที่ครูเห็นอยู่บนหน้าจอตอนกดแก้ · ใช้ตรวจว่ามีใครแก้ช่องนี้ไปก่อนหรือเปล่า
/// ถ้าไม่ตรงกับในฐานข้อมูลจะตอบ 409 แทนที่จะทับของเขาเงียบ ๆ
/// </param>
public record SetScoreRequest(int ItemId, int StudentId, decimal? Value, decimal? Expected);

/// ตารางคะแนน · แก้ทีละช่องและเขียน ScoreAudit ทุกครั้งตามกฎโปรเจกต์
public static class ScoreEndpoints
{
    public static void MapScores(this WebApplication app)
    {
        var g = app.MapGroup("/api").RequireAuthorization(AuthSetup.Teacher);

        // ตารางทั้งห้องในคำขอเดียว ฝั่งเว็บจะได้ไม่ต้องยิงทีละช่อง
        g.MapGet("/classrooms/{id:int}/scores", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindClassroom(user, id) is null) return Problems.NotFound("ห้องเรียนนี้");

            var items = await db.ItemsOf(user)
                .Where(i => i.ClassroomId == id)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new { i.Id, i.Name, i.MaxScore, i.TeacherOnly })
                .ToListAsync();

            var students = await db.EnrollmentsOf(user)
                .Where(e => e.ClassroomId == id)
                .OrderBy(e => e.No)
                .Select(e => new { e.StudentId, e.No, e.Student.StudentCode, e.Student.FirstName, e.Student.LastName })
                .ToListAsync();

            var itemIds = items.Select(i => i.Id).ToList();
            var scores = await db.Scores
                .Where(s => itemIds.Contains(s.ItemId) && s.Value != null)
                .Select(s => new { s.ItemId, s.StudentId, s.Value })
                .ToListAsync();

            return Results.Ok(new { items, students, scores });
        });

        g.MapPut("/scores", async (SetScoreRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            var item = await db.FindItem(user, req.ItemId);
            if (item is null) return Problems.NotFound("รายการคะแนนนี้");

            // นักเรียนต้องอยู่ในห้องเดียวกับรายการนี้ ไม่งั้นจะให้คะแนนข้ามห้องได้
            var enrolled = await db.EnrollmentsOf(user)
                .AnyAsync(e => e.ClassroomId == item.ClassroomId && e.StudentId == req.StudentId);
            if (!enrolled) return Problems.NotFound("นักเรียนคนนี้ในห้องนี้");

            if (Validate.Score(req.Value, item.MaxScore) is { } error) return Problems.Invalid(error);

            var score = await db.Scores.FirstOrDefaultAsync(s => s.ItemId == req.ItemId && s.StudentId == req.StudentId);

            // กันครูสองคนแก้ช่องเดียวกันพร้อมกันแล้วคนหลังทับคนแรกโดยไม่มีใครรู้
            if (score?.Value != req.Expected)
                return Problems.Conflict(
                    $"คะแนนช่องนี้ถูกแก้ไปแล้วเป็น {(score?.Value is { } v ? v.ToString("0.##") : "ว่าง")} กรุณารีเฟรชแล้วลองใหม่");

            if (score is null)
            {
                score = new Score { ItemId = req.ItemId, StudentId = req.StudentId };
                db.Scores.Add(score);
            }

            var changed = ScoreWriter.Set(db, score, req.Value, user.UserId(), DateTime.UtcNow);
            if (changed) await db.SaveChangesAsync();

            return Results.Ok(new { req.ItemId, req.StudentId, score.Value });
        });

        // ประวัติการแก้คะแนนของช่องหนึ่ง — ไว้ใช้ตอนเด็กท้วงว่าคะแนนไม่ตรง
        g.MapGet("/scores/audits", async (int itemId, int studentId, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindItem(user, itemId) is null) return Problems.NotFound("รายการคะแนนนี้");

            var audits = await db.ScoreAudits
                .Where(a => a.ItemId == itemId && a.StudentId == studentId)
                .OrderByDescending(a => a.At)
                .Take(50)
                .ToListAsync();

            // ScoreAudit ตั้งใจไม่มี FK เพื่อให้ประวัติอยู่ต่อแม้ลบครูหรือรายการไปแล้ว จึงต้องหาชื่อครูเอง
            var teacherIds = audits.Select(a => a.TeacherId).Distinct().ToList();
            var teachers = await db.Teachers
                .Where(t => teacherIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name == "" ? t.Email : t.Name);

            return Results.Ok(audits.Select(a => new
            {
                a.Id,
                a.OldValue,
                a.NewValue,
                a.At,
                By = teachers.GetValueOrDefault(a.TeacherId, "ครูที่ถูกลบไปแล้ว"),
            }));
        });
    }
}
