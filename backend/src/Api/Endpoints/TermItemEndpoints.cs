using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

/// ซ่อน/แสดงรายการคะแนนชื่อนี้ทุกห้องในภาคเรียนพร้อมกัน
public record TermItemVisibilityRequest(string Name, bool Visible);

/// <summary>
/// จัดการรายการคะแนน "ทั้งภาคเรียน" ทีเดียว แทนการไล่ทำทีละห้อง
/// ไฟล์ครูนำเข้าทีเดียว 8 ห้อง ทุกห้องจึงมีรายการชื่อเดียวกัน (จำนวนเต็ม สอบ 1, สอบกลางภาค, ...)
/// ครูคิดเป็นระดับวิชา ไม่ใช่ระดับห้อง จึงรวมรายการที่ชื่อตรงกันเป็นแถวเดียวแล้วสั่งทีเดียว
/// </summary>
public static class TermItemEndpoints
{
    public static void MapTermItems(this WebApplication app)
    {
        var g = app.MapGroup("/api/terms/{termId:int}").RequireAuthorization(AuthSetup.Teacher);

        // รายการคะแนนของทั้งภาคเรียน รวมตามชื่อ · ค่าที่ไม่ตรงกันทุกห้องส่ง null ไปให้เว็บบอกว่าคละกัน
        g.MapGet("/items", async (int termId, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindTerm(user, termId) is null) return Problems.NotFound("ภาคเรียนนี้");

            var rows = await db.ItemsOf(user)
                .Where(i => i.Classroom.TermId == termId)
                .Select(i => new
                {
                    i.Id,
                    i.Name,
                    i.MaxScore,
                    i.TeacherOnly,
                    i.SortOrder,
                    Scored = db.Scores.Count(s => s.ItemId == i.Id && s.Value != null),
                    // ช่องที่ยังมีเศษ ใช้บอกครูว่ากดปัดแล้วจะเปลี่ยนกี่ช่อง
                    Fractional = db.Scores.Count(s => s.ItemId == i.Id && s.Value != null && s.Value != Math.Floor(s.Value!.Value)),
                })
                .ToListAsync();

            var groups = rows
                .GroupBy(i => i.Name)
                .Select(grp => new
                {
                    Name = grp.Key,
                    Classrooms = grp.Count(),
                    // null = คละกัน (บางห้องซ่อน บางห้องไม่ซ่อน หรือคะแนนเต็มไม่เท่ากัน)
                    Visible = grp.All(i => !i.TeacherOnly) ? true : grp.All(i => i.TeacherOnly) ? false : (bool?)null,
                    MaxScore = grp.Select(i => i.MaxScore).Distinct().Count() == 1 ? grp.First().MaxScore : (decimal?)null,
                    ScoredCount = grp.Sum(i => i.Scored),
                    FractionalCount = grp.Sum(i => i.Fractional),
                    SortOrder = grp.Min(i => i.SortOrder),
                })
                .OrderBy(grp => grp.SortOrder)
                .ThenBy(grp => grp.Name)
                .ToList();

            return Results.Ok(groups);
        });

        g.MapPatch("/items/visibility", async (int termId, TermItemVisibilityRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindTerm(user, termId) is null) return Problems.NotFound("ภาคเรียนนี้");

            var name = req.Name?.Trim() ?? "";
            if (name.Length == 0) return Problems.Invalid("กรุณาระบุชื่อรายการคะแนน");

            var changed = await db.ItemsOf(user)
                .Where(i => i.Classroom.TermId == termId && i.Name == name)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.TeacherOnly, !req.Visible));

            if (changed == 0) return Problems.NotFound("รายการคะแนนนี้ในภาคเรียนนี้");
            return Results.Ok(new { Classrooms = changed });
        });

        // ลบรายการชื่อนี้ทุกห้อง พร้อมคะแนนและคำถามที่ห้อยอยู่ · ใช้เก็บกวาดรายการที่นำเข้ามาผิด
        g.MapDelete("/items", async (int termId, string name, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindTerm(user, termId) is null) return Problems.NotFound("ภาคเรียนนี้");

            var wanted = name?.Trim() ?? "";
            if (wanted.Length == 0) return Problems.Invalid("กรุณาระบุชื่อรายการคะแนน");

            var ids = await db.ItemsOf(user)
                .Where(i => i.Classroom.TermId == termId && i.Name == wanted)
                .Select(i => i.Id)
                .ToListAsync();
            if (ids.Count == 0) return Problems.NotFound("รายการคะแนนนี้ในภาคเรียนนี้");

            await ItemEndpoints.DeleteItems(db, ids);
            return Results.Ok(new { Classrooms = ids.Count });
        });

        // ---------- ปัดคะแนนเป็นจำนวนเต็มทั้งภาคเรียน ----------

        // ดูก่อนว่าจะเปลี่ยนกี่ช่อง ครูจะได้ตัดสินใจก่อนกดจริง (ปัดแล้วเศษหายจากคะแนนปัจจุบัน เหลือแค่ในประวัติ)
        g.MapGet("/round-scores", async (int termId, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindTerm(user, termId) is null) return Problems.NotFound("ภาคเรียนนี้");

            var targets = await Roundable(db, user, termId).ToListAsync();
            return Results.Ok(new
            {
                Count = targets.Count(t => Rounding.ToWhole(t.Value, t.MaxScore) != t.Value),
                Items = targets
                    .Where(t => Rounding.ToWhole(t.Value, t.MaxScore) != t.Value)
                    .Select(t => t.Name).Distinct().OrderBy(n => n).ToList(),
            });
        });

        g.MapPost("/round-scores", async (int termId, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindTerm(user, termId) is null) return Problems.NotFound("ภาคเรียนนี้");

            var teacherId = user.UserId();
            var now = DateTime.UtcNow;
            var changed = 0;

            await using var tx = await db.Database.BeginTransactionAsync();

            // โหลดของจริงมาแก้ (ไม่ใช้ ExecuteUpdate) เพราะทุกช่องที่เปลี่ยนต้องมี ScoreAudit ตามกฎโปรเจกต์
            var scores = await db.ScoresOf(user)
                .Where(s => s.Item.Classroom.TermId == termId && !s.Item.TeacherOnly && s.Value != null)
                .Include(s => s.Item)
                .ToListAsync();

            foreach (var score in scores)
            {
                var whole = Rounding.ToWhole(score.Value!.Value, score.Item.MaxScore);
                if (ScoreWriter.Set(db, score, whole, teacherId, now)) changed++;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Ok(new { Changed = changed });
        });
    }

    /// คะแนนที่ปัดได้ = เฉพาะรายการที่นักเรียนเห็น (คะแนนดิบของครูต้องคงค่าจริงไว้)
    static IQueryable<RoundTarget> Roundable(AppDbContext db, ClaimsPrincipal user, int termId) =>
        db.ScoresOf(user)
            .Where(s => s.Item.Classroom.TermId == termId && !s.Item.TeacherOnly && s.Value != null)
            .Select(s => new RoundTarget(s.Item.Name, s.Value!.Value, s.Item.MaxScore));

    record RoundTarget(string Name, decimal Value, decimal MaxScore);
}
