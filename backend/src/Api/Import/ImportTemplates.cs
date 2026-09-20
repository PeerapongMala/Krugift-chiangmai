using Api.Common;
using ClosedXML.Excel;

namespace Api.Import;

/// <summary>
/// ไฟล์ Excel ของห้องหนึ่งห้องพร้อมข้อมูลปัจจุบัน ใช้ตอนครูกดส่งออก
/// ขาออกอย่างเดียว การนำเข้ามีทางเดียวคือไฟล์ของครูทั้งภาคเรียน
/// </summary>
public static class ImportTemplates
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// ตั้ง data validation และรูปแบบข้อความไว้ล่วงหน้าเท่ากับเพดานแถวที่นำเข้าได้
    const int PreparedLastRow = Limits.ImportMaxRows + 1;

    const int FirstItemColumn = 5;

    /// หัวตารางของไฟล์ที่ส่งออก · เรียงเหมือนที่ครูคุ้นจากไฟล์ของตัวเอง
    static readonly string[] Headers = [Cells.NoHeader, Cells.CodeHeader, Cells.FirstNameHeader, Cells.LastNameHeader];

    /// หัวคอลัมน์คะแนนใส่คะแนนเต็มไว้ในวงเล็บ ครูจะได้รู้ว่าเต็มเท่าไรโดยไม่ต้องเปิดเว็บเทียบ
    static string ItemHeader(string name, decimal maxScore) => $"{name} ({Cells.Format(maxScore)})";

    /// ไฟล์เดียวของห้องหนึ่งห้อง 2 ชีท: รายชื่อนักเรียน กับ ตารางคะแนน
    /// ครูกดส่งออกครั้งเดียวได้ครบ ไม่ต้องเลือกว่าจะเอาอะไร
    public static byte[] Classroom(IReadOnlyList<ExistingItem> items, IReadOnlyList<ExistingEnrollment> students,
        IReadOnlyDictionary<(int ItemId, int StudentId), decimal?> scores)
    {
        using var workbook = new XLWorkbook();

        var roster = StartSheet(workbook, "นักเรียน", Headers);
        WriteStudents(roster, students, (_, _) => { });

        var headers = Headers.Concat(items.Select(i => ItemHeader(i.Name, i.MaxScore))).ToList();
        var sheet = StartSheet(workbook, "คะแนน", headers);

        WriteStudents(sheet, students, (row, student) =>
        {
            for (var i = 0; i < items.Count; i++)
                if (scores.GetValueOrDefault((items[i].Id, student.StudentId)) is { } value)
                    sheet.Cell(row, FirstItemColumn + i).SetValue((double)value);
        });

        // ดักตั้งแต่ตอนพิมพ์ใน Excel · server ยังตรวจซ้ำทุกครั้ง เพราะไฟล์แก้นอก Excel ได้
        for (var i = 0; i < items.Count; i++)
        {
            var column = FirstItemColumn + i;
            var rule = sheet.Range(2, column, PreparedLastRow, column).CreateDataValidation();
            rule.Decimal.Between(0, (double)items[i].MaxScore);
            rule.ErrorTitle = "คะแนนไม่ถูกต้อง";
            rule.ErrorMessage = $"ใส่ตัวเลข 0 ถึง {Cells.Format(items[i].MaxScore)} ปล่อยว่าง = ไม่เปลี่ยนคะแนนเดิม";
            sheet.Column(column).Width = Math.Clamp(headers[column - 1].Length + 4, 14, 40);
        }

        return Save(workbook);
    }

    static IXLWorksheet StartSheet(XLWorkbook workbook, string title, IReadOnlyList<string> headers)
    {
        var sheet = workbook.AddWorksheet(title);
        for (var c = 0; c < headers.Count; c++) sheet.Cell(1, c + 1).SetValue(headers[c]);

        var header = sheet.Range(1, 1, 1, headers.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F6E7DC");
        sheet.SheetView.FreezeRows(1);

        // รหัสนักเรียนเป็นข้อความ ไม่งั้น Excel ตัดเลข 0 ข้างหน้าทิ้ง (01234 → 1234)
        sheet.Range(2, 2, PreparedLastRow, 2).Style.NumberFormat.Format = "@";

        var noRule = sheet.Range(2, 1, PreparedLastRow, 1).CreateDataValidation();
        noRule.WholeNumber.Between(1, Limits.MaxStudentNo);
        noRule.ErrorTitle = "เลขที่ไม่ถูกต้อง";
        noRule.ErrorMessage = $"ใส่จำนวนเต็ม 1 ถึง {Limits.MaxStudentNo}";

        sheet.Column(1).Width = 8;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 18;
        sheet.Column(4).Width = 18;
        return sheet;
    }

    static void WriteStudents(IXLWorksheet sheet, IReadOnlyList<ExistingEnrollment> students, Action<int, ExistingEnrollment> extra)
    {
        var row = 2;
        foreach (var student in students.OrderBy(s => s.No))
        {
            sheet.Cell(row, 1).SetValue(student.No);
            sheet.Cell(row, 2).SetValue(student.Code);
            sheet.Cell(row, 3).SetValue(student.FirstName);
            sheet.Cell(row, 4).SetValue(student.LastName);
            extra(row, student);
            row++;
        }
    }

    static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
