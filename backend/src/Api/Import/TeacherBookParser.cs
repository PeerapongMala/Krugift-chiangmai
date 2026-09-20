using System.Text.RegularExpressions;
using Api.Common;

namespace Api.Import;

/// <summary>
/// อ่านชีทห้องเรียนจากไฟล์ครู · ไม่แตะ DB ไม่ผูกกับ ClosedXML จึงเทสต์ได้ด้วยข้อมูลในโค้ด
///
/// รูปแบบชีทของครู (หาเอาจากข้อมูล ไม่ฟิกซ์เลขแถว เผื่อครูแทรกแถวเพิ่ม):
///   แถวบนสุด        ชื่อห้อง เช่น "รายชื่อนักเรียนชั้นมัธยมศึกษาปีที่ 1/1 ปีการศึกษา 2569"
///   แถวหัวตาราง      ช่องที่เขียนว่า เลขที่ / ชื่อ - นามสกุล / ชื่อเล่น / รหัสนักเรียน และคะแนนเต็มของคอลัมน์คะแนน
///   2 แถวเหนือหัว    ชื่อกลุ่มคะแนนกับชื่อย่อย (merge ไว้) เอามาต่อกันเป็นชื่อรายการ
///   ถัดจากหัวตาราง   ข้อมูลนักเรียนทีละแถว
/// </summary>
public static partial class TeacherBookParser
{
    /// หัวตารางอยู่ไม่เกินแถวนี้ · ไฟล์ครูมีหัวเรื่อง 5 แถว เผื่อไว้เยอะแล้ว
    const int MaxHeaderRow = 20;

    const string NicknameHeader = "ชื่อเล่น";

    /// หัวคอลัมน์ที่ถือว่าเป็นการสอบย่อย เช่น "สอบ 1" "สอบ 2"
    const string ExamWord = "สอบ";

    /// สอบใหญ่ที่ครูเขียนหัวเป็นภาษาอังกฤษ · เก็บเป็นชื่อไทยให้เด็กอ่านเข้าใจ
    static readonly Dictionary<string, string> BigExams = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Midterm"] = "สอบกลางภาค",
        ["Final"] = "สอบปลายภาค",
    };

    /// "...ชั้นมัธยมศึกษาปีที่ 1/1 ปีการศึกษา 2569" → ม.1/1
    [GeneratedRegex(@"ปีที่\s*(\d+)\s*/\s*(\d+)")]
    private static partial Regex ClassroomName();

    /// ชีทที่ไม่มีหัวตาราง (เช่น ชีทสรุปคะแนนรวม) ไม่ใช่ห้องเรียน ข้ามได้เลย ไม่ใช่จุดผิด
    public static bool IsRoomSheet(BookSheet sheet) => FindHeaderRow(sheet) != 0;

    public static BookSheetPlan? Parse(BookSheet sheet, ImportErrors errors)
    {
        var headerRow = FindHeaderRow(sheet);
        if (headerRow == 0)
        {
            errors.File($"ชีท {sheet.Name}: ไม่พบหัวตาราง ต้องมีช่องที่เขียนว่า “{Cells.NoHeader}” และ “{Cells.CodeHeader}”");
            return null;
        }

        if (FindStudentColumns(sheet, headerRow) is not { } columns)
        {
            errors.File($"ชีท {sheet.Name}: หัวตารางไม่ครบ ต้องมี {Cells.NoHeader}, {Cells.CodeHeader} และช่องชื่อ-นามสกุล");
            return null;
        }

        var items = RaiseMaxScores(sheet, headerRow, columns, ReadItems(sheet, headerRow, columns, errors));
        var (students, scores) = ReadRows(sheet, headerRow, columns, items, errors);

        if (students.Count == 0) errors.File($"ชีท {sheet.Name}: ไม่พบข้อมูลนักเรียน");

        return new BookSheetPlan(sheet.Name, ReadClassroomName(sheet, headerRow), students, items, scores);
    }

    /// แถวหัวตาราง = แถวที่มีทั้ง "เลขที่" และ "รหัสนักเรียน"
    static int FindHeaderRow(BookSheet sheet)
    {
        for (var r = 1; r <= Math.Min(MaxHeaderRow, sheet.Rows.Count); r++)
        {
            var texts = Texts(sheet, r);
            if (texts.Contains(Cells.NoHeader) && texts.Contains(Cells.CodeHeader)) return r;
        }
        return 0;
    }

    static List<string> Texts(BookSheet sheet, int row) =>
        row >= 1 && row <= sheet.Rows.Count
            ? sheet.Rows[row - 1].Cells.Select(Cells.HeaderText).ToList()
            : [];

    readonly record struct StudentColumns(int No, int Code, int Title, int First, int Last, int Nickname, int LastStudentColumn);

    /// <summary>
    /// ช่องชื่อของครู merge ไว้ 3 คอลัมน์ (คำนำหน้า · ชื่อ · นามสกุล) แต่เขียนหัวไว้ช่องเดียวว่า "ชื่อ - นามสกุล"
    /// จึงดูจากจำนวนคอลัมน์ที่ merge: 3 คอลัมน์ = มีคำนำหน้า · 2 คอลัมน์ = ชื่อกับนามสกุล
    /// </summary>
    static StudentColumns? FindStudentColumns(BookSheet sheet, int headerRow)
    {
        var texts = Texts(sheet, headerRow);
        var no = texts.IndexOf(Cells.NoHeader);
        var code = texts.IndexOf(Cells.CodeHeader);
        if (no < 0 || code < 0) return null;

        var nameStart = texts.FindIndex(t => t.Contains(Cells.FirstNameHeader) && t.Contains(Cells.LastNameHeader));
        if (nameStart < 0) return null;

        var nameEnd = nameStart;
        while (nameEnd + 1 < texts.Count && texts[nameEnd + 1] == texts[nameStart]) nameEnd++;

        var (title, first, last) = nameEnd - nameStart >= 2
            ? (nameStart, nameStart + 1, nameStart + 2)
            : (-1, nameStart, nameEnd);

        var nickname = texts.IndexOf(NicknameHeader);
        return new StudentColumns(no, code, title, first, last, nickname, new[] { no, code, last, nickname }.Max());
    }

    static string ReadClassroomName(BookSheet sheet, int headerRow)
    {
        for (var r = 1; r < headerRow; r++)
            foreach (var text in Texts(sheet, r))
                if (ClassroomName().Match(text) is { Success: true } m)
                    return $"ม.{m.Groups[1].Value}/{m.Groups[2].Value}";

        // ไม่เจอชื่อห้องในหัวเรื่อง ใช้ชื่อชีทแทน ครูเปลี่ยนชื่อห้องในเว็บทีหลังได้
        return sheet.Name;
    }

    /// <summary>
    /// คอลัมน์ที่นำเข้า: เอาเฉพาะคอลัมน์สอบ และการสอบหนึ่งครั้งในไฟล์ครูมี 2 คอลัมน์คู่กัน
    ///   คอลัมน์แรก   = คะแนนดิบ เช่น สอบ 1 เต็ม 45 → เก็บไว้ให้ครูดูคนเดียว
    ///   คอลัมน์ถัดไป = คะแนนที่หารเป็นคะแนนเก็บแล้ว เต็ม 10 → อันนี้คือที่เด็กเห็น
    /// คอลัมน์หารซ้ำอันที่สาม (เช่น คอลัมน์ I ที่หารด้วย 4) ไม่เอา ครูบอกว่าใช้อันแรก
    /// </summary>
    static List<BookItem> ReadItems(BookSheet sheet, int headerRow, StudentColumns columns, ImportErrors errors)
    {
        var items = new List<BookItem>();
        var used = new HashSet<string>();
        var width = sheet.Rows.Count > 0 ? sheet.Rows.Max(r => r.Cells.Count) : 0;

        for (var c = columns.LastStudentColumn + 1; c < width; c++)
        {
            if (MaxScoreAt(sheet, headerRow, c, errors) is not { } rawMax) continue;
            if (ExamName(sheet, headerRow, c) is not { } name) continue;

            var scaledColumn = c + 1;
            var scaledMax = MaxScoreAt(sheet, headerRow, scaledColumn, errors);
            var hasScaled = scaledMax is not null && ExamName(sheet, headerRow, scaledColumn) is null;

            // ชื่อหลักเป็นของคอลัมน์ที่เด็กเห็น ส่วนคะแนนดิบต่อท้ายชื่อไว้ว่าเป็นคะแนนดิบ
            if (hasScaled)
            {
                items.Add(new BookItem(scaledColumn, UniqueName(name, Cells.ColumnLetter(scaledColumn), used), scaledMax!.Value));
                items.Add(new BookItem(c, UniqueName($"{name} (คะแนนดิบ)", Cells.ColumnLetter(c), used), rawMax, TeacherOnly: true));
                c = scaledColumn;
            }
            else
            {
                items.Add(new BookItem(c, UniqueName(name, Cells.ColumnLetter(c), used), rawMax));
            }

            if (items.Count > Limits.ImportMaxItems)
            {
                errors.File($"ชีท {sheet.Name}: คอลัมน์คะแนนเกิน {Limits.ImportMaxItems} รายการ");
                break;
            }
        }

        if (items.Count == 0)
            errors.File($"ชีท {sheet.Name}: ไม่พบคอลัมน์คะแนนสอบ คอลัมน์สอบต้องมีคำว่า สอบ / Midterm / Final อยู่หัวตาราง และมีคะแนนเต็มเป็นตัวเลข");

        return items;
    }

    /// คะแนนเต็มที่หัวตารางของคอลัมน์นี้ · คืน null ถ้าไม่ใช่ตัวเลข (แปลว่าไม่ใช่คอลัมน์คะแนน)
    static decimal? MaxScoreAt(BookSheet sheet, int headerRow, int column, ImportErrors errors)
    {
        var cell = sheet.Cell(headerRow, column);
        if (cell.Kind != CellKind.Number) return null;

        var maxScore = Math.Round((decimal)cell.Number, 2);
        if (Validate.MaxScore(maxScore) is { } error)
        {
            errors.Cell(headerRow, Cells.ColumnLetter(column), error);
            return null;
        }
        return maxScore;
    }

    /// <summary>
    /// คอลัมน์ที่ครูใส่สูตรแปลงคะแนนบางทีให้ค่าเกินคะแนนเต็มที่เขียนไว้บนหัว (เช่น หัวว่าเต็ม 10 แต่คำนวณได้ 10.5)
    /// ใช้ค่าที่มากที่สุดในคอลัมน์นั้นเป็นคะแนนเต็มแทน ครูจะได้ไม่ต้องแก้ไฟล์ทุกครั้งที่นำเข้า
    /// </summary>
    static List<BookItem> RaiseMaxScores(BookSheet sheet, int headerRow, StudentColumns columns, List<BookItem> items)
    {
        var rows = DataRowNumbers(sheet, headerRow, columns).ToList();

        return items.Select(item =>
        {
            var highest = item.MaxScore;
            foreach (var r in rows)
            {
                var cell = Round2(sheet.Cell(r, item.Column));
                if (cell.Kind == CellKind.Number && (decimal)cell.Number > highest) highest = (decimal)cell.Number;
            }
            return highest > item.MaxScore && Validate.MaxScore(highest) is null ? item with { MaxScore = highest } : item;
        }).ToList();
    }

    /// แถวนักเรียนในตาราง · ใช้ร่วมกันทั้งตอนหาคะแนนเต็มจริงและตอนอ่านข้อมูล จะได้หยุดที่ท้ายตารางเหมือนกัน
    static IEnumerable<int> DataRowNumbers(BookSheet sheet, int headerRow, StudentColumns columns)
    {
        for (var r = headerRow + 1; r <= sheet.Rows.Count; r++)
        {
            var noCell = sheet.Cell(r, columns.No);
            var codeCell = sheet.Cell(r, columns.Code);

            // แถวว่าง = แถวคั่นที่ครูเว้นไว้ ข้ามไป
            if (noCell.Kind == CellKind.Blank && codeCell.Kind == CellKind.Blank) continue;

            // ท้ายตาราง: ใต้รายชื่อครูเขียนหมายเหตุไว้ (เกณฑ์แบบฝึกหัด จิตพิสัย ฯลฯ) ช่องเลขที่เป็นข้อความและไม่มีรหัสนักเรียน
            // ถ้ามีรหัสนักเรียนแต่เลขที่ผิด ยังถือว่าเป็นแถวนักเรียนและดักเป็นจุดผิดตามเดิม
            if (codeCell.Kind == CellKind.Blank && Cells.No(noCell).Value is null) yield break;

            yield return r;
        }
    }

    /// <summary>
    /// ชื่อรายการของคอลัมน์สอบ · คืน null ถ้าไม่ใช่คอลัมน์สอบ (ครูขอเก็บเฉพาะคะแนนสอบ)
    ///   "เก็บ 1 (25 คะแนน) / จำนวนเต็ม" + "สอบ 1" → "จำนวนเต็ม สอบ 1"
    ///   "Midterm" → "สอบกลางภาค" · "Final" → "สอบปลายภาค"
    /// </summary>
    static string? ExamName(BookSheet sheet, int headerRow, int column)
    {
        var group = Cells.Normalize(sheet.Text(headerRow - 2, column));
        var sub = Cells.Normalize(sheet.Text(headerRow - 1, column));

        if (BigExams.TryGetValue(group, out var bigExam))
            // Midterm/Final merge คร่อมคอลัมน์คะแนนดิบกับคอลัมน์ที่คิดจากสูตร เอาเฉพาะคอลัมน์แรกของกรอบ
            return Cells.Normalize(sheet.Text(headerRow - 2, column - 1)) == group ? null : bigExam;

        if (!sub.StartsWith(ExamWord, StringComparison.Ordinal)) return null;

        // ชื่อกลุ่มของครูเขียนว่า "เก็บ 1 (25 คะแนน) / ชื่อเรื่อง" · เอาเฉพาะชื่อเรื่องหลังเครื่องหมาย /
        var topic = group.Contains('/') ? Cells.Normalize(group[(group.IndexOf('/') + 1)..]) : "";
        var name = topic == "" ? sub : $"{topic} {sub}";
        return name.Length > Limits.NameLength ? name[..Limits.NameLength] : name;
    }

    /// ชื่อรายการซ้ำกันไม่ได้ (คอลัมน์ที่คำนวณจากคะแนนดิบมักไม่มีหัวของตัวเอง) ต่อท้ายด้วยชื่อคอลัมน์
    static string UniqueName(string name, string columnLetter, HashSet<string> used)
    {
        if (used.Add(name)) return name;
        var withColumn = $"{name} (คอลัมน์ {columnLetter})";
        used.Add(withColumn);
        return withColumn;
    }

    static (List<BookStudent> Students, List<BookScore> Scores) ReadRows(
        BookSheet sheet, int headerRow, StudentColumns columns, List<BookItem> items, ImportErrors errors)
    {
        var students = new List<BookStudent>();
        var scores = new List<BookScore>();
        var seenNo = new Dictionary<int, int>();
        var seenCode = new Dictionary<string, int>();

        foreach (var r in DataRowNumbers(sheet, headerRow, columns))
        {
            var student = ReadStudent(sheet, r, columns, errors);
            if (student is null) continue;

            if (seenNo.TryGetValue(student.No, out var firstNoRow))
                errors.Cell(r, Cells.ColumnLetter(columns.No), $"เลขที่ {student.No} ซ้ำกับแถว {firstNoRow}");
            else seenNo[student.No] = r;

            if (seenCode.TryGetValue(student.Code, out var firstCodeRow))
                errors.Cell(r, Cells.ColumnLetter(columns.Code), $"รหัสนักเรียน {student.Code} ซ้ำกับแถว {firstCodeRow}");
            else seenCode[student.Code] = r;

            students.Add(student);
            ReadScores(sheet, r, items, students.Count - 1, scores, errors);
        }

        return (students, scores);
    }

    static BookStudent? ReadStudent(BookSheet sheet, int row, StudentColumns columns, ImportErrors errors)
    {
        var (no, noError) = Cells.No(sheet.Cell(row, columns.No));
        if (noError is not null) errors.Cell(row, Cells.ColumnLetter(columns.No), noError);

        var (code, codeError) = Cells.Code(sheet.Cell(row, columns.Code));
        if (codeError is not null) errors.Cell(row, Cells.ColumnLetter(columns.Code), codeError);

        var (first, firstError) = Cells.Name(sheet.Cell(row, columns.First), Cells.FirstNameHeader);
        if (firstError is not null) errors.Cell(row, Cells.ColumnLetter(columns.First), firstError);

        var (last, lastError) = Cells.Name(sheet.Cell(row, columns.Last), Cells.LastNameHeader);
        if (lastError is not null) errors.Cell(row, Cells.ColumnLetter(columns.Last), lastError);

        var title = Optional(sheet, row, columns.Title, "คำนำหน้า", errors);
        var nickname = Optional(sheet, row, columns.Nickname, NicknameHeader, errors);

        return no is null || code is null || first is null || last is null
            ? null
            : new BookStudent(row, no.Value, code, title, first, last, nickname);
    }

    /// ช่องที่ว่างได้ · ว่างไม่เป็นจุดผิด แต่ยาวเกินยังต้องดัก
    static string Optional(BookSheet sheet, int row, int column, string what, ImportErrors errors)
    {
        if (column < 0) return "";
        var cell = sheet.Cell(row, column);
        if (cell.Kind == CellKind.Blank) return "";

        var text = Cells.HeaderText(cell);
        if (Validate.OptionalName(text, what) is { } error)
        {
            errors.Cell(row, Cells.ColumnLetter(column), error);
            return "";
        }
        return text;
    }

    /// คอลัมน์ที่ครูใส่สูตรแปลงคะแนน (เช่น 32.5 → 7.222222222) มีทศนิยมยาวกว่าที่ DB เก็บได้
    /// ปัดให้เหลือ 2 ตำแหน่งตั้งแต่ตอนอ่าน ไม่ถือเป็นจุดผิด เพราะเป็นค่าที่ Excel คำนวณเองไม่ใช่ครูพิมพ์ผิด
    static SheetCell Round2(SheetCell cell) =>
        cell.Kind == CellKind.Number ? SheetCell.Of((double)Math.Round((decimal)cell.Number, 2)) : cell;

    static void ReadScores(BookSheet sheet, int row, List<BookItem> items, int studentIndex, List<BookScore> scores, ImportErrors errors)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var cell = sheet.Cell(row, items[i].Column);
            if (cell.Kind == CellKind.Blank) continue;

            var (value, error) = Cells.Score(Round2(cell), items[i].MaxScore);
            if (error is not null)
            {
                errors.Cell(row, Cells.ColumnLetter(items[i].Column), error);
                continue;
            }
            if (value is null) continue;

            scores.Add(new BookScore(studentIndex, i, value.Value));
        }
    }
}
