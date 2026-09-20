namespace Api.Common;

/// <summary>
/// ปัดคะแนนที่หารแล้วให้เป็นจำนวนเต็ม (ครูขอ เพราะเลข 7.33 อ่านยากสำหรับเด็ก)
/// ปัดครึ่งขึ้น: 7.33 → 7 · 7.5 → 8 · 4.44 → 4
/// </summary>
public static class Rounding
{
    /// <summary>
    /// ปัดเป็นจำนวนเต็ม แต่ห้ามเกินคะแนนเต็มของรายการนั้น
    /// (คะแนนเต็มบางรายการไม่ใช่จำนวนเต็ม เช่น 9.6 ที่ได้จากสูตรในไฟล์ครู ปัด 9.6 ขึ้นเป็น 10 จะเกินคะแนนเต็ม)
    /// </summary>
    public static decimal ToWhole(decimal value, decimal maxScore)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded > maxScore ? Math.Floor(maxScore) : rounded;
    }
}
