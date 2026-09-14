using System.Security.Claims;
using Api.Auth;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Common;

/// <summary>
/// จุดเริ่มต้นของ "ทุก" query ฝั่งครู
/// ตัวช่วยพวกนี้ผูก TeacherId จาก cookie เข้าไปใน WHERE ให้เรียบร้อย ครูคนอื่นจึงดึงข้อมูลข้ามกันไม่ได้
/// อย่าเรียก db.Terms / db.Classrooms ตรง ๆ ใน endpoint ของครู มิฉะนั้นจะเปิดช่อง IDOR
/// </summary>
public static class TeacherScope
{
    public static IQueryable<Term> TermsOf(this AppDbContext db, ClaimsPrincipal user) =>
        db.Terms.Where(t => t.TeacherId == user.UserId());

    public static IQueryable<Classroom> ClassroomsOf(this AppDbContext db, ClaimsPrincipal user) =>
        db.Classrooms.Where(c => c.Term.TeacherId == user.UserId());

    public static IQueryable<AssessmentItem> ItemsOf(this AppDbContext db, ClaimsPrincipal user) =>
        db.Items.Where(i => i.Classroom.Term.TeacherId == user.UserId());

    public static IQueryable<Enrollment> EnrollmentsOf(this AppDbContext db, ClaimsPrincipal user) =>
        db.Enrollments.Where(e => e.Classroom.Term.TeacherId == user.UserId());

    public static IQueryable<Score> ScoresOf(this AppDbContext db, ClaimsPrincipal user) =>
        db.Scores.Where(s => s.Item.Classroom.Term.TeacherId == user.UserId());

    public static IQueryable<Appeal> AppealsOf(this AppDbContext db, ClaimsPrincipal user) =>
        db.Appeals.Where(a => a.Item.Classroom.Term.TeacherId == user.UserId());

    // หาทีละตัวพร้อมเช็คสิทธิ์ในคำสั่งเดียว — คืน null ทั้งกรณี "ไม่มี" และ "ไม่ใช่ของครูคนนี้"
    // ตั้งใจไม่แยก 404/403 เพื่อไม่ให้เดาได้ว่ามี id นี้อยู่จริงไหม
    public static Task<Term?> FindTerm(this AppDbContext db, ClaimsPrincipal user, int id) =>
        db.TermsOf(user).FirstOrDefaultAsync(t => t.Id == id);

    public static Task<Classroom?> FindClassroom(this AppDbContext db, ClaimsPrincipal user, int id) =>
        db.ClassroomsOf(user).FirstOrDefaultAsync(c => c.Id == id);

    public static Task<AssessmentItem?> FindItem(this AppDbContext db, ClaimsPrincipal user, int id) =>
        db.ItemsOf(user).FirstOrDefaultAsync(i => i.Id == id);

    public static Task<Appeal?> FindAppeal(this AppDbContext db, ClaimsPrincipal user, int id) =>
        db.AppealsOf(user).FirstOrDefaultAsync(a => a.Id == id);
}
