using System.Net.Mail;

namespace Api.Common;

/// ตรวจ input ที่ขอบระบบ · คืน null = ผ่าน, คืน string = ข้อความไทยที่เอาไปโชว์ได้เลย
/// แยกจาก Problems เพราะเป็นฟังก์ชันบริสุทธิ์ เทสต์ได้โดยไม่ต้องมี HTTP
public static class Validate
{
    public static string? Name(string? value, string what) => value?.Trim() switch
    {
        null or "" => $"กรุณากรอก{what}",
        { Length: > Limits.NameLength } => $"{what}ยาวเกิน {Limits.NameLength} ตัวอักษร",
        _ => null,
    };

    public static string? Email(string? value)
    {
        var email = value?.Trim();
        if (string.IsNullOrEmpty(email)) return "กรุณากรอกอีเมล";
        if (email.Length > Limits.NameLength) return $"อีเมลยาวเกิน {Limits.NameLength} ตัวอักษร";
        return MailAddress.TryCreate(email, out _) ? null : "รูปแบบอีเมลไม่ถูกต้อง";
    }

    /// รหัสนักเรียนของโรงเรียน · เป็นตัวชี้ว่าเป็นใคร ไม่ใช่ความลับ ตัวยืนยันตัวตนจริงคือบัญชี Google
    public static string? StudentCode(string? value)
    {
        var code = value?.Trim();
        if (string.IsNullOrEmpty(code)) return "กรุณากรอกรหัสนักเรียน";
        if (code.Length > Limits.StudentCodeLength) return $"รหัสนักเรียนยาวเกิน {Limits.StudentCodeLength} ตัวอักษร";
        return code.All(char.IsLetterOrDigit) ? null : "รหัสนักเรียนใช้ได้เฉพาะตัวเลขและตัวอักษร";
    }

    /// เลขที่ในห้อง
    public static string? No(int value) =>
        value is < 1 or > Limits.MaxStudentNo ? $"เลขที่ต้องอยู่ระหว่าง 1 ถึง {Limits.MaxStudentNo}" : null;

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
        // DB เก็บ decimal(6,2) ถ้าไม่ดักตรงนี้ 10.555 จะถูกปัดเงียบ ๆ เป็น 10.56
        _ when value != Math.Round(value, 2) => "คะแนนเต็มมีทศนิยมได้ไม่เกิน 2 ตำแหน่ง",
        _ => null,
    };

    /// คะแนนที่กรอก · null = ยังไม่ให้คะแนน ถือว่าผ่าน
    public static string? Score(decimal? value, decimal maxScore) => value switch
    {
        null => null,
        < 0 => "คะแนนติดลบไม่ได้",
        _ when value > maxScore => $"คะแนนเกินคะแนนเต็ม ({maxScore:0.##})",
        // DB เก็บ decimal(6,2) ถ้าไม่ดักตรงนี้ 18.555 จะถูกปัดเงียบ ๆ เป็น 18.56 (ใช้ทั้งกรอกเองและนำเข้า Excel)
        _ when value != Math.Round(value.Value, 2) => "คะแนนมีทศนิยมได้ไม่เกิน 2 ตำแหน่ง",
        _ => null,
    };
}
