using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public record AddStaffRequest(string Email, TeacherRole Role);

public record ChangeRoleRequest(TeacherRole Role);

/// จัดการรายชื่อครู — เฉพาะครูที่เป็น Owner เท่านั้น
public static class StaffEndpoints
{
    public static void MapStaff(this WebApplication app)
    {
        var g = app.MapGroup("/api/staff").RequireAuthorization(AuthSetup.OwnerPolicy);

        g.MapGet("/", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var meId = user.UserId();
            return Results.Ok(await db.Teachers
                .OrderBy(t => t.Role)
                .ThenBy(t => t.Email)
                .Select(t => new { t.Id, t.Email, t.Name, Role = t.Role.ToString(), IsMe = t.Id == meId })
                .ToListAsync());
        });

        g.MapPost("/", async (AddStaffRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await Owner(db, user) is null) return Problems.Denied();
            if (Validate.Email(req.Email) is { } error) return Problems.Invalid(error);

            var email = req.Email.Trim().ToLowerInvariant();
            if (await db.Teachers.AnyAsync(t => t.Email == email))
                return Problems.Conflict("มีครูอีเมลนี้อยู่แล้ว");

            var teacher = new Teacher { Email = email, Role = req.Role };
            db.Teachers.Add(teacher);
            await db.SaveChangesAsync();
            return Results.Ok(new { teacher.Id, teacher.Email, teacher.Name, Role = teacher.Role.ToString() });
        });

        g.MapPatch("/{id:int}", async (int id, ChangeRoleRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            var me = await Owner(db, user);
            if (me is null) return Problems.Denied();
            // กันเจ้าของเผลอลดยศตัวเองจนไม่มีใครจัดการรายชื่อครูได้
            if (id == me.Id) return Problems.Invalid("เปลี่ยนสิทธิ์ของตัวเองไม่ได้ ให้ผู้ดูแลระบบอีกคนเปลี่ยนให้");

            var target = await db.Teachers.FindAsync(id);
            if (target is null) return Problems.NotFound("ครูคนนี้");
            if (target.Role == req.Role) return Results.NoContent();

            if (await WouldRemoveLastOwner(db, target, req.Role))
                return Problems.Conflict("ต้องมีผู้ดูแลระบบอย่างน้อย 1 คน");

            target.Role = req.Role;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var me = await Owner(db, user);
            if (me is null) return Problems.Denied();
            if (id == me.Id) return Problems.Invalid("ลบบัญชีตัวเองไม่ได้");

            var target = await db.Teachers.FindAsync(id);
            if (target is null) return Problems.NotFound("ครูคนนี้");

            // ภาคเรียนผูกกับ TeacherId อยู่ ลบครูทิ้งเฉย ๆ จะทำให้ข้อมูลคะแนนหาย
            if (await db.Terms.AnyAsync(t => t.TeacherId == id))
                return Problems.Conflict("ครูคนนี้มีภาคเรียนอยู่ ลบไม่ได้ ให้ลบหรือย้ายภาคเรียนก่อน");

            if (await WouldRemoveLastOwner(db, target, TeacherRole.Teacher))
                return Problems.Conflict("ต้องมีผู้ดูแลระบบอย่างน้อย 1 คน");

            db.Teachers.Remove(target);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// เช็คยศจาก DB ซ้ำ ไม่เชื่อ claim อย่างเดียว เพราะ cookie อยู่ได้ 30 วัน
    /// ถ้าเพิ่งโดนลดยศไป claim เดิมจะยังบอกว่าเป็น Owner อยู่
    static async Task<Teacher?> Owner(AppDbContext db, ClaimsPrincipal user)
    {
        var me = await db.Teachers.FindAsync(user.UserId());
        return me?.Role is TeacherRole.Owner ? me : null;
    }

    static async Task<bool> WouldRemoveLastOwner(AppDbContext db, Teacher target, TeacherRole newRole) =>
        target.Role is TeacherRole.Owner
        && newRole is not TeacherRole.Owner
        && await db.Teachers.CountAsync(t => t.Role == TeacherRole.Owner) <= 1;
}
