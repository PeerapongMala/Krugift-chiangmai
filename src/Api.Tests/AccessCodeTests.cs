using Api.Auth;
using Api.Data;

namespace Api.Tests;

public class AccessCodeTests
{
    static readonly DateTime Now = new(2026, 9, 14, 8, 0, 0, DateTimeKind.Utc);

    static Student NewStudent(string code) => new()
    {
        StudentCode = "12345", FirstName = "ก", LastName = "ข", CodeHash = AccessCode.Hash(code),
    };

    [Fact]
    public void New_code_is_8_chars_without_confusing_letters()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = AccessCode.New();
            Assert.Equal(8, code.Length);
            Assert.DoesNotContain(code, c => "0O1IL".Contains(c));
        }
    }

    [Fact]
    public void Hash_verifies_ignoring_case_and_spaces_but_rejects_wrong_code()
    {
        var hash = AccessCode.Hash("ABCD2345");
        Assert.True(AccessCode.Matches(hash, " abcd2345 "));
        Assert.False(AccessCode.Matches(hash, "ABCD2346"));
        Assert.DoesNotContain("ABCD2345", hash);
    }

    [Fact]
    public void Locks_after_max_failed_attempts_even_for_correct_code()
    {
        var s = NewStudent("ABCD2345");
        for (var i = 0; i < AccessCode.MaxAttempts; i++)
            Assert.False(AccessCode.TryVerify(s, "WRONG000", Now));

        Assert.True(AccessCode.IsLocked(s, Now));
        Assert.False(AccessCode.TryVerify(s, "ABCD2345", Now.AddMinutes(14)));
        Assert.True(AccessCode.TryVerify(s, "ABCD2345", Now + AccessCode.LockDuration + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Success_resets_failed_attempts()
    {
        var s = NewStudent("ABCD2345");
        AccessCode.TryVerify(s, "WRONG000", Now);
        AccessCode.TryVerify(s, "WRONG000", Now);
        Assert.True(AccessCode.TryVerify(s, "ABCD2345", Now));
        Assert.Equal(0, s.FailedAttempts);
        Assert.Null(s.LockedUntil);
    }
}
