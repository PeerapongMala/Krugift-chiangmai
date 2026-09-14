namespace Api.Common;

/// ค่ากลางของระบบ — แก้ที่นี่ที่เดียว ห้ามฝังตัวเลขไว้ตาม endpoint
public static class Limits
{
    /// ตรงกับ HaveMaxLength(200) ใน AppDbContext.ConfigureConventions
    public const int NameLength = 200;

    /// ตรงกับ HasMaxLength(2000) ของ AppealMessage.Body
    public const int MessageLength = 2000;

    /// เพดานของ decimal(6,2) ที่ตั้งไว้ใน ConfigureConventions
    public const decimal ScoreCeiling = 9999.99m;

    /// กันไฟล์ Excel ที่ใหญ่ผิดปกติ (ห้องนึงมีไม่เกิน ~50 คน เผื่อไว้เยอะแล้ว)
    public const int ImportMaxRows = 2000;
}
