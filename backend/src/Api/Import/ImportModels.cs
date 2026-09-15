using Api.Common;

namespace Api.Import;

// ชนิดข้อมูลกลางของการนำเข้า Excel · ไม่ผูกกับ ClosedXML และไม่ผูกกับ EF
// parser จึงเทสต์ได้ด้วยข้อมูลที่สร้างในโค้ด ไม่ต้องมีไฟล์ Excel หรือ DB

public enum CellKind { Blank, Text, Number, Date, Boolean, Error }

/// ค่าในช่องหนึ่งช่องตามที่ Excel เก็บไว้ · Text ของ Date/Error คือข้อความที่ Excel แสดง
public readonly record struct SheetCell(CellKind Kind, string Text = "", double Number = 0)
{
    public static readonly SheetCell Blank = new(CellKind.Blank);

    /// ช่องที่มีแต่ช่องว่างถือว่าว่าง แถวที่ครูเผลอเคาะ space ไว้จะได้ไม่กลายเป็นจุดผิด
    public static SheetCell Of(string text) => string.IsNullOrWhiteSpace(text) ? Blank : new(CellKind.Text, text);

    public static SheetCell Of(double number) => new(CellKind.Number, "", number);
}

/// แถวข้อมูลหนึ่งแถว · RowNumber คือเลขแถวใน Excel (แถวหัวตาราง = 1) ไว้บอกครูว่าผิดแถวไหน
public record SheetRow(int RowNumber, IReadOnlyList<SheetCell> Cells)
{
    public SheetCell this[int column] => column < Cells.Count ? Cells[column] : SheetCell.Blank;
}

public record Sheet(IReadOnlyList<SheetCell> Header, IReadOnlyList<SheetRow> Rows);

/// จุดผิดหนึ่งจุด · Row เป็น null เมื่อเป็นปัญหาของหัวตารางหรือทั้งไฟล์
public record ImportError(int? Row, string? Column, string Message);

/// สิ่งที่จะเปลี่ยนหนึ่งอย่าง แสดงให้ครูตรวจก่อนกดยืนยัน
public record ImportChange(int Row, string Label, string Detail);

/// แผนการเปลี่ยนแปลงที่ผ่านการตรวจแล้ว · หน้าเว็บแสดงผลได้แบบเดียวกันทั้งไฟล์นักเรียนและไฟล์คะแนน
public interface IImportPlan
{
    IReadOnlyList<string> Summary { get; }
    IReadOnlyList<ImportChange> Changes { get; }
    bool HasChanges { get; }
}

/// <param name="Errors">จุดผิดเรียงตามแถว ตัดไว้ที่ Limits.ImportMaxErrors</param>
/// <param name="ErrorCount">จำนวนจุดผิดทั้งหมดก่อนตัด</param>
/// <param name="Plan">มีค่าก็ต่อเมื่อไม่มีจุดผิดเลย (all-or-nothing)</param>
public record ImportResult<TPlan>(IReadOnlyList<ImportError> Errors, int ErrorCount, TPlan? Plan)
    where TPlan : class, IImportPlan
{
    public bool IsValid => ErrorCount == 0;
}

/// รวบรวมจุดผิดทุกจุดในรอบเดียว ครูจะได้แก้ครบทีเดียว ไม่ต้องอัปโหลดวนทีละจุด
public sealed class ImportErrors
{
    readonly List<ImportError> all = [];

    public int Count => all.Count;

    public void File(string message, string? column = null) => all.Add(new ImportError(null, column, message));

    public void Cell(int row, string column, string message) => all.Add(new ImportError(row, column, message));

    /// แผนจะถูกทิ้งถ้ามีจุดผิดแม้แต่จุดเดียว
    public ImportResult<TPlan> Result<TPlan>(TPlan? plan) where TPlan : class, IImportPlan
    {
        // OrderBy ของ LINQ เรียงแบบคงลำดับเดิม จุดผิดในแถวเดียวกันจะเรียงตามคอลัมน์ที่ตรวจ
        var sorted = all.OrderBy(e => e.Row ?? 0).ToList();
        return new ImportResult<TPlan>(sorted.Take(Limits.ImportMaxErrors).ToList(), sorted.Count, sorted.Count == 0 ? plan : null);
    }
}

// ---- ข้อมูลของห้องในระบบตอนนี้ (endpoint โหลดมาให้ parser)

public record ExistingItem(int Id, string Name, decimal MaxScore);

public record ExistingStudent(int Id, string Code, string FirstName, string LastName);

public record ExistingEnrollment(int StudentId, int No, string Code, string FirstName, string LastName);
