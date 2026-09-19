using System.IO.Compression;
using Api.Common;
using ClosedXML.Excel;

namespace Api.Import;

/// <summary>
/// อ่านไฟล์ .xlsx เป็น Sheet · ด่านระดับไฟล์: ขนาด ชนิดไฟล์ zip bomb ไฟล์เสีย จำนวนแถว/คอลัมน์
/// ไม่แตะ DB และไม่ตัดสินว่าข้อมูลถูกหรือผิด (เป็นหน้าที่ของ parser)
/// </summary>
public static class SheetReader
{
    public const string NotXlsx =
        "ไฟล์นี้ไม่ใช่ Excel .xlsx หรือไฟล์เสีย · ถ้าเป็น .xls หรือ .csv ให้เปิดใน Excel แล้วเลือก Save As เป็น Excel Workbook (.xlsx)";

    public static readonly string TooLarge =
        $"ไฟล์ใหญ่เกิน {Limits.ImportMaxBytes / (1024 * 1024)} MB · ไฟล์ของห้องหนึ่งปกติไม่ถึง 100 KB ลองลบแผ่นงานหรือรูปภาพที่ไม่ใช้ออก";

    /// xlsx ปกติมีส่วนประกอบไม่กี่สิบไฟล์ข้างใน
    const int MaxZipEntries = 1000;

    const int HeaderRows = 1;

    /// คอลัมน์ข้อมูลนักเรียน 4 คอลัมน์ + คอลัมน์คะแนน
    const int MaxColumns = 4 + Limits.ImportMaxItems;

    public static (Sheet? Sheet, string? Error) Read(byte[] bytes)
    {
        if (bytes.Length == 0) return (null, "ไฟล์ว่างเปล่า");
        if (bytes.Length > Limits.ImportMaxBytes) return (null, TooLarge);
        if (CheckZip(bytes) is { } zipError) return (null, zipError);

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(bytes, writable: false));
        }
        // zip ถูกต้องแต่ข้างในพัง ClosedXML โยน exception ได้หลายชนิด ตอบข้อความเดียวที่ครูทำตามได้
        catch (Exception)
        {
            return (null, NotXlsx);
        }

        using (workbook)
            return ReadFirstVisibleSheet(workbook);
    }

    /// ใช้ร่วมกับ BookReader (ไฟล์ครู) ด่านระดับไฟล์จะได้เหมือนกันทุกทาง
    internal static string? CheckZip(byte[] bytes)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
            if (zip.GetEntry("[Content_Types].xml") is null || zip.GetEntry("xl/workbook.xml") is null) return NotXlsx;
            if (zip.Entries.Count > MaxZipEntries) return NotXlsx;

            // ponytail: ใช้ขนาดที่ header ของ zip ประกาศไว้ ไฟล์ที่ปลอม header ได้จะหลุด แต่ครอบคลุม zip bomb ทั่วไปแล้ว
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                total += entry.Length;
                if (total > Limits.ImportMaxUnzippedBytes)
                    return "ข้อมูลในไฟล์ใหญ่ผิดปกติ ไม่ใช่ไฟล์คะแนนทั่วไป · ดาวน์โหลดไฟล์ตัวอย่างไปใช้แทน";
            }
            return null;
        }
        catch (InvalidDataException)
        {
            return NotXlsx;
        }
    }

    static (Sheet? Sheet, string? Error) ReadFirstVisibleSheet(XLWorkbook workbook)
    {
        // แผ่นงานที่ซ่อนไว้มักเป็นข้อมูลอ้างอิงที่ครูไม่ได้ตั้งใจให้นำเข้า
        var sheet = workbook.Worksheets.FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible);
        if (sheet is null) return (null, "ไม่พบแผ่นงาน (sheet) ที่มองเห็นได้ในไฟล์");

        // นับเฉพาะช่องที่มีค่า · ช่องที่แค่จัดรูปแบบหรือตั้ง data validation ไว้ทั้งคอลัมน์ไม่นับ
        var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0;
        if (lastRow == 0) return (null, "แผ่นงานแรกไม่มีข้อมูล");
        if (lastRow - HeaderRows > Limits.ImportMaxRows)
            return (null, $"ไฟล์มีข้อมูลเกิน {Limits.ImportMaxRows} แถว ให้แยกเป็นไฟล์ละห้อง");
        if (lastColumn > MaxColumns)
            return (null, $"ไฟล์มีคอลัมน์เกิน {MaxColumns} คอลัมน์ · คอลัมน์คะแนนได้ไม่เกิน {Limits.ImportMaxItems} รายการ");

        var header = Enumerable.Range(1, lastColumn).Select(c => ToCell(sheet.Cell(1, c))).ToList();
        var rows = Enumerable.Range(HeaderRows + 1, lastRow - HeaderRows)
            .Select(r => new SheetRow(r, Enumerable.Range(1, lastColumn).Select(c => ToCell(sheet.Cell(r, c))).ToList()))
            .ToList();
        return (new Sheet(header, rows), null);
    }

    internal static SheetCell ToCell(IXLCell cell)
    {
        // สูตรใช้ผลลัพธ์ที่ Excel คำนวณเก็บไว้ ไม่ประมวลสูตรเอง (สูตรอาจอ้างไฟล์อื่นหรือใช้ฟังก์ชันที่ไม่รองรับ)
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        if (cell.HasFormula && value.IsBlank)
            return new SheetCell(CellKind.Error, "เป็นสูตรที่ยังไม่มีผลลัพธ์ ให้เปิดไฟล์ใน Excel แล้วกดบันทึกใหม่");

        return value.Type switch
        {
            XLDataType.Blank => SheetCell.Blank,
            XLDataType.Text => SheetCell.Of(value.GetText()),
            XLDataType.Number => SheetCell.Of(value.GetNumber()),
            XLDataType.Boolean => new SheetCell(CellKind.Boolean, value.GetBoolean() ? "TRUE" : "FALSE"),
            XLDataType.DateTime or XLDataType.TimeSpan => new SheetCell(CellKind.Date, cell.GetFormattedString()),
            _ => new SheetCell(CellKind.Error, value.ToString()),
        };
    }
}
