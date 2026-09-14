using System.Text.Json.Serialization;
using Api.Auth;
using Api.Data;
using Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddProblemDetails();
// enum ใน JSON ใช้ชื่อ ไม่ใช่ตัวเลข — ฝั่งเว็บจะได้ส่ง {"role":"Owner"} และอ่านค่ากลับมาแบบเดียวกัน
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.AddAppAuth();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await SeedFirstOwner(db, app.Configuration["TEACHER_EMAILS"]);
}

app.UseForwardedHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapAuth();
app.MapStaff();
app.MapTerms();

// หน้าเว็บที่ build แล้ว (frontend/dist) ถูก copy มาไว้ใน wwwroot ตอน build Docker
app.MapFallbackToFile("index.html");

app.Run();

// สร้างครูจาก env TEACHER_EMAILS="a@gmail.com,b@gmail.com" — คนแรกในลิสต์เป็นเจ้าของ
// ใช้ตอน bootstrap เท่านั้น ถ้ามีครูในตารางแล้วจะไม่แตะอะไรเลย
// เหตุผล: หลัง bootstrap เจ้าของจัดการรายชื่อครูผ่าน /api/staff ถ้ายัง seed ทุกครั้งที่ start
// ครูที่เจ้าของลบไปแล้วจะฟื้นกลับมาเองตอน restart
static async Task SeedFirstOwner(AppDbContext db, string? emails)
{
    if (!await db.Teachers.AnyAsync())
    {
        var list = (emails ?? "")
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToList();
        if (list.Count == 0) return;

        db.Teachers.AddRange(list.Select((email, i) => new Teacher
        {
            Email = email,
            Role = i == 0 ? TeacherRole.Owner : TeacherRole.Teacher,
        }));
        await db.SaveChangesAsync();
        return;
    }

    // กันระบบล็อกตัวเอง: มีครูอยู่แต่ไม่เหลือเจ้าของเลย (เช่นข้อมูลถูกแก้จากนอกแอป)
    if (!await db.Teachers.AnyAsync(t => t.Role == TeacherRole.Owner))
    {
        var first = await db.Teachers.OrderBy(t => t.Id).FirstAsync();
        first.Role = TeacherRole.Owner;
        await db.SaveChangesAsync();
    }
}

public partial class Program;
