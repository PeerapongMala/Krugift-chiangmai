using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public record AddStudentRequest(string StudentCode, string FirstName, string LastName, int No);

public record EditStudentRequest(string FirstName, string LastName, int No);

/// นักเรียนในห้องเรียน · นักเรียนล็อกอินด้วย Google แล้วผูกกับรหัสนักเรียนเอง ครูไม่ต้องแจกรหัสอะไร
public static class StudentEndpoints
{
    public static void MapStudents(this WebApplication app)
    {
        var g = app.MapGroup("/api").RequireAuthorization(AuthSetup.Teacher);

        g.MapGet("/classrooms/{id:int}/students", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (await db.FindClassroom(user, id) is null) return Problems.NotFound("ห้องเรียนนี้");

            return Results.Ok(await db.EnrollmentsOf(user)
                .Where(e => e.ClassroomId == id)
                .OrderBy(e => e.No)
                .Select(e => new
                {
                    e.StudentId,
                    e.No,
                    e.Student.StudentCode,
                    e.Student.FirstName,
                    e.Student.LastName,
                    // ครูจะได้รู้ว่าใครยังไม่เคยเข้าระบบ จะได้ตามได้ถูกคน
                    HasGoogle = e.Student.GoogleSub != null,
                })
                .ToListAsync());
        });

        g.MapPost("/classrooms/{id:int}/students", async (int id, AddStudentRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            var error = Validate.StudentCode(req.StudentCode)
                        ?? Validate.Name(req.FirstName, "นักเรียน")
                        ?? Validate.Name(req.LastName, "สกุลนักเรียน")
                        ?? Validate.No(req.No);
            if (error is not null) return Problems.Invalid(error);

            if (await db.FindClassroom(user, id) is null) return Problems.NotFound("ห้องเรียนนี้");

            if (await db.Enrollments.AnyAsync(e => e.ClassroomId == id && e.No == req.No))
                return Problems.Conflict($"มีนักเรียนเลขที่ {req.No} ในห้องนี้แล้ว");

            // รหัสนักเรียน unique ทั้งโรงเรียนและใช้ข้ามเทอม ถ้ามีอยู่แล้วให้ดึงคนเดิมมาเข้าห้อง
            // ไม่สร้างซ้ำ ไม่งั้นประวัติคะแนนของเด็กคนเดียวจะแตกเป็นหลายคน
            var code = req.StudentCode.Trim();
            var student = await db.Students.FirstOrDefaultAsync(s => s.StudentCode == code);

            if (student is null)
            {
                student = new Student
                {
                    StudentCode = code,
                    FirstName = req.FirstName.Trim(),
                    LastName = req.LastName.Trim(),
                };
                db.Students.Add(student);
                await db.SaveChangesAsync();
            }
            else if (await db.Enrollments.AnyAsync(e => e.ClassroomId == id && e.StudentId == student.Id))
            {
                return Problems.Conflict("นักเรียนรหัสนี้อยู่ในห้องนี้แล้ว");
            }

            db.Enrollments.Add(new Enrollment { ClassroomId = id, StudentId = student.Id, No = req.No });
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                StudentId = student.Id,
                student.StudentCode,
                student.FirstName,
                student.LastName,
                req.No,
                HasGoogle = student.GoogleSub != null,
            });
        });

        g.MapPatch("/students/{id:int}", async (int id, EditStudentRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            var error = Validate.Name(req.FirstName, "นักเรียน")
                        ?? Validate.Name(req.LastName, "สกุลนักเรียน")
                        ?? Validate.No(req.No);
            if (error is not null) return Problems.Invalid(error);

            var enrollment = await db.EnrollmentsOf(user)
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.StudentId == id);
            if (enrollment is null) return Problems.NotFound("นักเรียนคนนี้");

            if (await db.Enrollments.AnyAsync(e =>
                    e.ClassroomId == enrollment.ClassroomId && e.No == req.No && e.StudentId != id))
                return Problems.Conflict($"มีนักเรียนเลขที่ {req.No} ในห้องนี้แล้ว");

            enrollment.Student.FirstName = req.FirstName.Trim();
            enrollment.Student.LastName = req.LastName.Trim();
            enrollment.No = req.No;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapDelete("/classrooms/{classroomId:int}/students/{studentId:int}",
            async (int classroomId, int studentId, ClaimsPrincipal user, AppDbContext db) =>
            {
                var enrollment = await db.EnrollmentsOf(user)
                    .FirstOrDefaultAsync(e => e.ClassroomId == classroomId && e.StudentId == studentId);
                if (enrollment is null) return Problems.NotFound("นักเรียนคนนี้ในห้องนี้");

                // เอาออกจากห้องเท่านั้น ไม่ลบตัวนักเรียน เพราะยังมีคะแนนเทอมอื่นผูกอยู่
                db.Enrollments.Remove(enrollment);
                await db.SaveChangesAsync();
                return Results.NoContent();
            });

        // ยกเลิกการผูกบัญชี Google — ใช้ตอนเด็กผูกผิดบัญชี หรือมีคนอื่นเผลอไปผูกรหัสของเด็กคนนี้
        g.MapPost("/students/{id:int}/unlink", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var enrollment = await db.EnrollmentsOf(user)
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.StudentId == id);
            if (enrollment is null) return Problems.NotFound("นักเรียนคนนี้");

            if (enrollment.Student.GoogleSub is null)
                return Problems.Invalid("นักเรียนคนนี้ยังไม่ได้ผูกบัญชี Google");

            enrollment.Student.GoogleSub = null;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
