namespace Api.Common;

/// ตรวจ input ที่ขอบระบบ · คืน null = ผ่าน, คืน string = ข้อความไทยที่เอาไปโชว์ได้เลย
/// แยกจาก Problems เพราะเป็นฟังก์ชันบริสุทธิ์ เทสต์ได้โดยไม่ต้องมี HTTP
public static class Validate
{
    public static string? Name(string? value, string what) => value?.Trim() switch
    {
        null or "" => $"กรุณากรอกชื่อ{what}",
        { Length: > Limits.NameLength } => $"ชื่อ{what}ยาวเกิน {Limits.NameLength} ตัวอักษร",
        _ => null,
    };

    public static string? Message(string? value) => value?.Trim() switch
    {
        null or "" => "กรุณากรอกข้อความ",
        { Length: > Limits.MessageLength } => $"ข้อความยาวเกิน {Limits.MessageLength} ตัวอักษร",
        _ => null,
    };

    /// คะแนนเต็มของรายการประเมิน
    public static string? MaxScore(decimal value) => value switch
    {
        <= 0 => "คะแนนเต็มต้องมากกว่า 0",
        > Limits.ScoreCeiling => $"คะแนนเต็มต้องไม่เกิน {Limits.ScoreCeiling:0.##}",
        _ => null,
    };

    /// คะแนนที่กรอก · null = ยังไม่ให้คะแนน ถือว่าผ่าน
    public static string? Score(decimal? value, decimal maxScore) => value switch
    {
        null => null,
        < 0 => "คะแนนติดลบไม่ได้",
        _ when value > maxScore => $"คะแนนเกินคะแนนเต็ม ({maxScore:0.##})",
        _ => null,
    };
}
