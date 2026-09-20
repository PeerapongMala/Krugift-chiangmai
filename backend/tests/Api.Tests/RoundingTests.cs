using Api.Common;

namespace Api.Tests;

public class RoundingTests
{
    [Theory]
    // ตัวอย่างที่ตกลงกับครูไว้ (คะแนนจริงจากไฟล์)
    [InlineData(7.33, 10, 7)]
    [InlineData(7.5, 10, 8)]
    [InlineData(4.44, 10, 4)]
    [InlineData(9.6, 10, 10)]
    [InlineData(0, 10, 0)]
    [InlineData(10, 10, 10)]
    public void ปัดครึ่งขึ้นเป็นจำนวนเต็ม(decimal value, decimal maxScore, decimal expected) =>
        Assert.Equal(expected, Rounding.ToWhole(value, maxScore));

    [Theory]
    // คะแนนเต็มไม่ใช่จำนวนเต็ม (ได้จากสูตรในไฟล์ครูที่คำนวณเกินหัวตาราง) ปัดขึ้นแล้วจะเกินเต็ม จึงต้องลงมา
    [InlineData(9.6, 9.6, 9)]
    [InlineData(0.5, 0.5, 0)]
    [InlineData(9.2, 9.6, 9)]
    public void ปัดแล้วต้องไม่เกินคะแนนเต็ม(decimal value, decimal maxScore, decimal expected) =>
        Assert.Equal(expected, Rounding.ToWhole(value, maxScore));

    [Fact]
    public void ค่าที่เป็นจำนวนเต็มอยู่แล้วไม่เปลี่ยน()
    {
        foreach (var v in new decimal[] { 0, 1, 7, 20, 100 })
            Assert.Equal(v, Rounding.ToWhole(v, 100));
    }
}
