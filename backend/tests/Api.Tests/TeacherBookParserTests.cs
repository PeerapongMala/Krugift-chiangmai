using Api.Import;

namespace Api.Tests;

/// ไฟล์ครูจริง: ชีทละห้อง หัวตาราง 3 ชั้น ช่อง merge ถูกเติมค่าให้แล้วโดย BookReader
public class TeacherBookParserTests
{
    /// แถว 1 หัวเรื่อง · แถว 2-3 ชื่อกลุ่ม/ชื่อย่อย · แถว 4 หัวตาราง+คะแนนเต็ม · แถว 5+ นักเรียน
    static BookSheet Book(string name, params object?[][] rows) =>
        new(name, rows.Select((cells, i) => new SheetRow(i + 1, cells.Select(SheetBuilder.Cell).ToList())).ToList());

    static object?[] Row(params object?[] cells) => cells;

    /// ชีทย่อส่วนที่หน้าตาเหมือนไฟล์ครู: เลขที่ · คำนำหน้า/ชื่อ/นามสกุล (merge 3 ช่อง) · ชื่อเล่น · รหัส · คะแนน
    static BookSheet Sample() => Book("ห้อง 1",
        Row("รายชื่อนักเรียนชั้นมัธยมศึกษาปีที่ 1/1  ปีการศึกษา 2569"),
        Row(null, null, null, null, null, null, "เก็บ 1 (25 คะแนน) / จำนวนเต็ม", "เก็บ 1 (25 คะแนน) / จำนวนเต็ม",
            "เก็บ 1 (25 คะแนน) / จำนวนเต็ม", "Midterm", "Midterm", "แบบฝึกหัด", "1"),
        Row(null, null, null, null, null, null, "สอบ 1", null, "เอกสาร", null, null, "เต็ม", null),
        Row("เลขที่", "ชื่อ - นามสกุล", "ชื่อ - นามสกุล", "ชื่อ - นามสกุล", "ชื่อเล่น", "รหัสนักเรียน",
            45, 10, 5, 35, 20, 1, null),
        Row(1, "เด็กหญิง", "กชณิภา", "วัชระวรากร", "ไอ", "690001", 32.5, 7.22, 5, 33, 18.86, 1),
        Row(2, "เด็กชาย", "กรชวัล", "มงพลเมือง", "ปีโป้", "690006", 14.5, null, 4, null, null, null));

    static (BookSheetPlan? Plan, ImportErrors Errors) Parse(BookSheet sheet)
    {
        var errors = new ImportErrors();
        return (TeacherBookParser.Parse(sheet, errors), errors);
    }

    [Fact]
    public void อ่านชื่อห้องจากหัวเรื่อง()
    {
        var (plan, errors) = Parse(Sample());

        Assert.Equal(0, errors.Count);
        Assert.Equal("ม.1/1", plan!.ClassroomName);
    }

    [Fact]
    public void ไม่เจอชื่อห้องในหัวเรื่องใช้ชื่อชีทแทน()
    {
        var sheet = Book("ห้องพิเศษ",
            Row("รายชื่อนักเรียน"),
            Row(null, null, null, null),
            Row(null, null, null, "สอบ 1"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10),
            Row(1, "สมชาย ใจดี", "70001", 5));

        var (plan, _) = Parse(sheet);

        Assert.Equal("ห้องพิเศษ", plan!.ClassroomName);
    }

    [Fact]
    public void แยกคำนำหน้าชื่อนามสกุลชื่อเล่นจากช่องที่merge3คอลัมน์()
    {
        var (plan, errors) = Parse(Sample());

        Assert.Equal(0, errors.Count);
        var first = plan!.Students[0];
        Assert.Equal(1, first.No);
        Assert.Equal("690001", first.Code);
        Assert.Equal("เด็กหญิง", first.Title);
        Assert.Equal("กชณิภา", first.FirstName);
        Assert.Equal("วัชระวรากร", first.LastName);
        Assert.Equal("ไอ", first.Nickname);
    }

    [Fact]
    public void ช่องชื่อmerge2คอลัมน์ถือว่าไม่มีคำนำหน้า()
    {
        var sheet = Book("ห้อง 9",
            Row("ปีที่ 2/3"),
            Row(null, null, null, null, null),
            Row(null, null, null, null, "สอบ 1"),
            Row("เลขที่", "ชื่อ - นามสกุล", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10),
            Row(1, "สมชาย", "ใจดี", "70001", 9));

        var (plan, errors) = Parse(sheet);

        Assert.Equal(0, errors.Count);
        Assert.Equal("", plan!.Students[0].Title);
        Assert.Equal("สมชาย", plan.Students[0].FirstName);
        Assert.Equal("ใจดี", plan.Students[0].LastName);
    }

    [Fact]
    public void เอาเฉพาะคอลัมน์สอบ_คะแนนที่หารแล้วให้เด็กเห็น_คะแนนดิบเฉพาะครู()
    {
        var (plan, _) = Parse(Sample());

        // เอกสาร แบบฝึกหัด และคอลัมน์หารซ้ำอันที่สาม ไม่ถูกนำเข้า
        Assert.Equal(
            ["จำนวนเต็ม สอบ 1", "จำนวนเต็ม สอบ 1 (คะแนนดิบ)", "สอบกลางภาค", "สอบกลางภาค (คะแนนดิบ)"],
            plan!.Items.Select(i => i.Name));
        Assert.Equal([10m, 45m, 20m, 35m], plan.Items.Select(i => i.MaxScore));
        Assert.Equal([false, true, false, true], plan.Items.Select(i => i.TeacherOnly));
    }

    [Fact]
    public void ไม่มีชื่อเรื่องใช้ชื่อสอบอย่างเดียว()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, "เก็บ 2", "Final", "Final"),
            Row(null, null, null, "สอบ 2", null, null),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10, 30, 20),
            Row(1, "สมชาย ใจดี", "70001", 9, 25, 16.67));

        var (plan, _) = Parse(sheet);

        Assert.Equal(["สอบ 2", "สอบปลายภาค", "สอบปลายภาค (คะแนนดิบ)"], plan!.Items.Select(i => i.Name));
    }

    [Fact]
    public void ชื่อรายการซ้ำต่อท้ายด้วยชื่อคอลัมน์()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, "ก / เรื่องเดียวกัน", "ก / เรื่องเดียวกัน"),
            Row(null, null, null, "สอบ 1", "สอบ 1"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10, 10),
            Row(1, "สมชาย ใจดี", "70001", 5, 6));

        var (plan, _) = Parse(sheet);

        Assert.Equal(["เรื่องเดียวกัน สอบ 1", "เรื่องเดียวกัน สอบ 1 (คอลัมน์ E)"], plan!.Items.Select(i => i.Name));
    }

    [Fact]
    public void มีคอลัมน์หารติดกันหลายอันให้เอาอันสุดท้าย()
    {
        // ไฟล์ครูจริง: G ดิบเต็ม 45 · H = (G/45)*10 · I = (G/40)*10 · ครูยืนยันให้ใช้ I
        var sheet = Book("ห้อง 1",
            Row("รายชื่อนักเรียนชั้นมัธยมศึกษาปีที่ 1/1"),
            Row(null, null, null, "เก็บ 1 / จำนวนเต็ม", "เก็บ 1 / จำนวนเต็ม", "เก็บ 1 / จำนวนเต็ม", "เก็บ 1 / จำนวนเต็ม"),
            Row(null, null, null, "สอบ 1", null, null, "เอกสาร"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 45, 10, 10, 5),
            Row(1, "อี ทู", "690001", 32.5, 7.22, 8.13, 5));

        var (plan, errors) = Parse(sheet);

        Assert.Equal(0, errors.Count);
        // เอาคอลัมน์หารอันสุดท้าย (8.13) เป็นคะแนนที่เด็กเห็น · เก็บคะแนนดิบไว้ให้ครู · คอลัมน์ "เอกสาร" ไม่ถูกดูด
        Assert.Equal(["จำนวนเต็ม สอบ 1", "จำนวนเต็ม สอบ 1 (คะแนนดิบ)"], plan!.Items.Select(i => i.Name));
        Assert.Equal([10m, 45m], plan.Items.Select(i => i.MaxScore));
        Assert.Equal([8.13m, 32.5m], plan.Scores.Select(s => s.Value));
    }

    [Fact]
    public void หัวสอบที่merteคร่อมทั้งสองคอลัมน์ยังจับคู่ดิบกับหารได้()
    {
        // ไฟล์ครูจริง merge หัว Midterm คร่อม M4:N5 ทั้งสองแถว ช่องหัวของคอลัมน์หารจึงถูกเติมข้อความเดียวกัน
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, "Midterm", "Midterm", "แบบฝึกหัด"),
            Row(null, null, null, "Midterm", "Midterm", "ส่งงาน"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 35, 20, 5),
            Row(1, "อี ทู", "690001", 33, 18.86, 5));

        var (plan, errors) = Parse(sheet);

        Assert.Equal(0, errors.Count);
        Assert.Equal(["สอบกลางภาค", "สอบกลางภาค (คะแนนดิบ)"], plan!.Items.Select(i => i.Name));
        Assert.Equal([20m, 35m], plan.Items.Select(i => i.MaxScore));
        Assert.Equal([18.86m, 33m], plan.Scores.Select(s => s.Value));
    }

    [Fact]
    public void ปัดคะแนนที่หารแล้วเป็นจำนวนเต็ม_คะแนนดิบคงค่าตามไฟล์()
    {
        var errors = new ImportErrors();
        var plan = TeacherBookParser.Parse(Sample(), errors, roundScores: true);

        Assert.Equal(0, errors.Count);
        // คนแรกเรียงตามลำดับรายการ: สอบ 1 หารแล้ว 7.22 → 7 · ดิบ 32.5 คงเดิม · กลางภาคหารแล้ว 18.86 → 19 · ดิบ 33 คงเดิม
        Assert.Equal([7m, 32.5m, 19m, 33m], plan!.Scores.Where(s => s.Row == 0).Select(s => s.Value));
    }

    [Fact]
    public void ไม่ติ๊กปัดเศษคะแนนต้องเท่าไฟล์เป๊ะ()
    {
        var (plan, _) = Parse(Sample());

        Assert.Equal([7.22m, 32.5m, 18.86m, 33m], plan!.Scores.Where(s => s.Row == 0).Select(s => s.Value));
    }

    [Fact]
    public void ช่องคะแนนว่างไม่นับเป็นคะแนน()
    {
        var (plan, errors) = Parse(Sample());

        Assert.Equal(0, errors.Count);
        // คนแรกมีทั้งคะแนนหารแล้วและคะแนนดิบของสอบ 1 กับสอบกลางภาค · คนที่สองมีแค่คะแนนดิบของสอบ 1
        Assert.Equal(4, plan!.Scores.Count(s => s.Row == 0));
        Assert.Equal(1, plan.Scores.Count(s => s.Row == 1));
    }

    [Fact]
    public void คะแนนติดลบเป็นจุดผิด()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, null),
            Row(null, null, null, "สอบ 1"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10),
            Row(1, "สมชาย ใจดี", "70001", -1));

        var (_, errors) = Parse(sheet);
        var result = errors.Result<BookPlan>(null);

        Assert.Contains("ติดลบ", result.Errors[0].Message);
        Assert.Equal("D", result.Errors[0].Column);
    }

    [Fact]
    public void เลขที่ซ้ำและรหัสซ้ำเป็นจุดผิด()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, null),
            Row(null, null, null, "สอบ 1"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10),
            Row(1, "สมชาย ใจดี", "70001", 5),
            Row(1, "สมหญิง ใจงาม", "70001", 6));

        var (_, errors) = Parse(sheet);

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void แถวว่างระหว่างตารางข้ามไปไม่เป็นจุดผิด()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, null),
            Row(null, null, null, "สอบ 1"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10),
            Row(1, "สมชาย ใจดี", "70001", 5),
            Row(null, null, null, null),
            Row(2, "สมหญิง ใจงาม", "70002", 6));

        var (plan, errors) = Parse(sheet);

        Assert.Equal(0, errors.Count);
        Assert.Equal(2, plan!.Students.Count);
    }

    [Fact]
    public void ไม่มีหัวตารางบอกให้รู้ว่าชีทไหน()
    {
        var sheet = Book("สรุป", Row("คะแนนรวมทุกห้อง"), Row("ห้อง", "คะแนน"));

        var (plan, errors) = Parse(sheet);

        Assert.Null(plan);
        Assert.Contains("สรุป", errors.Result<BookPlan>(null).Errors[0].Message);
    }

    [Fact]
    public void ไม่มีคอลัมน์คะแนนเป็นจุดผิด()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null),
            Row(null, null, null),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน"),
            Row(1, "สมชาย ใจดี", "70001"));

        var (plan, errors) = Parse(sheet);

        Assert.Empty(plan!.Items);
        Assert.Contains("ไม่พบคอลัมน์คะแนน", errors.Result<BookPlan>(null).Errors[0].Message);
    }

    [Fact]
    public void หมายเหตุใต้ตารางไม่ถือเป็นนักเรียน()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, null),
            Row(null, null, null, "สอบ 1"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10),
            Row(1, "สมชาย ใจดี", "70001", 5),
            Row(null, null, null, null),
            Row("แบบฝึกหัด", "ส่งครบ 4"),
            Row("จิตพิสัย", "1-2 สัปดาห์"));

        var (plan, errors) = Parse(sheet);

        Assert.Equal(0, errors.Count);
        Assert.Single(plan!.Students);
    }

    [Fact]
    public void เลขที่ผิดแต่มีรหัสนักเรียนยังเป็นจุดผิดไม่ใช่ท้ายตาราง()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, null),
            Row(null, null, null, "สอบ 1"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10),
            Row("หนึ่ง", "สมชาย ใจดี", "70001", 5));

        var (_, errors) = Parse(sheet);

        Assert.Contains(errors.Result<BookPlan>(null).Errors, e => e.Message.Contains("ไม่ใช่ตัวเลข"));
    }

    [Fact]
    public void คะแนนที่คำนวณมาทศนิยมยาวปัดเหลือ2ตำแหน่ง()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, null),
            Row(null, null, null, "สอบ 1"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10),
            Row(1, "สมชาย ใจดี", "70001", 7.222222222));

        var (plan, errors) = Parse(sheet);

        Assert.Equal(0, errors.Count);
        Assert.Equal(7.22m, plan!.Scores[0].Value);
    }

    [Fact]
    public void คะแนนที่เกินคะแนนเต็มถูกตัดลงมาเท่าคะแนนเต็ม()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, null),
            Row(null, null, null, "สอบ 1"),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 10),
            Row(1, "สมชาย ใจดี", "70001", 10.5),
            Row(2, "สมหญิง ใจงาม", "70002", 9));

        var (plan, errors) = Parse(sheet);

        Assert.Equal(0, errors.Count);
        // คะแนนเต็มยังเป็น 10 ตามที่ครูเขียนหัวตาราง ส่วน 10.5 ถูกตัดลงมาเป็น 10
        Assert.Equal(10m, plan!.Items[0].MaxScore);
        Assert.Equal([10m, 9m], plan.Scores.Select(s => s.Value));
    }

    [Fact]
    public void คอลัมน์ที่มีชื่อย่อยไม่ดึงชื่อกลุ่มของคอลัมน์ถัดไปมาต่อ()
    {
        var sheet = Book("ห้อง 1",
            Row("ปีที่ 1/1"),
            Row(null, null, null, "เก็บ 2 / เศษส่วน", "Final"),
            Row(null, null, null, "สอบ 1", null),
            Row("เลขที่", "ชื่อ - นามสกุล", "รหัสนักเรียน", 5, null),
            Row(1, "สมชาย ใจดี", "70001", 5));

        var (plan, _) = Parse(sheet);

        Assert.Equal(["เศษส่วน สอบ 1"], plan!.Items.Select(i => i.Name));
    }

}
