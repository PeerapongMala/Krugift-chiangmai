using Api.Import;
using static Api.Tests.SheetBuilder;

namespace Api.Tests;

public class ScoreSheetParserTests
{
    static readonly object?[] Header = ["เลขที่", "รหัสนักเรียน", "ชื่อ", "นามสกุล", "สอบกลางภาค (20)"];

    /// ห้องมี "สอบกลางภาค" เต็ม 20 · 90001 เลขที่ 1 ได้ 18 · 90002 เลขที่ 2 ยังไม่มีคะแนน
    static ScoreSnapshot Room() => new(
        [new ExistingItem(1, "สอบกลางภาค", 20m)],
        [new ExistingEnrollment(10, 1, "90001", "เอ", "หนึ่ง"), new ExistingEnrollment(11, 2, "90002", "บี", "สอง")],
        new Dictionary<(int, int), decimal?> { [(1, 10)] = 18m });

    static ImportError SingleError(ImportResult<ScorePlan> result)
    {
        Assert.Null(result.Plan);
        return Assert.Single(result.Errors);
    }

    static ImportResult<ScorePlan> ParseRow(object? score) =>
        ScoreSheetParser.Parse(Sheet(Header, [2, "90002", "บี", "สอง", score]), Room());

    [Fact]
    public void กรอกใหม่และแก้ของเดิม_บอกจำนวนที่ทับของเดิม()
    {
        var plan = ScoreSheetParser.Parse(Sheet(Header, [1, "90001", "เอ", "หนึ่ง", 19], [2, "90002", "บี", "สอง", 15]), Room()).Plan!;

        Assert.Equal(2, plan.Scores.Count);
        Assert.Contains("ในนั้นเป็นการแก้คะแนนที่มีอยู่แล้ว 1 ช่อง", plan.Summary);
        Assert.Contains(plan.Changes, c => c.Detail == "18 → 19");
        Assert.Contains(plan.Changes, c => c.Detail == "ว่าง → 15");
    }

    [Fact]
    public void ช่องว่างและคะแนนเท่าเดิม_ไม่นับเป็นการเปลี่ยน()
    {
        var plan = ScoreSheetParser.Parse(Sheet(Header, [1, "90001", "เอ", "หนึ่ง", 18], [2, "90002", "บี", "สอง", null]), Room()).Plan!;

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void หัวคอลัมน์บอกคะแนนเต็ม_สร้างรายการใหม่ได้()
    {
        var plan = ScoreSheetParser.Parse(Sheet([.. Header, "งานกลุ่ม (10)"], [2, "90002", "บี", "สอง", null, 9.5]), Room()).Plan!;

        Assert.Equal(new NewItem("งานกลุ่ม", 10m), Assert.Single(plan.NewItems));
        var change = Assert.Single(plan.Scores);
        Assert.Null(change.ItemId);
        Assert.Equal(9.5m, change.NewValue);
    }

    [Fact]
    public void รายการเดิม_ไม่ต้องเขียนคะแนนเต็มก็ได้() =>
        Assert.True(ScoreSheetParser.Parse(Sheet(["รหัสนักเรียน", "สอบกลางภาค"], ["90002", 10]), Room()).IsValid);

    [Fact]
    public void รายการใหม่ไม่บอกคะแนนเต็ม_ไม่ผ่าน() =>
        Assert.Contains("\"งานกลุ่ม (10)\"",
            SingleError(ScoreSheetParser.Parse(Sheet(["รหัสนักเรียน", "งานกลุ่ม"], ["90002", 10]), Room())).Message);

    [Fact]
    public void คะแนนเต็มในไฟล์ไม่ตรงกับในระบบ_ไม่ผ่าน()
    {
        var error = SingleError(ScoreSheetParser.Parse(Sheet(["รหัสนักเรียน", "สอบกลางภาค (30)"], ["90002", 10]), Room()));

        Assert.Equal("\"สอบกลางภาค\" ในไฟล์เต็ม 30 แต่ในระบบเต็ม 20 ถ้าจะเปลี่ยนคะแนนเต็มให้แก้ที่แท็บรายการคะแนนก่อน", error.Message);
    }

    [Theory]
    [InlineData("งานกลุ่ม (สิบ)", "ต้องเป็นตัวเลข")]
    [InlineData("งานกลุ่ม (0)", "คะแนนเต็มต้องมากกว่า 0")]
    [InlineData("งานกลุ่ม (10.555)", "ทศนิยมได้ไม่เกิน 2 ตำแหน่ง")]
    [InlineData("(10)", "กรุณากรอกชื่อรายการคะแนน")]
    public void หัวคอลัมน์คะแนนผิดรูปแบบ_ไม่ผ่าน(string header, string message) =>
        Assert.Contains(message, SingleError(ScoreSheetParser.Parse(Sheet(["รหัสนักเรียน", header], ["90002", 5]), Room())).Message);

    [Fact]
    public void คอลัมน์รายการซ้ำ_ไม่ผ่าน() =>
        Assert.Contains("ซ้ำ", SingleError(ScoreSheetParser.Parse(
            Sheet(["รหัสนักเรียน", "สอบกลางภาค (20)", "สอบกลางภาค"], ["90002", 5, 6]), Room())).Message);

    [Fact]
    public void ไม่มีคอลัมน์คะแนน_ไม่ผ่าน() =>
        Assert.Contains("ไม่พบคอลัมน์คะแนน",
            SingleError(ScoreSheetParser.Parse(Sheet(["เลขที่", "รหัสนักเรียน"], [2, "90002"]), Room())).Message);

    [Fact]
    public void ไม่มีคอลัมน์รหัสนักเรียน_ไม่ผ่าน() =>
        Assert.Contains("\"รหัสนักเรียน\"",
            SingleError(ScoreSheetParser.Parse(Sheet(["เลขที่", "สอบกลางภาค (20)"], [2, 5]), Room())).Message);

    [Fact]
    public void ใช้แค่รหัสกับคะแนนก็ได้()
    {
        var change = Assert.Single(ScoreSheetParser.Parse(Sheet(["รหัสนักเรียน", "สอบกลางภาค (20)"], ["90002", 10]), Room()).Plan!.Scores);

        Assert.Equal((11, 10m), (change.StudentId, change.NewValue));
    }

    [Fact]
    public void รหัสไม่อยู่ในห้องนี้_ไม่ผ่าน() =>
        Assert.Contains("ไม่มีนักเรียนรหัส 70001 ในห้องนี้",
            SingleError(ScoreSheetParser.Parse(Sheet(Header, [3, "70001", "ซี", "สาม", 10]), Room())).Message);

    [Fact]
    public void เลขที่ไม่ตรงกับรหัส_ไม่ผ่านกันคะแนนลงผิดคน()
    {
        var error = SingleError(ScoreSheetParser.Parse(Sheet(Header, [1, "90002", "บี", "สอง", 10]), Room()));

        Assert.Equal("เลขที่", error.Column);
    }

    [Fact]
    public void ชื่อไม่ตรงกับรหัส_ไม่ผ่าน() =>
        Assert.Contains("เป็นของ บี สอง",
            SingleError(ScoreSheetParser.Parse(Sheet(Header, [2, "90002", "เอ", "หนึ่ง", 10]), Room())).Message);

    [Fact]
    public void รหัสซ้ำในไฟล์_ไม่ผ่าน() =>
        Assert.Equal("รหัสนักเรียน 90002 ซ้ำกับแถว 2", SingleError(ScoreSheetParser.Parse(
            Sheet(["รหัสนักเรียน", "สอบกลางภาค (20)"], ["90002", 5], ["90002", 6]), Room())).Message);

    [Theory]
    [InlineData(21, "คะแนนเกินคะแนนเต็ม (20)")]
    [InlineData(-1, "คะแนนติดลบไม่ได้")]
    [InlineData(18.555, "คะแนนมีทศนิยมได้ไม่เกิน 2 ตำแหน่ง")]
    [InlineData("ขาดสอบ", "\"ขาดสอบ\" ไม่ใช่ตัวเลข")]
    [InlineData(true, "ช่องนี้ต้องเป็นตัวเลข")]
    [InlineData(1e12, "ตัวเลขใหญ่ผิดปกติ")]
    public void คะแนนผิด_ไม่ผ่านและบอกคอลัมน์(object score, string message)
    {
        var error = SingleError(ParseRow(score));

        Assert.Equal((2, "สอบกลางภาค (20)", message), (error.Row!.Value, error.Column, error.Message));
    }

    [Fact]
    public void คะแนนเป็นวันที่_บอกให้แก้รูปแบบเซลล์() =>
        Assert.Contains("วันที่", SingleError(ParseRow(new DateTime(2026, 1, 20))).Message);

    [Theory]
    [InlineData("๑๙.๕", 19.5)]
    [InlineData(" 17 ", 17)]
    public void ตัวเลขที่เป็นข้อความ_อ่านได้(string text, double expected) =>
        Assert.Equal((decimal)expected, Assert.Single(ParseRow(text).Plan!.Scores).NewValue);

    [Fact]
    public void เศษทศนิยมจากdoubleของExcel_ไม่นับเป็นทศนิยมเกิน() =>
        Assert.Equal(0.3m, Assert.Single(ParseRow(0.1 + 0.2).Plan!.Scores).NewValue);

    [Fact]
    public void ผิดแถวเดียว_ไม่ได้แผนเลย()
    {
        var result = ScoreSheetParser.Parse(Sheet(Header, [1, "90001", "เอ", "หนึ่ง", 19], [2, "90002", "บี", "สอง", 25]), Room());

        Assert.Null(result.Plan);
        Assert.Equal(1, result.ErrorCount);
    }
}
