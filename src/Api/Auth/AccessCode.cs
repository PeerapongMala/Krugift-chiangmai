using System.Security.Cryptography;
using Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Api.Auth;

/// รหัสส่วนตัวของนักเรียน: สุ่ม, เก็บเป็น hash, ผิดครบ MaxAttempts ครั้งล็อก LockDuration
public static class AccessCode
{
    // ตัดตัวที่ชวนสับสนออก: 0 O 1 I L
    const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int MaxAttempts = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    static readonly PasswordHasher<object> Hasher = new();

    public static string New() => RandomNumberGenerator.GetString(Alphabet, 8);

    public static string Hash(string code) => Hasher.HashPassword(null!, Normalize(code));

    public static bool Matches(string hash, string code) =>
        Hasher.VerifyHashedPassword(null!, hash, Normalize(code)) != PasswordVerificationResult.Failed;

    static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static bool IsLocked(Student s, DateTime now) => s.LockedUntil > now;

    /// เช็ครหัสพร้อมนับครั้งที่ผิด ต้องเรียก SaveChanges ต่อทุกครั้ง
    public static bool TryVerify(Student s, string code, DateTime now)
    {
        if (IsLocked(s, now)) return false;

        if (Matches(s.CodeHash, code))
        {
            s.FailedAttempts = 0;
            s.LockedUntil = null;
            return true;
        }

        if (++s.FailedAttempts >= MaxAttempts)
        {
            s.FailedAttempts = 0;
            s.LockedUntil = now + LockDuration;
        }
        return false;
    }
}
