using Api.Auth;
using Api.Data;
using Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddProblemDetails();
builder.AddAppAuth();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await SeedTeachers(db, app.Configuration["TEACHER_EMAILS"]);
}

app.UseForwardedHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapAuth();

// หน้าเว็บที่ build แล้ว (web/dist) ถูก copy มาไว้ใน wwwroot ตอน build Docker
app.MapFallbackToFile("index.html");

app.Run();

// ครูคนแรกมาจาก env TEACHER_EMAILS="a@gmail.com,b@gmail.com" เพิ่มอย่างเดียว ไม่ลบ
static async Task SeedTeachers(AppDbContext db, string? emails)
{
    var list = (emails ?? "")
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(e => e.ToLowerInvariant())
        .Distinct();
    foreach (var email in list)
        if (!await db.Teachers.AnyAsync(t => t.Email == email))
            db.Teachers.Add(new Teacher { Email = email });
    await db.SaveChangesAsync();
}

public partial class Program;
