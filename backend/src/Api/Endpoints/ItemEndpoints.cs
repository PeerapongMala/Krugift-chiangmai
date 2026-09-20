using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

/// <summary>
/// รายการที่เอาไว้ให้คะแนน เช่น "สอบกลางภาค 20 คะแนน" · หนึ่งห้องมีได้หลายรายการ
/// การเพิ่ม/แก้/ซ่อน ทำที่ระดับภาคเรียน (TermItemEndpoints) เพราะทุกห้องใช้รายการชุดเดียวกัน
/// เหลือไว้ที่นี่แค่ดูรายการของห้องเดียว กับลบรายการเดี่ยว ๆ
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
