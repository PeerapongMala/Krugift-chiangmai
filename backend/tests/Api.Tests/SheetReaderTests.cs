using System.IO.Compression;
using Api.Common;
using Api.Import;
using ClosedXML.Excel;

namespace Api.Tests;

public class SheetReaderTests
{
    /// รหัส 01234 ขึ้นต้นด้วย 0 ไว้ยืนยันว่า template ไม่ทำเลข 0 หาย
    static ExistingEnrollment[] Students() =>
    [
        new(10, 1, "01234", "เอ", "หนึ่ง"),
        new(11, 2, "90002", "บี", "สอง"),
    ];

    static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    static byte[] Workbook(Action<IXLWorksheet> fill)
    {
        using var workbook = new XLWorkbook();
        fill(workbook.AddWorksheet("แผ่น1"));
        return Save(workbook);
    }

    /// zip ที่แต่ละไฟล์ข้างในเป็นเลข 0 ขนาดตามที่กำหนด · บีบอัดแล้วเล็กมาก ใช้จำลอง zip bomb
    static byte[] Zip(params (string Name, long Size)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var chunk = new byte[1024 * 1024];
            foreach (var (name, size) in entries)
            {
                using var entry = zip.CreateEntry(name, CompressionLevel.SmallestSize).Open();
                for (long written = 0; written < size; written += chunk.Length)
                    entry.Write(chunk, 0, (int)Math.Min(chunk.Length, size - written));
            }
        }
        return stream.ToArray();
    }

    static string Errors<TPlan>(ImportResult<TPlan> result) where TPlan : class, IImportPlan =>
        string.Join(" | ", result.Errors.Select(e => $"แถว {e.Row} {e.Column}: {e.Message}"));

    [Fact]
    public void ไฟล์ว่าง_ไม่ผ่าน() => Assert.Equal("ไฟล์ว่างเปล่า", SheetReader.Read([]).Error);

    [Fact]
    public void ไม่ใช่zip_เช่นcsvที่เปลี่ยนนามสกุล_ไม่ผ่าน() =>
        Assert.Equal(SheetReader.NotXlsx, SheetReader.Read("code,name\n90001,a\n"u8.ToArray()).Error);

    [Fact]
    public void ใหญ่เกินเพดาน_ไม่ผ่าน() =>
        Assert.Equal(SheetReader.TooLarge, SheetReader.Read(new byte[Limits.ImportMaxBytes + 1]).Error);

    [Fact]
    public void zipที่ไม่ใช่Excel_ไม่ผ่าน() =>
        Assert.Equal(SheetReader.NotXlsx, SheetReader.Read(Zip(("readme.txt", 10))).Error);

    [Fact]
    public void zipโครงเหมือนxlsxแต่ข้างในพัง_ไม่ผ่าน() =>
        Assert.Equal(SheetReader.NotXlsx, SheetReader.Read(Zip(("[Content_Types].xml", 10), ("xl/workbook.xml", 10))).Error);

    [Fact]
    public void zipBomb_ไม่ผ่านก่อนเปิดไฟล์()
    {
        var bomb = Zip(("[Content_Types].xml", 10), ("xl/workbook.xml", 10), ("xl/worksheets/sheet1.xml", Limits.ImportMaxUnzippedBytes + 1));

        Assert.True(bomb.Length <= Limits.ImportMaxBytes);
        Assert.Contains("ใหญ่ผิดปกติ", SheetReader.Read(bomb).Error);
    }

    [Fact]
    public void แผ่นงานว่าง_ไม่ผ่าน() => Assert.Equal("แผ่นงานแรกไม่มีข้อมูล", SheetReader.Read(Workbook(_ => { })).Error);

    [Fact]
    public void ข้อมูลเกินเพดานแถว_ไม่ผ่าน()
    {
        var bytes = Workbook(sheet =>
        {
            sheet.Cell(1, 1).SetValue("รหัสนักเรียน");
            sheet.Cell(Limits.ImportMaxRows + 2, 1).SetValue("90001");
        });

        Assert.Contains($"เกิน {Limits.ImportMaxRows} แถว", SheetReader.Read(bytes).Error);
    }

    [Fact]
    public void แผ่นงานที่ซ่อนไว้ถูกข้าม()
    {
        using var workbook = new XLWorkbook();
        var hidden = workbook.AddWorksheet("อ้างอิง");
        hidden.Cell(1, 1).SetValue("ข้อมูลซ่อน");
        hidden.Hide();
        workbook.AddWorksheet("คะแนน").Cell(1, 1).SetValue("รหัสนักเรียน");

        var (sheet, error) = SheetReader.Read(Save(workbook));

        Assert.Null(error);
        Assert.Equal("รหัสนักเรียน", sheet!.Header[0].Text);
    }

    [Fact]
    public void ช่องวันที่ในไฟล์จริง_อ่านเป็นวันที่ให้parserดัก()
    {
        var bytes = Workbook(sheet =>
        {
            sheet.Cell(1, 1).SetValue("รหัสนักเรียน");
            sheet.Cell(2, 1).SetValue(new DateTime(2026, 1, 20));
        });

        Assert.Equal(CellKind.Date, SheetReader.Read(bytes).Sheet!.Rows[0][0].Kind);
    }

    [Fact]
    public void templateนักเรียน_อ่านกลับแล้วไม่มีอะไรเปลี่ยน()
    {
        var students = Students();
        var (sheet, error) = SheetReader.Read(ImportTemplates.Students(students));
        Assert.Null(error);

        var snapshot = new StudentSnapshot(students,
            students.ToDictionary(s => s.Code, s => new ExistingStudent(s.StudentId, s.Code, s.FirstName, s.LastName)));
        var result = StudentSheetParser.Parse(sheet!, snapshot);

        Assert.True(result.IsValid, Errors(result));
        Assert.False(result.Plan!.HasChanges);
        Assert.Contains(result.Plan.Students, s => s.Code == "01234");
    }

    [Fact]
    public void templateคะแนน_อ่านกลับแล้วไม่มีอะไรเปลี่ยน()
    {
        var students = Students();
        ExistingItem[] items = [new(1, "สอบกลางภาค", 20m), new(2, "งาน/การบ้าน", 7.5m)];
        var scores = new Dictionary<(int, int), decimal?> { [(1, 10)] = 17.5m, [(2, 11)] = 7m };

        var (sheet, error) = SheetReader.Read(ImportTemplates.Scores(items, students, scores));
        Assert.Null(error);

        var result = ScoreSheetParser.Parse(sheet!, new ScoreSnapshot(items, students, scores));

        Assert.True(result.IsValid, Errors(result));
        Assert.False(result.Plan!.HasChanges);
    }

    [Fact]
    public void templateคะแนนห้องว่าง_ยังอ่านหัวตารางได้()
    {
        ExistingItem[] items = [new(1, "สอบกลางภาค", 20m)];

        var (sheet, error) = SheetReader.Read(ImportTemplates.Scores(items, [], new Dictionary<(int, int), decimal?>()));

        Assert.Null(error);
        Assert.Equal(["เลขที่", "รหัสนักเรียน", "ชื่อ", "นามสกุล", "สอบกลางภาค (20)"], sheet!.Header.Select(c => c.Text));
    }
}
