using System.Security.Claims;
using Api.Auth;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

/// ข้อมูลของนักเรียนที่ล็อกอินอยู่ · studentId มาจาก cookie claim เท่านั้น ไม่รับ id จาก URL หรือ body
public static class MeEndpoints
{
    public static void MapMe(this WebApplication app)
    {
        var g = app.MapGroup("/api/me").RequireAuthorization(AuthSetup.Student);

        // คะแนนทุกห้องที่ลงชื่อไว้ เรียงจากภาคเรียนล่าสุด
        g.MapGet("/scores", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var studentId = user.UserId();

            return Results.Ok(await db.Enrollments
                .Where(e => e.StudentId == studentId)
                .OrderByDescending(e => e.Classroom.Term.CreatedAt)
                .ThenBy(e => e.Classroom.Name)
                .Select(e => new
                {
                    e.ClassroomId,
                    Classroom = e.Classroom.Name,
                    Term = e.Classroom.Term.Name,
                    e.No,
                    Items = e.Classroom.Items
                        .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                        .Select(i => new
                        {
                            i.Id,
                            i.Name,
                            i.MaxScore,
                            Value = db.Scores
                                .Where(s => s.ItemId == i.Id && s.StudentId == studentId)
                                .Select(s => s.Value)
                                .FirstOrDefault(),
                        })
                        .ToList(),
                })
                .ToListAsync());
        });
    }
}
