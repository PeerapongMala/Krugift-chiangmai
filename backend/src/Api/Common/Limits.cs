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

    /// ขนาดไฟล์ import สูงสุด (ตกลงไว้ใน PLAN) · ไฟล์ของห้องหนึ่งจริง ๆ ไม่ถึง 100 KB
    public const int ImportMaxBytes = 2 * 1024 * 1024;

    /// ขนาดรวมหลังแตก zip · .xlsx คือ zip กันไฟล์เล็กที่แตกออกมาเป็นหลาย GB (zip bomb)
    public const long ImportMaxUnzippedBytes = 50 * 1024 * 1024;

    /// จำนวนคอลัมน์รายการคะแนนในไฟล์เดียว
    public const int ImportMaxItems = 100;

    /// ส่งจุดผิดกลับไปแสดงสูงสุดกี่จุด ที่เหลือบอกแค่จำนวน
    public const int ImportMaxErrors = 200;

    /// รหัสนักเรียนของโรงเรียน เช่น 12345
    public const int StudentCodeLength = 20;

    /// เลขที่ในห้อง
    public const int MaxStudentNo = 999;
}
