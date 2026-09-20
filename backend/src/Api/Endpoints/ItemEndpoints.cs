using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

/// TeacherOnly = ซ่อนจากนักเรียน (ค่าเริ่มต้นคือไม่ซ่อน)
public record ItemRequest(string Name, decimal MaxScore, bool TeacherOnly = false);

/// Visible = นักเรียนเห็นรายการนี้ (ตรงข้ามกับ TeacherOnly) ใช้กับสวิตช์บนหน้ารายการคะแนน
public record ItemVisibilityRequest(bool Visible);

/// รายการที่เอาไว้ให้คะแนน เช่น "สอบกลางภาค 20 คะแนน" · หนึ่งห้องมีได้หลายรายการ
public static class ItemEndpoints
{
    public static void MapItems(this WebApplication app)
    {
        var g = app.MapGroup("/api").RequireAuthorization(AuthSetup.Teacher);

        g.MapGet("/classrooms/{id:int}/items", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindClassroom(user, id) is null) return Problems.NotFound("ห้องเรียนนี้");

            return Results.Ok(await db.ItemsOf(user)
                .Where(i => i.ClassroomId == id)
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.Id)
                .Select(i => new
                {
                    i.Id,
                    i.Name,
                    i.MaxScore,
                    i.SortOrder,
                    i.TeacherOnly,
                    // ครูจะได้รู้ว่ารายการไหนกรอกคะแนนไปแล้วบ้าง ลบทิ้งจะได้ไม่เผลอ
                    ScoredCount = db.Scores.Count(s => s.ItemId == i.Id && s.Value != null),
                })
                .ToListAsync());
        });

        g.MapPost("/classrooms/{id:int}/items", async (int id, ItemRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (Validate.Name(req.Name, "ชื่อรายการ") is { } e1) return Problems.Invalid(e1);
            if (Validate.MaxScore(req.MaxScore) is { } e2) return Problems.Invalid(e2);

            if (await db.FindClassroom(user, id) is null) return Problems.NotFound("ห้องเรียนนี้");

            var name = req.Name.Trim();
            if (await db.Items.AnyAsync(i => i.ClassroomId == id && i.Name == name))
                return Problems.Duplicate("รายการคะแนน");

            // ต่อท้ายรายการเดิมเสมอ ครูจะได้ไม่ต้องกรอกลำดับเอง
            var nextOrder = await db.Items.Where(i => i.ClassroomId == id).MaxAsync(i => (int?)i.SortOrder) ?? 0;

            var item = new AssessmentItem
            {
                ClassroomId = id,
                Name = name,
                MaxScore = req.MaxScore,
                SortOrder = nextOrder + 1,
                TeacherOnly = req.TeacherOnly,
            };
            db.Items.Add(item);
            await db.SaveChangesAsync();

            return Results.Ok(new { item.Id, item.Name, item.MaxScore, item.SortOrder, item.TeacherOnly, ScoredCount = 0 });
        });

        g.MapPatch("/items/{id:int}", async (int id, ItemRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (Validate.Name(req.Name, "ชื่อรายการ") is { } e1) return Problems.Invalid(e1);
            if (Validate.MaxScore(req.MaxScore) is { } e2) return Problems.Invalid(e2);

            var item = await db.FindItem(user, id);
            if (item is null) return Problems.NotFound("รายการคะแนนนี้");

            var name = req.Name.Trim();
            if (await db.Items.AnyAsync(i => i.ClassroomId == item.ClassroomId && i.Name == name && i.Id != id))
                return Problems.Duplicate("รายการคะแนน");

            // ลดคะแนนเต็มลงต่ำกว่าคะแนนที่กรอกไปแล้ว จะทำให้ข้อมูลขัดกันเอง
            if (req.MaxScore < item.MaxScore)
            {
                var highest = await db.Scores.Where(s => s.ItemId == id).MaxAsync(s => s.Value);
                if (highest > req.MaxScore)
                    return Problems.Conflict($"มีคะแนนที่กรอกไว้สูงถึง {highest:0.##} ลดคะแนนเต็มต่ำกว่านั้นไม่ได้");
            }

            item.Name = name;
            item.MaxScore = req.MaxScore;
            item.TeacherOnly = req.TeacherOnly;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // สลับให้นักเรียนเห็น/ไม่เห็นทีละรายการ แยกจาก PATCH เต็มรูปแบบข้างบน
        // เพราะครูแค่กดสวิตช์ ไม่ควรต้องส่งชื่อกับคะแนนเต็มมาด้วย (ส่งมาผิดจะทับของเดิม)
        g.MapPatch("/items/{id:int}/visibility", async (int id, ItemVisibilityRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            var item = await db.FindItem(user, id);
            if (item is null) return Problems.NotFound("รายการคะแนนนี้");

            item.TeacherOnly = !req.Visible;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // withScores=true = ครูยืนยันแล้วว่าให้ลบคะแนนไปพร้อมกัน (หน้าเว็บบอกจำนวนคนก่อนถาม)
        // withScores เป็น query ที่ไม่ส่งมาก็ได้ ต้องใส่ค่า default ไม่งั้น Minimal API ถือว่าบังคับแล้วตอบ 400
        g.MapDelete("/items/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db, bool withScores = false) =>
        {
            var item = await db.FindItem(user, id);
            if (item is null) return Problems.NotFound("รายการคะแนนนี้");

            // กันลบทิ้งทั้งที่กรอกคะแนนไปแล้ว เพราะคะแนนจะหายหมดโดยครูไม่ทันรู้ตัว
            var scored = await db.Scores.CountAsync(s => s.ItemId == id && s.Value != null);
            if (scored > 0 && !withScores)
                return Problems.Conflict($"รายการนี้มีคะแนนที่กรอกไว้ {scored} คน ต้องล้างคะแนนให้หมดก่อนจึงลบได้");

            await DeleteItems(db, [id]);
            return Results.NoContent();
        });
    }

    /// <summary>
    /// ลบรายการคะแนนพร้อมของที่ห้อยอยู่ ใน transaction เดียว
    /// ลำดับต้องเป็น คำถาม → ประวัติ → คะแนน → รายการ เหมือนตอนลบทั้งภาคเรียน ไม่งั้นติด foreign key
    /// ประวัติการแก้คะแนนหายไปด้วยโดยธรรมชาติ เพราะมันผูกกับรายการที่ถูกลบ
    /// </summary>
    internal static async Task DeleteItems(AppDbContext db, IReadOnlyList<int> itemIds)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var appealIds = await db.Appeals.Where(a => itemIds.Contains(a.ItemId)).Select(a => a.Id).ToListAsync();
        await db.AppealMessages.Where(m => appealIds.Contains(m.AppealId)).ExecuteDeleteAsync();
        await db.Appeals.Where(a => itemIds.Contains(a.ItemId)).ExecuteDeleteAsync();
        await db.ScoreAudits.Where(a => itemIds.Contains(a.ItemId)).ExecuteDeleteAsync();
        await db.Scores.Where(s => itemIds.Contains(s.ItemId)).ExecuteDeleteAsync();
        await db.Items.Where(i => itemIds.Contains(i.Id)).ExecuteDeleteAsync();

        await tx.CommitAsync();
    }
}
