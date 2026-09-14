using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

namespace Api.Auth;

public static class AuthSetup
{
    public const string External = "External"; // cookie ชั่วคราวหลัง Google login ก่อน claim
    public const string LoginLimit = "login";
    public const string Teacher = "teacher";
    public const string Student = "student";

    public static void AddAppAuth(this WebApplicationBuilder builder)
    {
        var auth = builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "krugift";
                o.Cookie.HttpOnly = true;
                // Lax: กัน cross-site POST (CSRF) และยังให้ redirect กลับจาก Google ได้
                // ponytail: ไม่ใช้ antiforgery token เพราะ API รับแค่ JSON ซึ่งฟอร์มข้ามเว็บส่งมาไม่ได้โดยไม่ผ่าน CORS
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromDays(30);
                o.SlidingExpiration = true;
                o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
            })
            .AddCookie(External, o =>
            {
                o.Cookie.Name = "krugift.ext";
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(15);
            });

        var google = builder.Configuration.GetSection("Google");
        if (!string.IsNullOrEmpty(google["ClientId"]))
            auth.AddGoogle(o =>
            {
                o.ClientId = google["ClientId"]!;
                o.ClientSecret = google["ClientSecret"]!;
                o.SignInScheme = External;
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Teacher, p => p.RequireRole(Teacher))
            .AddPolicy(Student, p => p.RequireRole(Student));

        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(LoginLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // ponytail: เชื่อ proxy ทุกตัว (Render อยู่หน้าเสมอ) ถ้าเรียกตรงจะปลอม IP หลบ rate limit ได้ แต่ยังมี lockout ต่อนักเรียนกันไว้
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });
    }

    public static Task SignInAsync(this HttpContext http, string role, int id, string name) =>
        http.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(
            [new(ClaimTypes.Role, role), new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Name, name)],
            CookieAuthenticationDefaults.AuthenticationScheme)));

    public static int UserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
