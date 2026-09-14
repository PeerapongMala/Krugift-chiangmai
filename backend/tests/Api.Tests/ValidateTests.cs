using Api.Common;

namespace Api.Tests;

public class ValidateTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_ว่าง_ต้องไม่ผ่าน(string? value) =>
        Assert.Equal("กรุณากรอกชื่อเทอม", Validate.Name(value, "เทอม"));

    [Fact]
    public void Name_ยาวเกิน_ต้องไม่ผ่าน() =>
        Assert.NotNull(Validate.Name(new string('ก', Limits.NameLength + 1), "ห้องเรียน"));

    [Fact]
    public void Name_ยาวพอดีขอบ_ต้องผ่าน() =>
        Assert.Null(Validate.Name(new string('ก', Limits.NameLength), "ห้องเรียน"));

    [Fact]
    public void Name_มีช่องว่างหน้าหลัง_ตัดแล้วต้องผ่าน() =>
        Assert.Null(Validate.Name("  ม.2/1  ", "ห้องเรียน"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Email_ว่าง_ต้องไม่ผ่าน(string? value) =>
        Assert.Equal("กรุณากรอกอีเมล", Validate.Email(value));

    [Theory]
    [InlineData("ไม่ใช่อีเมล")]
    [InlineData("a@")]
    [InlineData("@b.com")]
    public void Email_รูปแบบผิด_ต้องไม่ผ่าน(string value) =>
        Assert.Equal("รูปแบบอีเมลไม่ถูกต้อง", Validate.Email(value));

    [Theory]
    [InlineData("teacher@gmail.com")]
    [InlineData("  teacher@gmail.com  ")]
    public void Email_ถูกต้อง_ต้องผ่าน(string value) =>
        Assert.Null(Validate.Email(value));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxScore_ไม่เป็นบวก_ต้องไม่ผ่าน(double value) =>
        Assert.Equal("คะแนนเต็มต้องมากกว่า 0", Validate.MaxScore((decimal)value));

    [Fact]
    public void MaxScore_เกินเพดาน_ต้องไม่ผ่าน() =>
        Assert.NotNull(Validate.MaxScore(Limits.ScoreCeiling + 0.01m));

    [Fact]
    public void Score_ว่าง_ถือว่ายังไม่ให้คะแนน_ต้องผ่าน() =>
        Assert.Null(Validate.Score(null, 20m));

    [Fact]
    public void Score_เท่ากับคะแนนเต็ม_ต้องผ่าน() =>
        Assert.Null(Validate.Score(20m, 20m));

    [Fact]
    public void Score_เกินคะแนนเต็ม_ต้องไม่ผ่าน() =>
        Assert.Equal("คะแนนเกินคะแนนเต็ม (20)", Validate.Score(20.01m, 20m));

    [Fact]
    public void Score_ติดลบ_ต้องไม่ผ่าน() =>
        Assert.Equal("คะแนนติดลบไม่ได้", Validate.Score(-0.5m, 20m));
}
