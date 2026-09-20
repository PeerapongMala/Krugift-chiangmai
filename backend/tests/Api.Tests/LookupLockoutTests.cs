using Api.Common;

namespace Api.Tests;

public class LookupLockoutTests
{
    static readonly DateTime Now = new(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void กรอกถูกกี่ครั้งก็ไม่โดนบล็อก()
    {
        var lockout = new LookupLockout();

        // ทั้งห้องออกเน็ต IP เดียวกันแล้วเปิดดูคะแนนพร้อมกัน 45 คน ต้องผ่านหมด
        for (var i = 0; i < 45; i++) Assert.False(lockout.IsLocked("1.1.1.1", Now));
    }

    [Fact]
    public void กรอกผิดครบโควตาแล้วโดนบล็อก()
    {
        var lockout = new LookupLockout();

        for (var i = 0; i < LookupLockout.MaxFailures - 1; i++) lockout.RecordFailure("1.1.1.1", Now);
        Assert.False(lockout.IsLocked("1.1.1.1", Now));

        lockout.RecordFailure("1.1.1.1", Now);
        Assert.True(lockout.IsLocked("1.1.1.1", Now));
    }

    [Fact]
    public void บล็อกเฉพาะไอพีที่กรอกผิด_ไม่ลามไปคนอื่น()
    {
        var lockout = new LookupLockout();

        for (var i = 0; i < LookupLockout.MaxFailures; i++) lockout.RecordFailure("1.1.1.1", Now);

        Assert.True(lockout.IsLocked("1.1.1.1", Now));
        Assert.False(lockout.IsLocked("2.2.2.2", Now));
    }

    [Fact]
    public void พ้นช่วงเวลาแล้วเริ่มนับใหม่()
    {
        var lockout = new LookupLockout();

        for (var i = 0; i < LookupLockout.MaxFailures; i++) lockout.RecordFailure("1.1.1.1", Now);

        Assert.True(lockout.IsLocked("1.1.1.1", Now));
        Assert.False(lockout.IsLocked("1.1.1.1", Now + LookupLockout.Window));
    }

    [Fact]
    public void กรอกถูกแล้วล้างยอดที่เคยผิด()
    {
        var lockout = new LookupLockout();

        for (var i = 0; i < LookupLockout.MaxFailures - 1; i++) lockout.RecordFailure("1.1.1.1", Now);
        lockout.Clear("1.1.1.1");

        // เริ่มนับใหม่ ผิดอีกครั้งเดียวต้องยังไม่โดนบล็อก
        lockout.RecordFailure("1.1.1.1", Now);
        Assert.False(lockout.IsLocked("1.1.1.1", Now));
    }
}
