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
    public const string OwnerPolicy = "owner";
    /// claim บอกว่าครูคนนี้เป็นเจ้าของระบบ เก็บใน cookie จะได้ไม่ต้อง query DB ทุก request
    /// หมายเหตุ: claim ค้างได้ถึง 30 วัน endpoint ที่อันตรายจึงต้องเช็คยศจาก DB ซ้ำอีกชั้น
    public const string OwnerClaim = "krugift:owner";

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
                // ค่า default คือ /signin-google ซึ่งอยู่นอก /api ทำให้ตอน dev ที่เปิดผ่าน Vite (5173)
                // Google จะ redirect กลับมาที่ 5173/signin-google ซึ่ง Vite ไม่ได้ proxy ไปหา API -> 404
                // ย้ายมาไว้ใต้ /api ให้ proxy ส่งต่อได้ และบน production (origin เดียวกัน) ก็ใช้ path นี้เหมือนกัน
                o.CallbackPath = "/api/signin-google";
                // บังคับให้ Google ถามทุกครั้งว่าจะใช้บัญชีไหน
                // ค่า default จะหยิบบัญชีที่ค้างอยู่ในเบราว์เซอร์มาใช้เงียบ ๆ ซึ่งอันตรายมาก
                // เพราะเด็กใช้ iPad/คอมร่วมกันที่โรงเรียน คนถัดไปจะเข้าเป็นบัญชีคนก่อนโดยไม่รู้ตัว
                o.Events.OnRedirectToAuthorizationEndpoint = ctx =>
                {
                    ctx.Response.Redirect(ctx.RedirectUri + "&prompt=select_account");
                    return Task.CompletedTask;
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Teacher, p => p.RequireRole(Teacher))
            .AddPolicy(Student, p => p.RequireRole(Student))
            .AddPolicy(OwnerPolicy, p => p.RequireRole(Teacher).RequireClaim(OwnerClaim, "true"));

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
            // ตอน dev เว็บอยู่ที่ Vite (5173) แต่ API อยู่ 5080 ถ้าไม่เชื่อ X-Forwarded-Host
            // redirect_uri ที่ส่งให้ Google จะเป็น 5080 แล้วหลังล็อกอินเสร็จจะเด้งไปพอร์ตที่ไม่มีหน้าเว็บ
            // เปิดเฉพาะ Development เพราะการเชื่อ Host จากภายนอกเสี่ยง host header injection
            if (builder.Environment.IsDevelopment())
                o.ForwardedHeaders |= ForwardedHeaders.XForwardedHost;
            // ponytail: เชื่อ proxy ทุกตัว (Render อยู่หน้าเสมอ) ถ้าเรียกตรงจะปลอม IP หลบ rate limit ได้ แต่ยังมี lockout ต่อนักเรียนกันไว้
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });
    }

    public static Task SignInAsync(this HttpContext http, string role, int id, string name, bool isOwner = false)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.Role, role),
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, name),
        ];
        if (isOwner) claims.Add(new Claim(OwnerClaim, "true"));

        return http.SignInAsync(new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    }

    public static bool IsOwner(this ClaimsPrincipal user) => user.HasClaim(OwnerClaim, "true");

    public static int UserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
