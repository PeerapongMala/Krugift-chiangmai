using Api.Import;

namespace Api.Tests;

/// สร้าง Sheet ในโค้ดสำหรับเทสต์ parser โดยไม่ต้องมีไฟล์ Excel จริง
static class SheetBuilder
{
    public static SheetCell Cell(object? value) => value switch
    {
        null => SheetCell.Blank,
        string text => SheetCell.Of(text),
        int number => SheetCell.Of(number),
        double number => SheetCell.Of(number),
        DateTime date => new SheetCell(CellKind.Date, date.ToString("d/M/yyyy")),
        bool flag => new SheetCell(CellKind.Boolean, flag ? "TRUE" : "FALSE"),
        _ => throw new ArgumentException($"ไม่รองรับค่าชนิด {value.GetType()}"),
    };

    /// แถวข้อมูลเริ่มที่แถว 2 ของ Excel เหมือนไฟล์จริง
    public static Sheet Sheet(object?[] header, params object?[][] rows) => new(
        header.Select(Cell).ToList(),
        rows.Select((cells, i) => new SheetRow(i + 2, cells.Select(Cell).ToList())).ToList());
}
