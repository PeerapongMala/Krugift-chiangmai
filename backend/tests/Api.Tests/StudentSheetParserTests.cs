using Api.Common;
using Api.Import;
using static Api.Tests.SheetBuilder;

namespace Api.Tests;

public class StudentSheetParserTests
{
    static readonly object?[] Header = ["เลขที่", "รหัสนักเรียน", "ชื่อ", "นามสกุล"];

    static StudentSnapshot EmptyRoom() => new([], new Dictionary<string, ExistingStudent>());

    /// ห้องที่มี 90001 เลขที่ 1 และ 90002 เลขที่ 2 · 70001 มีในระบบแต่อยู่ห้องอื่น
    static StudentSnapshot Room() => new(
        [new ExistingEnrollment(10, 1, "90001", "เอ", "หนึ่ง"), new ExistingEnrollment(11, 2, "90002", "บี", "สอง")],
        new Dictionary<string, ExistingStudent>
        {
            ["90001"] = new(10, "90001", "เอ", "หนึ่ง"),
            ["90002"] = new(11, "90002", "บี", "สอง"),
            ["70001"] = new(12, "70001", "ซี", "สาม"),
        });

    static ImportError SingleError(ImportResult<StudentPlan> result)
    {
        Assert.Null(result.Plan);
        return Assert.Single(result.Errors);
    }

    [Fact]
    public void ห้องว่าง_ทุกคนเป็นนักเรียนใหม่()
    {
        var result = StudentSheetParser.Parse(Sheet(Header, [1, "90001", "เอ", "หนึ่ง"], [2, "90002", "บี", "สอง"]), EmptyRoom());

        Assert.True(result.IsValid);
        Assert.All(result.Plan!.Students, s => Assert.True(s.IsNew));
        Assert.True(result.Plan.HasChanges);
        Assert.Contains("เพิ่มนักเรียนใหม่เข้าระบบ 2 คน", result.Plan.Summary);
    }

    [Fact]
    public void นักเรียนที่มีในระบบแล้ว_ดึงเข้าห้อง_ไม่สร้างซ้ำ()
    {
        var student = Assert.Single(StudentSheetParser.Parse(Sheet(Header, [3, "70001", "ซี", "สาม"]), Room()).Plan!.Students);

        Assert.Equal(12, student.StudentId);
        Assert.True(student.JoinsClassroom);
    }

    [Fact]
    public void ไฟล์ตรงกับในระบบทุกอย่าง_ไม่มีอะไรเปลี่ยน()
    {
        var plan = StudentSheetParser.Parse(Sheet(Header, [1, "90001", "เอ", "หนึ่ง"], [2, "90002", "บี", "สอง"]), Room()).Plan!;

        Assert.False(plan.HasChanges);
        Assert.Empty(plan.Changes);
    }

    [Fact]
    public void สลับเลขที่กันระหว่างคนในไฟล์_ทำได้()
    {
        var plan = StudentSheetParser.Parse(Sheet(Header, [2, "90001", "เอ", "หนึ่ง"], [1, "90002", "บี", "สอง"]), Room()).Plan!;

        Assert.All(plan.Students, s => Assert.True(s.IsRenumbered));
        Assert.Contains(plan.Changes, c => c.Detail == "เลขที่ 1 → 2");
    }

    [Fact]
    public void เลขที่ชนกับคนในห้องที่ไม่มีในไฟล์_ไม่ผ่าน()
    {
        var error = SingleError(StudentSheetParser.Parse(Sheet(Header, [2, "70001", "ซี", "สาม"]), Room()));

        Assert.Equal((2, "เลขที่"), (error.Row!.Value, error.Column));
        Assert.Contains("บี สอง", error.Message);
    }

    [Fact]
    public void รหัสมีในระบบแต่ชื่อไม่ตรง_ไม่ผ่าน()
    {
        var error = SingleError(StudentSheetParser.Parse(Sheet(Header, [3, "70001", "ดี", "สี่"]), Room()));

        Assert.Equal("รหัสนักเรียน", error.Column);
        Assert.Contains("ซี สาม", error.Message);
    }

    [Fact]
    public void ชื่อมีช่องว่างเกินหรืออักขระล่องหน_ถือว่าตรง()
    {
        var result = StudentSheetParser.Parse(Sheet(Header, [1, "90001", "  เอ\u200B ", "หนึ่ง\u00A0"]), Room());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void เลขที่ซ้ำในไฟล์_ไม่ผ่าน()
    {
        var error = SingleError(StudentSheetParser.Parse(Sheet(Header, [5, "80001", "ก", "ข"], [5, "80002", "ค", "ง"]), EmptyRoom()));

        Assert.Equal(3, error.Row);
        Assert.Equal("เลขที่ 5 ซ้ำกับแถว 2", error.Message);
    }

    [Fact]
    public void รหัสซ้ำในไฟล์_ไม่ผ่าน()
    {
        var error = SingleError(StudentSheetParser.Parse(Sheet(Header, [1, "80001", "ก", "ข"], [2, "80001", "ค", "ง"]), EmptyRoom()));

        Assert.Equal("รหัสนักเรียน 80001 ซ้ำกับแถว 2", error.Message);
    }

    [Fact]
    public void ขาดคอลัมน์ที่จำเป็น_บอกชื่อคอลัมน์ที่ขาด()
    {
        var error = SingleError(StudentSheetParser.Parse(Sheet(["รหัสนักเรียน", "ชื่อ"], ["80001", "ก"]), EmptyRoom()));

        Assert.Null(error.Row);
        Assert.Contains("\"เลขที่\"", error.Message);
        Assert.Contains("\"นามสกุล\"", error.Message);
    }

    [Fact]
    public void เอาไฟล์คะแนนมาใส่_ไม่ผ่าน()
    {
        var error = SingleError(StudentSheetParser.Parse(
            Sheet([.. Header, "สอบกลางภาค (20)"], [1, "80001", "ก", "ข", 10]), EmptyRoom()));

        Assert.Contains("นำเข้าคะแนน", error.Message);
    }

    [Fact]
    public void คอลัมน์ซ้ำ_ไม่ผ่าน()
    {
        var error = SingleError(StudentSheetParser.Parse(Sheet([.. Header, "ชื่อ"], [1, "80001", "ก", "ข", "ก"]), EmptyRoom()));

        Assert.Contains("ซ้ำ", error.Message);
    }

    [Fact]
    public void คอลัมน์ไม่มีหัวแต่มีข้อมูล_ไม่ผ่าน()
    {
        var error = SingleError(StudentSheetParser.Parse(Sheet([.. Header, null], [1, "80001", "ก", "ข", "หมายเหตุ"]), EmptyRoom()));

        Assert.Equal("E", error.Column);
    }

    [Fact]
    public void แถวว่างถูกข้าม()
    {
        var result = StudentSheetParser.Parse(
            Sheet(Header, [1, "80001", "ก", "ข"], [null, null, " ", null], [2, "80002", "ค", "ง"]), EmptyRoom());

        Assert.Equal(2, result.Plan!.Students.Count);
    }

    [Fact]
    public void ไม่มีข้อมูล_ไม่ผ่าน() =>
        Assert.Contains("ไม่มีข้อมูล", SingleError(StudentSheetParser.Parse(Sheet(Header), EmptyRoom())).Message);

    [Fact]
    public void ผิดแถวเดียว_ไม่ได้แผนเลย()
    {
        var result = StudentSheetParser.Parse(
            Sheet(Header, [1, "80001", "ก", "ข"], [2, "8000#", "ค", "ง"], [3, "80003", "จ", "ฉ"]), EmptyRoom());

        Assert.Null(result.Plan);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public void แถวว่างเกือบทั้งแถว_รายงานครบทุกช่องในรอบเดียว()
    {
        var result = StudentSheetParser.Parse(Sheet(Header, [null, null, null, "ข"], [1, "80001", "ก", "ข"]), EmptyRoom());

        Assert.Equal(3, result.ErrorCount);
        Assert.All(result.Errors, e => Assert.Equal(2, e.Row));
    }

    [Theory]
    [InlineData(1.5, "เลขที่ต้องเป็นจำนวนเต็ม")]
    [InlineData(0, "เลขที่ต้องอยู่ระหว่าง 1 ถึง 999")]
    [InlineData(1000, "เลขที่ต้องอยู่ระหว่าง 1 ถึง 999")]
    [InlineData("หนึ่ง", "\"หนึ่ง\" ไม่ใช่ตัวเลข")]
    [InlineData(true, "ช่องนี้ต้องเป็นตัวเลข")]
    public void เลขที่ผิดรูปแบบ_ไม่ผ่าน(object value, string message) =>
        Assert.Equal(message, SingleError(StudentSheetParser.Parse(Sheet(Header, [value, "80001", "ก", "ข"]), EmptyRoom())).Message);

    [Fact]
    public void เลขที่เป็นวันที่_บอกให้แก้รูปแบบเซลล์() =>
        Assert.Contains("วันที่", SingleError(StudentSheetParser.Parse(
            Sheet(Header, [new DateTime(2026, 1, 1), "80001", "ก", "ข"]), EmptyRoom())).Message);

    [Fact]
    public void เลขไทย_อ่านได้ทั้งเลขที่และรหัส()
    {
        var student = Assert.Single(StudentSheetParser.Parse(Sheet(Header, ["๑", "๙๐๐๐๑", "เอ", "หนึ่ง"]), Room()).Plan!.Students);

        Assert.Equal(("90001", 1, 10), (student.Code, student.No, student.StudentId!.Value));
    }

    [Fact]
    public void รหัสที่Excelเก็บเป็นตัวเลข_อ่านเป็นข้อความได้()
    {
        var student = Assert.Single(StudentSheetParser.Parse(Sheet(Header, [1, 90001.0, "เอ", "หนึ่ง"]), Room()).Plan!.Students);

        Assert.Equal(10, student.StudentId);
    }

    [Theory]
    [InlineData(90001.5, "รหัสนักเรียนต้องเป็นตัวเลขหรือตัวอักษร")]
    [InlineData("9000 1", "รหัสนักเรียนใช้ได้เฉพาะตัวเลขและตัวอักษร")]
    [InlineData("", "กรุณากรอกรหัสนักเรียน")]
    public void รหัสผิดรูปแบบ_ไม่ผ่าน(object value, string message) =>
        Assert.Equal(message, SingleError(StudentSheetParser.Parse(Sheet(Header, [1, value, "ก", "ข"]), EmptyRoom())).Message);

    [Fact]
    public void ชื่อเป็นตัวเลข_ไม่ผ่าน() =>
        Assert.Equal("ชื่อนักเรียนต้องเป็นข้อความ",
            SingleError(StudentSheetParser.Parse(Sheet(Header, [1, "80001", 123, "ข"]), EmptyRoom())).Message);

    [Fact]
    public void ชื่อยาวเกิน_ไม่ผ่าน() =>
        Assert.Contains("ยาวเกิน", SingleError(StudentSheetParser.Parse(
            Sheet(Header, [1, "80001", new string('ก', Limits.NameLength + 1), "ข"]), EmptyRoom())).Message);

    [Fact]
    public void จุดผิดเยอะเกินเพดาน_ตัดรายการแต่บอกจำนวนจริง()
    {
        var rows = Enumerable.Range(1, Limits.ImportMaxErrors + 50).Select(i => new object?[] { i, "!", "ก", "ข" }).ToArray();

        var result = StudentSheetParser.Parse(Sheet(Header, rows), EmptyRoom());

        Assert.Equal(Limits.ImportMaxErrors, result.Errors.Count);
        Assert.Equal(Limits.ImportMaxErrors + 50, result.ErrorCount);
    }

    [Fact]
    public void ดึงรหัสทั้งหมดในไฟล์ไปโหลดจากDB()
    {
        var codes = StudentSheetParser.StudentCodes(Sheet(Header, [1, "80001", "ก", "ข"], [2, 80002.0, "ค", "ง"], [3, "!", "จ", "ฉ"]));

        Assert.Equal(["80001", "80002"], codes);
    }
}
