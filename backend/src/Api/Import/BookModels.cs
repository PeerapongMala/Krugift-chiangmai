namespace Api.Import;

// ชนิดข้อมูลของ "ไฟล์ครู" — ไฟล์จริงที่ครูใช้อยู่ 1 ไฟล์มีหลายชีท ชีทละห้อง
// ในชีทเดียวมีทั้งรายชื่อนักเรียนและคะแนนทุกรายการ หัวตารางซ้อนกันหลายชั้นและมีช่อง merge

/// ชีทหนึ่งชีทแบบดิบ · Rows เรียงตามแถวจริง (แถว 1 อยู่ตัวแรก) ช่อง merge ถูกเติมค่าให้ครบทุกช่องแล้ว
public record BookSheet(string Name, IReadOnlyList<SheetRow> Rows)
{
    /// rowNumber นับแบบ Excel (เริ่มที่ 1) · column นับจาก 0 (A = 0)
    public SheetCell Cell(int rowNumber, int column) =>
        rowNumber >= 1 && rowNumber <= Rows.Count ? Rows[rowNumber - 1][column] : SheetCell.Blank;

    public string Text(int rowNumber, int column) => Cells.HeaderText(Cell(rowNumber, column));
}

/// นักเรียนหนึ่งคนที่อ่านได้จากชีท
public record BookStudent(int Row, int No, string Code, string Title, string FirstName, string LastName, string Nickname);

/// รายการคะแนนหนึ่งคอลัมน์ · TeacherOnly = คะแนนดิบที่ครูดูคนเดียว เด็กไม่เห็น
public record BookItem(int Column, string Name, decimal MaxScore, bool TeacherOnly = false);

/// คะแนนหนึ่งช่อง · ItemIndex ชี้ไปที่ลำดับใน Items ของชีทเดียวกัน
public record BookScore(int Row, int ItemIndex, decimal Value);

/// แผนของชีทเดียว = ห้องเรียนหนึ่งห้อง
public record BookSheetPlan(
    string SheetName,
    string ClassroomName,
    IReadOnlyList<BookStudent> Students,
    IReadOnlyList<BookItem> Items,
    IReadOnlyList<BookScore> Scores);

/// แผนรวมของทั้งไฟล์ · นำเข้าทีเดียวทุกชีทที่ครูเลือก ผิดชีทเดียวไม่บันทึกอะไรเลย
public record BookPlan(IReadOnlyList<BookSheetPlan> Sheets) : IImportPlan
{
    public IReadOnlyList<string> Summary =>
        Sheets.Select(s =>
            $"{s.ClassroomName} (ชีท {s.SheetName}): นักเรียน {s.Students.Count} คน รายการคะแนน {s.Items.Count} รายการ คะแนน {s.Scores.Count} ช่อง").ToList();

    public IReadOnlyList<ImportChange> Changes =>
        Sheets.SelectMany(s => s.Students.Select(student =>
            new ImportChange(student.Row, $"{s.ClassroomName} เลขที่ {student.No}",
                $"{student.Title}{student.FirstName} {student.LastName} ({student.Code})"))).ToList();

    public bool HasChanges => Sheets.Any(s => s.Students.Count > 0);
}
