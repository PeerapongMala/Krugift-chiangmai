namespace Api.Data;

// เวลาทั้งหมดเก็บเป็น UTC (Npgsql map DateTime Kind=Utc → timestamptz)

/// Owner = จัดการรายชื่อครูได้ และเห็นข้อมูลของครูทุกคน · Teacher = เห็นเฉพาะภาคเรียนของตัวเอง
public enum TeacherRole { Owner, Teacher }

public class Teacher
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public string Name { get; set; } = "";
    public TeacherRole Role { get; set; } = TeacherRole.Teacher;
}

public class Term
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public required string Name { get; set; } // "1/2569"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Classroom> Classrooms { get; set; } = [];
}

public class Classroom
{
    public int Id { get; set; }
    public int TermId { get; set; }
    public Term Term { get; set; } = null!;
    public required string Name { get; set; } // "ม.2/1"
    public List<Enrollment> Enrollments { get; set; } = [];
    public List<AssessmentItem> Items { get; set; } = [];
}

public class Student
{
    public int Id { get; set; }
    public required string StudentCode { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? GoogleSub { get; set; }
}

public class Enrollment
{
    public int ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int No { get; set; } // เลขที่ในห้อง
}

public class AssessmentItem
{
    public int Id { get; set; }
    public int ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
    public required string Name { get; set; }
    public decimal MaxScore { get; set; }
    public int SortOrder { get; set; }
}

public class Score
{
    public int ItemId { get; set; }
    public AssessmentItem Item { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public decimal? Value { get; set; }
}

// ponytail: ไม่มี FK ตั้งใจให้ประวัติยังอยู่แม้ลบรายการหรือนักเรียนไปแล้ว
public class ScoreAudit
{
    public long Id { get; set; }
    public int ItemId { get; set; }
    public int StudentId { get; set; }
    public decimal? OldValue { get; set; }
    public decimal? NewValue { get; set; }
    public int TeacherId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public enum AppealStatus { Open, Answered, Closed }

public class Appeal
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public AssessmentItem Item { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public AppealStatus Status { get; set; } = AppealStatus.Open;
    public bool UnreadByTeacher { get; set; } = true;
    public bool UnreadByStudent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<AppealMessage> Messages { get; set; } = [];
}

public class AppealMessage
{
    public int Id { get; set; }
    public int AppealId { get; set; }
    public bool FromTeacher { get; set; }
    public required string Body { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
