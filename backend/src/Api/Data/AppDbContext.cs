using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<AssessmentItem> Items => Set<AssessmentItem>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<ScoreAudit> ScoreAudits => Set<ScoreAudit>();
    public DbSet<Appeal> Appeals => Set<Appeal>();
    public DbSet<AppealMessage> AppealMessages => Set<AppealMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(6, 2);
        b.Properties<string>().HaveMaxLength(200);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Teacher>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Term>().HasIndex(x => new { x.TeacherId, x.Name }).IsUnique();

        b.Entity<Classroom>().HasIndex(x => new { x.TermId, x.Name }).IsUnique();

        b.Entity<Student>(e =>
        {
            e.HasIndex(x => x.StudentCode).IsUnique();
            e.HasIndex(x => x.GoogleSub).IsUnique();
        });

        b.Entity<Enrollment>().HasKey(x => new { x.ClassroomId, x.StudentId });

        b.Entity<AssessmentItem>(e =>
        {
            e.HasIndex(x => new { x.ClassroomId, x.Name }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("CK_Item_MaxScore", "\"MaxScore\" > 0"));
        });

        // ไม่เกินคะแนนเต็ม เช็คในโค้ด เพราะ check constraint ข้ามตารางไม่ได้
        b.Entity<Score>(e =>
        {
            e.HasKey(x => new { x.ItemId, x.StudentId });
            e.ToTable(t => t.HasCheckConstraint("CK_Score_Value", "\"Value\" IS NULL OR \"Value\" >= 0"));
        });

        b.Entity<ScoreAudit>().HasIndex(x => new { x.ItemId, x.StudentId });

        b.Entity<Appeal>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.StudentId);
            e.HasIndex(x => x.UnreadByTeacher);
        });

        b.Entity<AppealMessage>().Property(x => x.Body).HasMaxLength(2000);
    }
}
