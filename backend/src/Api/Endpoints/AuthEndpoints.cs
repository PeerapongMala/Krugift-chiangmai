using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public record ClaimRequest(string StudentCode);

public static class AuthEndpoints
{
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
                    isOwner = http.User.IsOwner(),
                });

            // ล็อกอิน Google แล้วแต่ยังไม่ได้ผูกกับนักเรียน
            var ext = await http.AuthenticateAsync(AuthSetup.External);
            return ext.Succeeded
                ? Results.Ok(new { role = "pending", name = ext.Principal.FindFirstValue(ClaimTypes.Email), isOwner = false })
                : Results.Unauthorized();
        });

        g.MapGet("/google", async (HttpContext http, IAuthenticationSchemeProvider schemes) =>
        {
            if (await schemes.GetSchemeAsync("Google") is null)
                return Results.Problem("ยังไม่ได้ตั้งค่า Google login", statusCode: 503);

            // ล้าง cookie ชั่วคราวของรอบก่อน กันสถานะค้างจากการล็อกอินที่ทำไม่จบ
            await http.SignOutAsync(AuthSetup.External);
            return Results.Challenge(new AuthenticationProperties { RedirectUri = "/api/auth/google/done" }, ["Google"]);
        });

        g.MapGet("/google/done", async (HttpContext http, AppDbContext db) =>
        {
            var ext = await http.AuthenticateAsync(AuthSetup.External);
            if (!ext.Succeeded) return Results.Redirect("/login?error=google");

            var email = ext.Principal.FindFirstValue(ClaimTypes.Email)?.ToLowerInvariant();
            var sub = ext.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var teacher = email is null ? null : await db.Teachers.FirstOrDefaultAsync(t => t.Email == email);
            if (teacher is not null)
            {
                // เก็บชื่อจาก Google ไว้แสดงผล จะได้ไม่ต้องโชว์อีเมลเปล่า ๆ
                var displayName = ext.Principal.FindFirstValue(ClaimTypes.Name);
                if (!string.IsNullOrWhiteSpace(displayName) && teacher.Name != displayName)
                {
                    teacher.Name = displayName;
                    await db.SaveChangesAsync();
                }

                await SignIn(http, AuthSetup.Teacher, teacher.Id,
                    teacher.Name is "" ? teacher.Email : teacher.Name, teacher.Role is TeacherRole.Owner);
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

        // ผูกบัญชี Google กับนักเรียนครั้งแรก — กรอกแค่รหัสนักเรียน
        // ตัวยืนยันตัวตนจริงคือบัญชี Google ของเจ้าตัว รหัสนักเรียนเป็นแค่ตัวชี้ว่าเป็นใครในระบบ
        g.MapPost("/claim", async (ClaimRequest req, HttpContext http, AppDbContext db) =>
        {
            var ext = await http.AuthenticateAsync(AuthSetup.External);
            if (!ext.Succeeded) return Results.Unauthorized();
            var sub = ext.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (Validate.StudentCode(req.StudentCode) is { } error) return Problems.Invalid(error);
            var code = req.StudentCode.Trim();

            var student = await db.Students.FirstOrDefaultAsync(s => s.StudentCode == code);
            if (student is null)
                return Problems.Invalid("ไม่พบรหัสนักเรียนนี้ในระบบ กรุณาตรวจสอบตัวเลขอีกครั้ง หรือติดต่อครู");

            if (student.GoogleSub is not null && student.GoogleSub != sub)
                return Problems.Conflict("รหัสนักเรียนนี้ผูกกับบัญชี Google อื่นไปแล้ว กรุณาให้ครูยกเลิกการผูกก่อน");

            if (await db.Students.AnyAsync(s => s.GoogleSub == sub && s.Id != student.Id))
                return Problems.Conflict("บัญชี Google นี้ผูกกับนักเรียนคนอื่นไปแล้ว");

            student.GoogleSub = sub;
            await db.SaveChangesAsync();
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
            // ใช้ทดสอบบนเครื่องโดยไม่ต้องตั้ง Google OAuth — มีเฉพาะ Development
            g.MapPost("/dev-login", async (string email, HttpContext http, AppDbContext db) =>
            {
                var t = await db.Teachers.FirstOrDefaultAsync(x => x.Email == email.ToLowerInvariant());
                if (t is null) return Results.NotFound();
                await SignIn(http, AuthSetup.Teacher, t.Id, t.Email, t.Role is TeacherRole.Owner);
                return Results.Ok(new { role = AuthSetup.Teacher });
            });

            // ทดสอบสิทธิ์ฝั่งนักเรียนโดยไม่ต้องมีบัญชี Google
            g.MapPost("/dev-login-student", async (string studentCode, HttpContext http, AppDbContext db) =>
            {
                var s = await db.Students.FirstOrDefaultAsync(x => x.StudentCode == studentCode);
                if (s is null) return Results.NotFound();
                await SignIn(http, AuthSetup.Student, s.Id, $"{s.FirstName} {s.LastName}");
                return Results.Ok(new { role = AuthSetup.Student });
            });
        }
    }

    static async Task SignIn(HttpContext http, string role, int id, string name, bool isOwner = false)
    {
        await http.SignOutAsync(AuthSetup.External);
        await http.SignInAsync(role, id, name, isOwner);
    }
}
