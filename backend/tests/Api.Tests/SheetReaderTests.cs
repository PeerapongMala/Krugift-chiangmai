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
    public void ไฟล์ส่งออกมี2ชีทเรียงนักเรียนแล้วคะแนน()
    {
        using var workbook = Exported();

        Assert.Equal(["นักเรียน", "คะแนน"], workbook.Worksheets.Select(w => w.Name));
    }

    [Fact]
    public void ชีทนักเรียนมีหัวตารางและรหัสที่ขึ้นต้นด้วยศูนย์ครบ()
    {
        using var workbook = Exported();
        var sheet = workbook.Worksheet("นักเรียน");

        Assert.Equal(["เลขที่", "รหัสนักเรียน", "ชื่อ", "นามสกุล"],
            Enumerable.Range(1, 4).Select(c => sheet.Cell(1, c).GetString()));
        // รหัสขึ้นต้นด้วย 0 ต้องไม่ถูก Excel ทำเป็นตัวเลขจนศูนย์หาย
        Assert.Equal("01234", sheet.Cell(2, 2).GetString());
    }

    [Fact]
    public void ชีทคะแนนมีคะแนนเต็มบนหัวและคะแนนของนักเรียน()
    {
        using var workbook = Exported();
        var sheet = workbook.Worksheet("คะแนน");

        Assert.Equal("สอบกลางภาค (20)", sheet.Cell(1, 5).GetString());
        Assert.Equal("งาน/การบ้าน (7.5)", sheet.Cell(1, 6).GetString());
        Assert.Equal(17.5, sheet.Cell(2, 5).GetDouble());
        Assert.Equal(7, sheet.Cell(3, 6).GetDouble());
    }

    [Fact]
    public void ห้องที่ยังไม่มีนักเรียนก็ส่งออกได้()
    {
        ExistingItem[] items = [new(1, "สอบกลางภาค", 20m)];
        using var workbook = new XLWorkbook(
            new MemoryStream(ImportTemplates.Classroom(items, [], new Dictionary<(int, int), decimal?>())));

        Assert.Equal("สอบกลางภาค (20)", workbook.Worksheet("คะแนน").Cell(1, 5).GetString());
    }

    /// ไฟล์ส่งออกของห้องตัวอย่าง · นักเรียน 2 คน รายการคะแนน 2 รายการ
    static XLWorkbook Exported()
    {
        ExistingItem[] items = [new(1, "สอบกลางภาค", 20m), new(2, "งาน/การบ้าน", 7.5m)];
        var scores = new Dictionary<(int, int), decimal?> { [(1, 10)] = 17.5m, [(2, 11)] = 7m };
        return new XLWorkbook(new MemoryStream(ImportTemplates.Classroom(items, Students(), scores)));
    }
}
