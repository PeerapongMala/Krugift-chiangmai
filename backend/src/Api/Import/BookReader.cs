using Api.Common;
using ClosedXML.Excel;

namespace Api.Import;

/// <summary>
/// อ่านไฟล์ครูทั้งไฟล์เป็นชีทดิบ ๆ · ด่านระดับไฟล์ใช้ตัวเดียวกับ SheetReader (ขนาด zip bomb ไฟล์เสีย)
/// ต่างกันตรงที่อ่านทุกชีทที่มองเห็นได้ ไม่ตัดหัวตาราง และเติมค่าช่อง merge ให้ทุกช่องในกรอบ
/// เพราะหัวตารางของครู merge ไว้หลายชั้น ถ้าไม่เติมจะอ่านได้แค่ช่องซ้ายบนช่องเดียว
/// </summary>
public static class BookReader
{
    /// ไฟล์ครูกว้างกว่าไฟล์ตัวอย่างของเรามาก (คอลัมน์คะแนน + คอลัมน์แบบฝึกหัด + คอลัมน์ว่างคั่น)
    const int MaxColumns = 200;

    public static (IReadOnlyList<BookSheet>? Sheets, string? Error) Read(byte[] bytes)
    {
        if (bytes.Length == 0) return (null, "ไฟล์ว่างเปล่า");
        if (bytes.Length > Limits.ImportMaxBytes) return (null, SheetReader.TooLarge);
        if (SheetReader.CheckZip(bytes) is { } zipError) return (null, zipError);

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(bytes, writable: false));
        }
        catch (Exception)
        {
            return (null, SheetReader.NotXlsx);
        }

        using (workbook)
        {
            var sheets = new List<BookSheet>();
            foreach (var worksheet in workbook.Worksheets.Where(w => w.Visibility == XLWorksheetVisibility.Visible))
            {
                var (sheet, error) = ReadSheet(worksheet);
                if (error is not null) return (null, error);
                if (sheet is not null) sheets.Add(sheet);
            }

            return sheets.Count == 0
                ? (null, "ไม่พบแผ่นงาน (sheet) ที่มีข้อมูลในไฟล์")
                : (sheets, null);
        }
    }

    static (BookSheet? Sheet, string? Error) ReadSheet(IXLWorksheet worksheet)
    {
        var lastRow = worksheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;
        var lastColumn = worksheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0;
        if (lastRow == 0 || lastColumn == 0) return (null, null);

        if (lastRow > Limits.ImportMaxRows)
            return (null, $"ชีท {worksheet.Name} มีข้อมูลเกิน {Limits.ImportMaxRows} แถว");
        if (lastColumn > MaxColumns)
            return (null, $"ชีท {worksheet.Name} มีคอลัมน์เกิน {MaxColumns} คอลัมน์");

        var rows = new List<SheetRow>(lastRow);
        for (var r = 1; r <= lastRow; r++)
        {
            var cells = new List<SheetCell>(lastColumn);
            for (var c = 1; c <= lastColumn; c++) cells.Add(SheetReader.ToCell(worksheet.Cell(r, c)));
            rows.Add(new SheetRow(r, cells));
        }

        FillMerges(worksheet, rows, lastRow, lastColumn);
        return (new BookSheet(worksheet.Name, rows), null);
    }

    /// ค่าของกรอบ merge อยู่ที่ช่องซ้ายบนช่องเดียว · เติมให้ทุกช่องในกรอบ คอลัมน์กลางกรอบจะได้รู้ว่าอยู่กลุ่มไหน
    static void FillMerges(IXLWorksheet worksheet, List<SheetRow> rows, int lastRow, int lastColumn)
    {
        foreach (var range in worksheet.MergedRanges)
        {
            var value = SheetReader.ToCell(range.FirstCell());
            if (value.Kind == CellKind.Blank) continue;

            for (var r = range.FirstRow().RowNumber(); r <= Math.Min(range.LastRow().RowNumber(), lastRow); r++)
            for (var c = range.FirstColumn().ColumnNumber(); c <= Math.Min(range.LastColumn().ColumnNumber(), lastColumn); c++)
            {
                var cells = (List<SheetCell>)rows[r - 1].Cells;
                cells[c - 1] = value;
            }
        }
    }
}
