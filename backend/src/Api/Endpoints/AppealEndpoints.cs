using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public record OpenAppealRequest(int ItemId, string Body);

public record AppealMessageRequest(string Body);

/// <summary>
/// ท้วงคะแนน — นักเรียนเปิดเรื่องต่อหนึ่งรายการคะแนน แล้วคุยกับครูเป็น thread
/// ใช้ endpoint ชุดเดียวทั้งสองฝั่ง · นักเรียนเห็นเฉพาะเรื่องของตัวเอง (studentId จาก cookie)
/// ครูเห็นผ่าน TeacherScope (ครูทั่วไปเห็นห้องตัวเอง Owner เห็นทุกห้อง)
/// </summary>
public static class AppealEndpoints
{
    public static void MapAppeals(this WebApplication app)
    {
        var g = app.MapGroup("/api/appeals").RequireAuthorization();

        g.MapGet("/", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var isTeacher = user.IsInRole(AuthSetup.Teacher);
            return Results.Ok(await Visible(db, user)
                // เรื่องที่ยังไม่ปิดขึ้นก่อน แล้วค่อยเรียงใหม่สุด
                .OrderBy(a => a.Status == AppealStatus.Closed)
                .ThenByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    Status = a.Status.ToString(),
                    a.CreatedAt,
                    Unread = isTeacher ? a.UnreadByTeacher : a.UnreadByStudent,
                    Item = a.Item.Name,
                    Classroom = a.Item.Classroom.Name,
                    Term = a.Item.Classroom.Term.Name,
                    Student = a.Student.FirstName + " " + a.Student.LastName,
                    a.Student.StudentCode,
                    LastMessage = a.Messages.OrderByDescending(m => m.At).Select(m => m.Body).FirstOrDefault(),
                })
                .ToListAsync());
        });

        // ตัวเลขบน badge ของเมนูท้วงคะแนน
        g.MapGet("/unread-count", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var isTeacher = user.IsInRole(AuthSetup.Teacher);
            var count = await Visible(db, user).CountAsync(a => isTeacher ? a.UnreadByTeacher : a.UnreadByStudent);
            return Results.Ok(new { Count = count });
        });

        g.MapPost("/", async (OpenAppealRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (!user.IsInRole(AuthSetup.Student)) return Problems.Denied("สอบถามคะแนนได้เฉพาะนักเรียน");
            if (Validate.Message(req.Body) is { } error) return Problems.Invalid(error);

            var studentId = user.UserId();
            // ต้องเป็นรายการในห้องที่ตัวเองลงชื่ออยู่เท่านั้น
            // รายการที่ครูดูคนเดียวถือว่าไม่มีในสายตานักเรียน สอบถามไม่ได้
            var item = await db.Items.FirstOrDefaultAsync(i =>
                i.Id == req.ItemId && !i.TeacherOnly && i.Classroom.Enrollments.Any(e => e.StudentId == studentId));
            if (item is null) return Problems.NotFound("รายการคะแนนนี้ในห้องของคุณ");

            // เรื่องเดิมยังไม่ปิด ให้คุยต่อในเรื่องเดิม ครูจะได้ไม่ต้องตอบซ้ำหลายที่
            var hasOpen = await db.Appeals.AnyAsync(a =>
                a.ItemId == item.Id && a.StudentId == studentId && a.Status != AppealStatus.Closed);
            if (hasOpen) return Problems.Conflict("มีคำถามเรื่องรายการนี้ที่ยังไม่เสร็จสิ้นอยู่แล้ว ให้ถามต่อในคำถามเดิม");

            var now = DateTime.UtcNow;
            var appeal = new Appeal
            {
                ItemId = item.Id,
                StudentId = studentId,
                CreatedAt = now,
                UnreadByTeacher = true,
                UnreadByStudent = false,
            };
            appeal.Messages.Add(new AppealMessage { Body = req.Body.Trim(), FromTeacher = false, At = now });
            db.Appeals.Add(appeal);
            await db.SaveChangesAsync();
            return Results.Ok(new { appeal.Id });
        });

        g.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var isTeacher = user.IsInRole(AuthSetup.Teacher);
            var appeal = await Visible(db, user)
                .Include(a => a.Messages)
                .Include(a => a.Student)
                .Include(a => a.Item).ThenInclude(i => i.Classroom).ThenInclude(c => c.Term)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (appeal is null) return Problems.NotFound("คำถามนี้");

            // เปิดอ่านแล้ว = ล้าง badge ของฝั่งตัวเอง
            if (isTeacher && appeal.UnreadByTeacher) appeal.UnreadByTeacher = false;
            else if (!isTeacher && appeal.UnreadByStudent) appeal.UnreadByStudent = false;
            await db.SaveChangesAsync();

            var score = await db.Scores
                .Where(s => s.ItemId == appeal.ItemId && s.StudentId == appeal.StudentId)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            return Results.Ok(new
            {
                appeal.Id,
                Status = appeal.Status.ToString(),
                appeal.CreatedAt,
                appeal.ItemId,
                appeal.Item.ClassroomId,
                Item = appeal.Item.Name,
                appeal.Item.MaxScore,
                Score = score,
                Classroom = appeal.Item.Classroom.Name,
                Term = appeal.Item.Classroom.Term.Name,
                Student = $"{appeal.Student.FirstName} {appeal.Student.LastName}",
                appeal.Student.StudentCode,
                Messages = appeal.Messages.OrderBy(m => m.At).Select(m => new { m.Id, m.FromTeacher, m.Body, m.At }),
            });
        });

        g.MapPost("/{id:int}/messages", async (int id, AppealMessageRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (Validate.Message(req.Body) is { } error) return Problems.Invalid(error);

            var isTeacher = user.IsInRole(AuthSetup.Teacher);
            var appeal = await Visible(db, user).FirstOrDefaultAsync(a => a.Id == id);
            if (appeal is null) return Problems.NotFound("คำถามนี้");
            if (appeal.Status == AppealStatus.Closed)
                return Problems.Conflict("คำถามนี้เสร็จสิ้นแล้ว ถ้ายังมีข้อสงสัยให้ส่งคำถามใหม่");

            db.AppealMessages.Add(new AppealMessage
            {
                AppealId = id,
                FromTeacher = isTeacher,
                Body = req.Body.Trim(),
                At = DateTime.UtcNow,
            });
            // ฝั่งไหนตอบ อีกฝั่งจะมี badge · ครูตอบ = Answered · นักเรียนตอบกลับ = Open อีกครั้ง
            appeal.Status = isTeacher ? AppealStatus.Answered : AppealStatus.Open;
            appeal.UnreadByTeacher = !isTeacher;
            appeal.UnreadByStudent = isTeacher;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPatch("/{id:int}/close", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var isTeacher = user.IsInRole(AuthSetup.Teacher);
            var appeal = await Visible(db, user).FirstOrDefaultAsync(a => a.Id == id);
            if (appeal is null) return Problems.NotFound("คำถามนี้");
            if (appeal.Status == AppealStatus.Closed) return Results.NoContent();

            appeal.Status = AppealStatus.Closed;
            // ปิดโดยฝั่งไหน อีกฝั่งควรได้รู้
            if (isTeacher) appeal.UnreadByStudent = true;
            else appeal.UnreadByTeacher = true;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// เรื่องท้วงที่ผู้ใช้คนนี้มีสิทธิ์เห็น
    static IQueryable<Appeal> Visible(AppDbContext db, ClaimsPrincipal user)
    {
        if (user.IsInRole(AuthSetup.Teacher)) return db.AppealsOf(user);
        if (user.IsInRole(AuthSetup.Student))
        {
            var studentId = user.UserId();
            return db.Appeals.Where(a => a.StudentId == studentId);
        }
        return db.Appeals.Where(_ => false);
    }
}
