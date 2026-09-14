using Api.Data;

namespace Api.Common;

/// <summary>
/// ทางเดียวที่อนุญาตให้แก้ค่า Score.Value
/// กฎโปรเจกต์: แก้คะแนนทุกครั้งต้องเขียน ScoreAudit — รวมไว้ที่นี่จะได้ลืมไม่ได้
/// </summary>
public static class ScoreWriter
{
    /// <summary>
    /// เขียนคะแนนใหม่ + บันทึก audit ถ้าค่าเปลี่ยนจริง
    /// ไม่เรียก SaveChanges ให้ ผู้เรียกรวบบันทึกทีเดียวตอนจบ (กรอกทั้งตารางจะได้เป็น transaction เดียว)
    /// </summary>
    /// <returns>true ถ้ามีการเปลี่ยนแปลง</returns>
    public static bool Set(AppDbContext db, Score score, decimal? newValue, int teacherId, DateTime now)
    {
        if (score.Value == newValue) return false;

        db.ScoreAudits.Add(new ScoreAudit
        {
            ItemId = score.ItemId,
            StudentId = score.StudentId,
            OldValue = score.Value,
            NewValue = newValue,
            TeacherId = teacherId,
            At = now,
        });
        score.Value = newValue;
        return true;
    }

    /// หา Score เดิม ถ้ายังไม่มีให้สร้างแถวใหม่ (คะแนนว่าง) — ตารางคะแนนเริ่มจากไม่มีแถว
    public static Score GetOrCreate(AppDbContext db, int itemId, int studentId)
    {
        var existing = db.Scores.Local.FirstOrDefault(s => s.ItemId == itemId && s.StudentId == studentId)
                       ?? db.Scores.Find(itemId, studentId);
        if (existing is not null) return existing;

        var created = new Score { ItemId = itemId, StudentId = studentId };
        db.Scores.Add(created);
        return created;
    }
}
