using System.Security.Claims;
using Api.Auth;
using Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public record CodeLoginRequest(string StudentCode, string Code);

public static class AuthEndpoints
{
    const string WrongCode = "รหัสนักเรียนหรือรหัสส่วนตัวไม่ถูกต้อง";
    const string Locked = "ใส่รหัสผิดหลายครั้ง กรุณารอ 15 นาทีแล้วลองใหม่";

    public static void MapAuth(this WebApplication app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapGet("/me", async (HttpContext http) =>
        {
            if (http.User.Identity?.IsAuthenticated == true)
                return Results.Ok(new
                {
                    role = http.User.FindFirstValue(ClaimTypes.Role),
                    name = http.User.FindFirstValue(ClaimTypes.Name),
                });
            // ล็อกอิน Google แล้วแต่ยังไม่ได้ผูกกับนักเรียน
            var ext = await http.AuthenticateAsync(AuthSetup.External);
            return ext.Succeeded ? Results.Ok(new { role = "pending", name = ext.Principal.FindFirstValue(ClaimTypes.Email) }) : Results.Unauthorized();
        });

        g.MapGet("/google", async (IAuthenticationSchemeProvider schemes) =>
            await schemes.GetSchemeAsync("Google") is null
                ? Results.Problem("ยังไม่ได้ตั้งค่า Google login", statusCode: 503)
                : Results.Challenge(new AuthenticationProperties { RedirectUri = "/api/auth/google/done" }, ["Google"]));

        g.MapGet("/google/done", async (HttpContext http, AppDbContext db) =>
        {
            var ext = await http.AuthenticateAsync(AuthSetup.External);
            if (!ext.Succeeded) return Results.Redirect("/login?error=google");

            var email = ext.Principal.FindFirstValue(ClaimTypes.Email)?.ToLowerInvariant();
            var sub = ext.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var teacher = email is null ? null : await db.Teachers.FirstOrDefaultAsync(t => t.Email == email);
            if (teacher is not null)
            {
                await SignIn(http, AuthSetup.Teacher, teacher.Id, teacher.Name is "" ? teacher.Email : teacher.Name);
                return Results.Redirect("/teacher");
            }

            var student = await db.Students.FirstOrDefaultAsync(s => s.GoogleSub == sub);
            if (student is not null)
            {
                await SignIn(http, AuthSetup.Student, student.Id, $"{student.FirstName} {student.LastName}");
                return Results.Redirect("/student");
            }

            return Results.Redirect("/claim"); // เก็บ External cookie ไว้ใช้ตอน claim
        });

        g.MapPost("/claim", async (CodeLoginRequest req, HttpContext http, AppDbContext db) =>
        {
            var ext = await http.AuthenticateAsync(AuthSetup.External);
            if (!ext.Succeeded) return Results.Unauthorized();
            var sub = ext.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var (student, error) = await Verify(db, req);
            if (student is null) return Results.Problem(error, statusCode: 400);

            if (student.GoogleSub is not null && student.GoogleSub != sub)
                return Results.Problem("นักเรียนคนนี้ผูกกับบัญชี Google อื่นไปแล้ว กรุณาติดต่อครู", statusCode: 409);
            if (await db.Students.AnyAsync(s => s.GoogleSub == sub && s.Id != student.Id))
                return Results.Problem("บัญชี Google นี้ผูกกับนักเรียนคนอื่นไปแล้ว", statusCode: 409);

            student.GoogleSub = sub;
            await db.SaveChangesAsync();
            await SignIn(http, AuthSetup.Student, student.Id, $"{student.FirstName} {student.LastName}");
            return Results.Ok(new { role = AuthSetup.Student });
        }).RequireRateLimiting(AuthSetup.LoginLimit);

        g.MapPost("/code-login", async (CodeLoginRequest req, HttpContext http, AppDbContext db) =>
        {
            var (student, error) = await Verify(db, req);
            if (student is null) return Results.Problem(error, statusCode: 400);

            await SignIn(http, AuthSetup.Student, student.Id, $"{student.FirstName} {student.LastName}");
            return Results.Ok(new { role = AuthSetup.Student });
        }).RequireRateLimiting(AuthSetup.LoginLimit);

        g.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignOutAsync(AuthSetup.External);
            return Results.NoContent();
        });

        if (app.Environment.IsDevelopment())
        {
            // เอาไว้ทดสอบบนเครื่องตอนยังไม่มี Google OAuth — มีเฉพาะ Development
            g.MapPost("/dev-login", async (string email, HttpContext http, AppDbContext db) =>
            {
                var t = await db.Teachers.FirstOrDefaultAsync(x => x.Email == email.ToLowerInvariant());
                if (t is null) return Results.NotFound();
                await SignIn(http, AuthSetup.Teacher, t.Id, t.Email);
                return Results.Ok(new { role = AuthSetup.Teacher });
            });
        }
    }

    static async Task SignIn(HttpContext http, string role, int id, string name)
    {
        await http.SignOutAsync(AuthSetup.External);
        await http.SignInAsync(role, id, name);
    }

    static async Task<(Student? Student, string Error)> Verify(AppDbContext db, CodeLoginRequest req)
    {
        var code = req.StudentCode?.Trim() ?? "";
        var student = await db.Students.FirstOrDefaultAsync(s => s.StudentCode == code);
        if (student is null) return (null, WrongCode);

        var now = DateTime.UtcNow;
        if (AccessCode.IsLocked(student, now)) return (null, Locked);

        var ok = AccessCode.TryVerify(student, req.Code ?? "", now);
        await db.SaveChangesAsync();
        return ok ? (student, "") : (null, AccessCode.IsLocked(student, now) ? Locked : WrongCode);
    }
}
