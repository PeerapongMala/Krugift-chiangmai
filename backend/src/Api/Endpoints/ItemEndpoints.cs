using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public record ItemRequest(string Name, decimal MaxScore);

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
            };
            db.Items.Add(item);
            await db.SaveChangesAsync();

            return Results.Ok(new { item.Id, item.Name, item.MaxScore, item.SortOrder, ScoredCount = 0 });
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
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapDelete("/items/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var item = await db.FindItem(user, id);
            if (item is null) return Problems.NotFound("รายการคะแนนนี้");

            // กันลบทิ้งทั้งที่กรอกคะแนนไปแล้ว เพราะ cascade จะลากคะแนนหายหมดโดยครูไม่ทันรู้ตัว
            var scored = await db.Scores.CountAsync(s => s.ItemId == id && s.Value != null);
            if (scored > 0)
                return Problems.Conflict($"รายการนี้มีคะแนนที่กรอกไว้ {scored} คน ต้องล้างคะแนนให้หมดก่อนจึงลบได้");

            db.Items.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
