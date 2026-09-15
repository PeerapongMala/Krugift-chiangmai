namespace Api.Import;

/// <param name="StudentsByCode">
/// นักเรียนทั้งโรงเรียนที่รหัสตรงกับในไฟล์ · รหัส unique ทั้งโรงเรียน ถ้าหาแค่ในห้องนี้จะสร้างเด็กคนเดิมซ้ำ
/// </param>
public record StudentSnapshot(
    IReadOnlyList<ExistingEnrollment> Enrollments,
    IReadOnlyDictionary<string, ExistingStudent> StudentsByCode);

/// <param name="StudentId">null = นักเรียนใหม่ที่ยังไม่มีในระบบ</param>
/// <param name="OldNo">เลขที่เดิมในห้องนี้ · null = ยังไม่ได้อยู่ในห้องนี้</param>
public record PlannedStudent(int Row, string Code, string FirstName, string LastName, int No, int? StudentId, int? OldNo)
{
    public bool IsNew => StudentId is null;

    public bool JoinsClassroom => OldNo is null;

    public bool IsRenumbered => OldNo is { } old && old != No;
}

public record StudentPlan(IReadOnlyList<PlannedStudent> Students) : IImportPlan
{
    public bool HasChanges => Students.Any(s => s.JoinsClassroom || s.IsRenumbered);

    public IReadOnlyList<string> Summary
    {
        get
        {
            var lines = new List<string> { $"นักเรียนในไฟล์ {Students.Count} คน" };
            AddCount(lines, Students.Count(s => s.IsNew), "เพิ่มนักเรียนใหม่เข้าระบบ");
            AddCount(lines, Students.Count(s => !s.IsNew && s.JoinsClassroom), "เพิ่มนักเรียนที่มีในระบบแล้วเข้าห้องนี้");
            AddCount(lines, Students.Count(s => s.IsRenumbered), "เปลี่ยนเลขที่");
            if (!HasChanges) lines.Add("ไม่มีอะไรต่างจากในระบบ");
            return lines;
        }
    }

    public IReadOnlyList<ImportChange> Changes => Students
        .Where(s => s.JoinsClassroom || s.IsRenumbered)
        .Select(s => new ImportChange(s.Row, $"{s.FirstName} {s.LastName} ({s.Code})",
            s.IsNew ? $"นักเรียนใหม่ · เลขที่ {s.No}"
            : s.JoinsClassroom ? $"เข้าห้องนี้ · เลขที่ {s.No}"
            : $"เลขที่ {s.OldNo} → {s.No}"))
        .ToList();

    static void AddCount(List<string> lines, int count, string what)
    {
        if (count > 0) lines.Add($"{what} {count} คน");
    }
}

/// <summary>
/// ตรวจไฟล์รายชื่อนักเรียน (template นักเรียน) · ฟังก์ชันบริสุทธิ์ ไม่แตะ DB
/// เพิ่มนักเรียนใหม่ ดึงคนที่มีในระบบแล้วเข้าห้อง และแก้เลขที่ · คนที่อยู่ในห้องแต่ไม่มีในไฟล์ไม่ถูกลบ
/// </summary>
public static class StudentSheetParser
{
    public static readonly string[] Headers = [Cells.NoHeader, Cells.CodeHeader, Cells.FirstNameHeader, Cells.LastNameHeader];

    /// รหัสทั้งหมดในไฟล์ ให้ endpoint โหลดนักเรียนจาก DB มาเทียบในคำสั่งเดียว
    public static string[] StudentCodes(Sheet sheet)
    {
        var column = sheet.Header.Select(Cells.HeaderText).ToList().IndexOf(Cells.CodeHeader);
        return column < 0
            ? []
            : sheet.Rows.Select(r => Cells.Code(r[column]).Value).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
    }

    public static ImportResult<StudentPlan> Parse(Sheet sheet, StudentSnapshot snapshot)
    {
        var errors = new ImportErrors();
        var columns = ReadColumns(sheet, errors);
        if (columns is null) return errors.Result<StudentPlan>(null);

        var rows = Cells.DataRows(sheet, errors);
        if (errors.Count > 0) return errors.Result<StudentPlan>(null);

        var rowOfNo = new Dictionary<int, int>();
        var rowOfCode = new Dictionary<string, int>(StringComparer.Ordinal);
        var enrollmentOf = snapshot.Enrollments.ToDictionary(e => e.StudentId);
        var idsInFile = new HashSet<int>();
        var students = new List<PlannedStudent>();

        foreach (var row in rows)
        {
            var errorsBefore = errors.Count;
            var r = row.RowNumber;

            var (no, noError) = Cells.No(row[columns[Cells.NoHeader]]);
            if (noError is not null) errors.Cell(r, Cells.NoHeader, noError);
            else if (!rowOfNo.TryAdd(no!.Value, r)) errors.Cell(r, Cells.NoHeader, $"เลขที่ {no} ซ้ำกับแถว {rowOfNo[no.Value]}");

            var (code, codeError) = Cells.Code(row[columns[Cells.CodeHeader]]);
            if (codeError is not null) errors.Cell(r, Cells.CodeHeader, codeError);
            else if (!rowOfCode.TryAdd(code!, r)) errors.Cell(r, Cells.CodeHeader, $"รหัสนักเรียน {code} ซ้ำกับแถว {rowOfCode[code!]}");

            var (first, firstError) = Cells.Name(row[columns[Cells.FirstNameHeader]], "ชื่อนักเรียน");
            if (firstError is not null) errors.Cell(r, Cells.FirstNameHeader, firstError);
            var (last, lastError) = Cells.Name(row[columns[Cells.LastNameHeader]], "นามสกุลนักเรียน");
            if (lastError is not null) errors.Cell(r, Cells.LastNameHeader, lastError);

            ExistingStudent? existing = null;
            if (codeError is null && snapshot.StudentsByCode.TryGetValue(code!, out existing))
            {
                idsInFile.Add(existing.Id);
                // กันพิมพ์รหัสผิดแล้วไปดึงเด็กคนอื่นเข้าห้อง
                if (first is not null && last is not null && !Cells.SameName(existing.FirstName, existing.LastName, first, last))
                    errors.Cell(r, Cells.CodeHeader, Cells.NameMismatch(code!, existing.FirstName, existing.LastName, first, last));
            }

            if (errors.Count > errorsBefore) continue;
            int? oldNo = existing is not null && enrollmentOf.TryGetValue(existing.Id, out var enrollment) ? enrollment.No : null;
            students.Add(new PlannedStudent(r, code!, first!, last!, no!.Value, existing?.Id, oldNo));
        }

        // คนที่อยู่ในห้องแต่ไม่มีในไฟล์ยังอยู่ต่อด้วยเลขที่เดิม เลขที่ในไฟล์จึงห้ามชนกับเขา
        foreach (var e in snapshot.Enrollments)
        {
            if (!idsInFile.Contains(e.StudentId) && rowOfNo.TryGetValue(e.No, out var row))
                errors.Cell(row, Cells.NoHeader,
                    $"เลขที่ {e.No} เป็นของ {e.FirstName} {e.LastName} ({e.Code}) ที่อยู่ในห้องนี้แต่ไม่มีในไฟล์ · แก้เลขที่ในไฟล์ หรือใส่นักเรียนคนนั้นในไฟล์ด้วย");
        }

        return errors.Result(new StudentPlan(students));
    }

    static Dictionary<string, int>? ReadColumns(Sheet sheet, ImportErrors errors)
    {
        var headers = Cells.Headers(sheet, errors);
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            if (headers[i].Length == 0 || Cells.TryClaim(columns, Headers, headers[i], i, errors)) continue;
            // ดักการอัปโหลดไฟล์ผิดประเภท เช่นเอาไฟล์คะแนนมาใส่หน้านำเข้ารายชื่อ
            errors.File(
                $"คอลัมน์ \"{Cells.Quote(headers[i])}\" ไม่ได้อยู่ในไฟล์ตัวอย่างรายชื่อนักเรียน · ถ้าจะนำเข้าคะแนนให้ใช้หน้านำเข้าคะแนน ถ้าเป็นหมายเหตุให้ลบคอลัมน์นี้ออก",
                Cells.ColumnLetter(i));
        }

        var missing = Headers.Where(h => !columns.ContainsKey(h)).ToList();
        if (missing.Count > 0) errors.File(Cells.MissingColumns(missing));
        return errors.Count > 0 ? null : columns;
    }
}
