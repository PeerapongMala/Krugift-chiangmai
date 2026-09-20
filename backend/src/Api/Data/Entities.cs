using Microsoft.EntityFrameworkCore;

namespace Api.Data;

// เวลาทั้งหมดเก็บเป็น UTC (Npgsql map DateTime Kind=Utc → timestamptz)
// [Comment] ถูกเขียนลง DB ด้วย COMMENT ON ให้เห็นคำอธิบายตอนเปิดดูผ่าน Navicat / Neon console
// แก้ข้อความ comment แล้วต้องสร้าง migration ใหม่ด้วย

/// Owner = จัดการรายชื่อครูได้ และเห็นข้อมูลของครูทุกคน · Teacher = เห็นเฉพาะภาคเรียนของตัวเอง
public enum TeacherRole { Owner, Teacher }

[Comment("ครูที่เข้าระบบได้ด้วย Google · ครูคนแรกมาจาก env TEACHER_EMAILS หลังจากนั้นเพิ่ม/ลบที่หน้า จัดการครู")]
public class Teacher
{
    [Comment("รหัสภายในระบบ")]
    public int Id { get; set; }

    [Comment("อีเมล Google ที่ใช้ล็อกอิน (ตัวพิมพ์เล็ก ห้ามซ้ำ)")]
    public required string Email { get; set; }

    [Comment("ชื่อที่แสดง · ว่างได้ ถ้าว่างระบบแสดงอีเมลแทน")]
    public string Name { get; set; } = "";

    [Comment("สิทธิ์: Owner = ผู้ดูแลระบบ (เห็นข้อมูลครูทุกคน + เพิ่ม/ลบครู) · Teacher = ครู (เห็นเฉพาะภาคเรียนของตัวเอง)")]
    public TeacherRole Role { get; set; } = TeacherRole.Teacher;
}

[Comment("ภาคเรียน เช่น 1/2569 · เป็นของครูหนึ่งคน")]
public class Term
{
    [Comment("รหัสภายในระบบ")]
    public int Id { get; set; }

    [Comment("ครูเจ้าของภาคเรียน (Teachers.Id)")]
    public int TeacherId { get; set; }

    public Teacher Teacher { get; set; } = null!;

    [Comment("ชื่อภาคเรียน เช่น 1/2569 · ครูคนเดียวกันห้ามซ้ำ")]
    public required string Name { get; set; }

    [Comment("เวลาที่สร้าง (UTC)")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// เปิดให้นักเรียนดูคะแนนด่วนโดยไม่ต้องล็อกอิน (ห้อง + เลขที่ + รหัสนักเรียน) · ครูปิดได้รายภาคเรียน
    [Comment("เปิดให้ดูคะแนนด่วนโดยไม่ต้องล็อกอิน (ห้อง + เลขที่ + รหัสนักเรียน) · true = เปิด")]
    public bool PublicScores { get; set; } = true;

    public List<Classroom> Classrooms { get; set; } = [];
}

[Comment("ห้องเรียนในภาคเรียน เช่น ม.2/1 · นักเรียนในห้องดูที่ Enrollments")]
public class Classroom
{
    [Comment("รหัสภายในระบบ")]
    public int Id { get; set; }

    [Comment("ภาคเรียนที่ห้องนี้อยู่ (Terms.Id)")]
    public int TermId { get; set; }

    public Term Term { get; set; } = null!;

    [Comment("ชื่อห้อง เช่น ม.2/1 · ในภาคเรียนเดียวกันห้ามซ้ำ")]
    public required string Name { get; set; }

    public List<Enrollment> Enrollments { get; set; } = [];
    public List<AssessmentItem> Items { get; set; } = [];
}

[Comment("นักเรียนทั้งโรงเรียน · หนึ่งคนมีแถวเดียวใช้ข้ามทุกภาคเรียน (อยู่ห้องไหนดูที่ Enrollments)")]
public class Student
{
    [Comment("รหัสภายในระบบ (ไม่ใช่รหัสนักเรียน)")]
    public int Id { get; set; }

    [Comment("รหัสนักเรียนของโรงเรียน · ห้ามซ้ำ")]
    public required string StudentCode { get; set; }

    [Comment("คำนำหน้า เช่น เด็กหญิง · ว่างได้")]
    public string Title { get; set; } = "";

    [Comment("ชื่อ")]
    public required string FirstName { get; set; }

    [Comment("นามสกุล")]
    public required string LastName { get; set; }

    [Comment("ชื่อเล่น · ว่างได้")]
    public string Nickname { get; set; } = "";

    [Comment("บัญชี Google ที่นักเรียนเชื่อมไว้ (Google subject id) · null = ยังไม่เคยเข้าสู่ระบบ")]
    public string? GoogleSub { get; set; }
}

[Comment("ตารางเชื่อมห้องกับนักเรียน: นักเรียนคนไหนอยู่ห้องไหน และเลขที่เท่าไหร่")]
public class Enrollment
{
    [Comment("ห้องเรียน (Classrooms.Id)")]
    public int ClassroomId { get; set; }

    public Classroom Classroom { get; set; } = null!;

    [Comment("นักเรียน (Students.Id)")]
    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;

    [Comment("เลขที่ในห้อง (1-999)")]
    public int No { get; set; }
}

[Comment("รายการที่ให้คะแนนในห้อง เช่น สอบกลางภาค เต็ม 20")]
public class AssessmentItem
{
    [Comment("รหัสภายในระบบ")]
    public int Id { get; set; }

    [Comment("ห้องเรียน (Classrooms.Id)")]
    public int ClassroomId { get; set; }

    public Classroom Classroom { get; set; } = null!;

    [Comment("ชื่อรายการ · ในห้องเดียวกันห้ามซ้ำ")]
    public required string Name { get; set; }

    [Comment("คะแนนเต็ม (มากกว่า 0)")]
    public decimal MaxScore { get; set; }

    [Comment("ลำดับการแสดง · น้อยขึ้นก่อน")]
    public int SortOrder { get; set; }

    [Comment("true = เห็นเฉพาะครู นักเรียนไม่เห็นและสอบถามไม่ได้ (เช่น คะแนนดิบก่อนคิดเป็นคะแนนเก็บ)")]
    public bool TeacherOnly { get; set; }

    [Comment("ชื่อตามหัวตารางในไฟล์ครู ใช้จับคู่ตอนนำเข้าซ้ำ ครูจึงเปลี่ยนชื่อที่แสดงได้โดยไม่เกิดรายการซ้ำ · ว่าง = สร้างในเว็บเอง")]
    public string SourceKey { get; set; } = "";
}

[Comment("คะแนนของนักเรียนต่อรายการ · แก้ผ่านแอปเท่านั้น เพื่อให้มีประวัติใน ScoreAudits")]
public class Score
{
    [Comment("รายการคะแนน (Items.Id)")]
    public int ItemId { get; set; }

    public AssessmentItem Item { get; set; } = null!;

    [Comment("นักเรียน (Students.Id)")]
    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;

    [Comment("คะแนนที่ได้ · null = ยังไม่ได้กรอก · ห้ามเกินคะแนนเต็ม (ตรวจในแอป)")]
    public decimal? Value { get; set; }
}

// ponytail: ไม่มี FK ตั้งใจให้ประวัติยังอยู่แม้ลบรายการหรือนักเรียนไปแล้ว
[Comment("ประวัติการแก้คะแนนทุกครั้ง · ตั้งใจไม่มี FK ประวัติจะอยู่ต่อแม้ลบรายการหรือนักเรียนไปแล้ว")]
public class ScoreAudit
{
    [Comment("รหัสภายในระบบ")]
    public long Id { get; set; }

    [Comment("รายการที่ถูกแก้ (Items.Id · อาจถูกลบไปแล้ว)")]
    public int ItemId { get; set; }

    [Comment("นักเรียนที่ถูกแก้คะแนน (Students.Id)")]
    public int StudentId { get; set; }

    [Comment("คะแนนก่อนแก้ · null = ว่าง")]
    public decimal? OldValue { get; set; }

    [Comment("คะแนนหลังแก้ · null = ล้างเป็นว่าง")]
    public decimal? NewValue { get; set; }

    [Comment("ครูที่แก้ (Teachers.Id · อาจถูกลบไปแล้ว)")]
    public int TeacherId { get; set; }

    [Comment("เวลาที่แก้ (UTC)")]
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public enum AppealStatus { Open, Answered, Closed }

[Comment("คำถามเรื่องคะแนนจากนักเรียน (หน้าเว็บเรียกว่า สอบถามคะแนน) · มีคำถามที่ยังไม่เสร็จสิ้นได้ครั้งละหนึ่งคำถามต่อรายการ")]
public class Appeal
{
    [Comment("รหัสภายในระบบ")]
    public int Id { get; set; }

    [Comment("รายการคะแนนที่ถาม (Items.Id)")]
    public int ItemId { get; set; }

    public AssessmentItem Item { get; set; } = null!;

    [Comment("นักเรียนที่ถาม (Students.Id)")]
    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;

    [Comment("สถานะ: Open = รอครูตอบ · Answered = ครูตอบแล้ว · Closed = เสร็จสิ้น")]
    public AppealStatus Status { get; set; } = AppealStatus.Open;

    [Comment("ครูยังไม่ได้อ่านข้อความล่าสุด (ใช้แสดง badge ฝั่งครู)")]
    public bool UnreadByTeacher { get; set; } = true;

    [Comment("นักเรียนยังไม่ได้อ่านข้อความล่าสุด (ใช้แสดง badge ฝั่งนักเรียน)")]
    public bool UnreadByStudent { get; set; }

    [Comment("เวลาที่ส่งคำถาม (UTC)")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AppealMessage> Messages { get; set; } = [];
}

[Comment("ข้อความในคำถามเรื่องคะแนน (ทั้งคำถามของนักเรียนและคำตอบของครู)")]
public class AppealMessage
{
    [Comment("รหัสภายในระบบ")]
    public int Id { get; set; }

    [Comment("คำถาม (Appeals.Id)")]
    public int AppealId { get; set; }

    [Comment("true = ครูเขียน · false = นักเรียนเขียน")]
    public bool FromTeacher { get; set; }

    [Comment("ข้อความ (ไม่เกิน 2000 ตัวอักษร)")]
    public required string Body { get; set; }

    [Comment("เวลาที่ส่ง (UTC)")]
    public DateTime At { get; set; } = DateTime.UtcNow;
}
