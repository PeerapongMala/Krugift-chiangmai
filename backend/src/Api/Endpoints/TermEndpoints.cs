using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public record NameRequest(string Name);

public record PublicScoresRequest(bool Enabled);

/// ภาคเรียนและห้องเรียนของครู · ทุก query ผ่าน TeacherScope เพื่อกันครูดึงข้อมูลข้ามกัน
public static class TermEndpoints
{
    public static void MapTerms(this WebApplication app)
    {
        var g = app.MapGroup("/api").RequireAuthorization(AuthSetup.Teacher);

        // ---------- ภาคเรียน ----------

        g.MapGet("/terms", async (ClaimsPrincipal user, AppDbContext db) =>
            Results.Ok(await db.TermsOf(user)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { t.Id, t.Name, t.CreatedAt, t.PublicScores, ClassroomCount = t.Classrooms.Count })
                .ToListAsync()));

        g.MapPost("/terms", async (NameRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (Validate.Name(req.Name, "ชื่อภาคเรียน") is { } error) return Problems.Invalid(error);

            var name = req.Name.Trim();
            var teacherId = user.UserId();
            if (await db.Terms.AnyAsync(t => t.TeacherId == teacherId && t.Name == name))
                return Problems.Duplicate("ภาคเรียน");

            var term = new Term { TeacherId = teacherId, Name = name };
            db.Terms.Add(term);
            await db.SaveChangesAsync();
            return Results.Ok(new { term.Id, term.Name, term.CreatedAt, term.PublicScores, ClassroomCount = 0 });
        });

        g.MapPatch("/terms/{id:int}", async (int id, NameRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (Validate.Name(req.Name, "ชื่อภาคเรียน") is { } error) return Problems.Invalid(error);

            var term = await db.FindTerm(user, id);
            if (term is null) return Problems.NotFound("ภาคเรียนนี้");

            var name = req.Name.Trim();
            if (await db.Terms.AnyAsync(t => t.TeacherId == term.TeacherId && t.Name == name && t.Id != id))
                return Problems.Duplicate("ภาคเรียน");

            term.Name = name;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // เปิด/ปิดหน้าดูคะแนนด่วน (ไม่ต้องล็อกอิน) ของภาคเรียนนี้
        g.MapPatch("/terms/{id:int}/public-scores", async (int id, PublicScoresRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            var term = await db.FindTerm(user, id);
            if (term is null) return Problems.NotFound("ภาคเรียนนี้");

            term.PublicScores = req.Enabled;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapDelete("/terms/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var term = await db.FindTerm(user, id);
            if (term is null) return Problems.NotFound("ภาคเรียนนี้");

            // ไม่ให้ลบทั้งที่มีห้องอยู่ เพราะ cascade จะลากคะแนนหายไปทั้งหมดโดยที่ครูไม่ทันรู้ตัว
            if (await db.Classrooms.AnyAsync(c => c.TermId == id))
                return Problems.Conflict("ภาคเรียนนี้ยังมีห้องเรียนอยู่ ต้องลบห้องเรียนให้หมดก่อน");

            db.Terms.Remove(term);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------- ห้องเรียน ----------

        g.MapGet("/terms/{termId:int}/classrooms", async (int termId, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindTerm(user, termId) is null) return Problems.NotFound("ภาคเรียนนี้");

            return Results.Ok(await db.ClassroomsOf(user)
                .Where(c => c.TermId == termId)
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    StudentCount = c.Enrollments.Count,
                    ItemCount = c.Items.Count,
                })
                .ToListAsync());
        });

        g.MapPost("/terms/{termId:int}/classrooms", async (int termId, NameRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (Validate.Name(req.Name, "ชื่อห้องเรียน") is { } error) return Problems.Invalid(error);
            if (await db.FindTerm(user, termId) is null) return Problems.NotFound("ภาคเรียนนี้");

            var name = req.Name.Trim();
            if (await db.Classrooms.AnyAsync(c => c.TermId == termId && c.Name == name))
                return Problems.Duplicate("ห้องเรียน");

            var room = new Classroom { TermId = termId, Name = name };
            db.Classrooms.Add(room);
            await db.SaveChangesAsync();
            return Results.Ok(new { room.Id, room.Name, StudentCount = 0, ItemCount = 0 });
        });

        g.MapPatch("/classrooms/{id:int}", async (int id, NameRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (Validate.Name(req.Name, "ชื่อห้องเรียน") is { } error) return Problems.Invalid(error);

            var room = await db.FindClassroom(user, id);
            if (room is null) return Problems.NotFound("ห้องเรียนนี้");

            var name = req.Name.Trim();
            if (await db.Classrooms.AnyAsync(c => c.TermId == room.TermId && c.Name == name && c.Id != id))
                return Problems.Duplicate("ห้องเรียน");

            room.Name = name;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapDelete("/classrooms/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var room = await db.FindClassroom(user, id);
            if (room is null) return Problems.NotFound("ห้องเรียนนี้");

            if (await db.Items.AnyAsync(i => i.ClassroomId == id))
                return Problems.Conflict("ห้องเรียนนี้ยังมีรายการคะแนนอยู่ ต้องลบรายการให้หมดก่อน");

            db.Classrooms.Remove(room);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
