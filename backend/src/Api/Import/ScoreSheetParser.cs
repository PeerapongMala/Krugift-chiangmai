using System.Text.RegularExpressions;
using Api.Common;

namespace Api.Import;

public record ScoreSnapshot(
    IReadOnlyList<ExistingItem> Items,
    IReadOnlyList<ExistingEnrollment> Enrollments,
    IReadOnlyDictionary<(int ItemId, int StudentId), decimal?> Scores);

public record NewItem(string Name, decimal MaxScore);

/// <param name="ItemId">null = รายการใหม่ที่จะสร้างจากหัวคอลัมน์ (อ้างด้วย ItemName)</param>
public record ScoreChange(int Row, int StudentId, string StudentLabel, string ItemName, int? ItemId, decimal? OldValue, decimal NewValue);

public record ScorePlan(int Rows, IReadOnlyList<NewItem> NewItems, IReadOnlyList<ScoreChange> Scores) : IImportPlan
{
    public bool HasChanges => NewItems.Count > 0 || Scores.Count > 0;

    public IReadOnlyList<string> Summary
    {
        get
        {
            var lines = new List<string> { $"นักเรียนในไฟล์ {Rows} คน" };
            if (NewItems.Count > 0)
                lines.Add($"เพิ่มรายการคะแนนใหม่: {string.Join(", ", NewItems.Select(i => ScoreSheetParser.ItemHeader(i.Name, i.MaxScore)))}");
            if (Scores.Count > 0) lines.Add($"กรอกหรือแก้คะแนน {Scores.Count} ช่อง");
            // ครูต้องรู้ชัด ๆ ว่ามีการทับคะแนนที่กรอกไว้แล้ว ไม่ใช่แค่เติมช่องว่าง
            var overwrites = Scores.Count(s => s.OldValue is not null);
            if (overwrites > 0) lines.Add($"ในนั้นเป็นการแก้คะแนนที่มีอยู่แล้ว {overwrites} ช่อง");
            if (!HasChanges) lines.Add("ไม่มีอะไรต่างจากในระบบ");
            return lines;
        }
    }

    public IReadOnlyList<ImportChange> Changes => Scores
        .Select(s => new ImportChange(s.Row, $"{s.StudentLabel} · {s.ItemName}",
            $"{(s.OldValue is { } old ? Cells.Format(old) : "ว่าง")} → {Cells.Format(s.NewValue)}"))
        .ToList();
}

/// <summary>
/// ตรวจไฟล์คะแนน (template คะแนน) · ฟังก์ชันบริสุทธิ์ ไม่แตะ DB
/// - นักเรียนต้องอยู่ในห้องแล้ว จับคู่ด้วยรหัส · เลขที่และชื่อในไฟล์ถ้ามีต้องตรงกับในระบบ กันคะแนนลงผิดคน
/// - หัวคอลัมน์คะแนนเขียนเป็น "ชื่อรายการ (คะแนนเต็ม)" · รายการที่ยังไม่มีจะถูกสร้างใหม่
/// - ช่องคะแนนว่าง = ไม่เปลี่ยนคะแนนเดิม (ล้างคะแนนทำที่ตารางคะแนนทีละช่อง มี audit ชัดเจนกว่า)
/// </summary>
public static partial class ScoreSheetParser
{
    static readonly string[] StudentHeaders = [Cells.NoHeader, Cells.CodeHeader, Cells.FirstNameHeader, Cells.LastNameHeader];

    [GeneratedRegex(@"^(?<name>.*?)\s*\((?<max>[^()]*)\)$")]
    private static partial Regex ItemHeaderPattern();

    public static string ItemHeader(string name, decimal maxScore) => $"{name} ({Cells.Format(maxScore)})";

    sealed record ItemColumn(int Index, string Header, string Name, decimal MaxScore, int? ItemId);

    public static ImportResult<ScorePlan> Parse(Sheet sheet, ScoreSnapshot snapshot)
    {
        var errors = new ImportErrors();
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        var items = ReadColumns(sheet, snapshot, columns, errors);
        if (errors.Count > 0) return errors.Result<ScorePlan>(null);

        var rows = Cells.DataRows(sheet, errors);
        if (errors.Count > 0) return errors.Result<ScorePlan>(null);

        var enrollmentOf = snapshot.Enrollments.ToDictionary(e => e.Code, StringComparer.Ordinal);
        var rowOfCode = new Dictionary<string, int>(StringComparer.Ordinal);
        var changes = new List<ScoreChange>();

        foreach (var row in rows)
        {
            var errorsBefore = errors.Count;
            var r = row.RowNumber;

            var (code, codeError) = Cells.Code(row[columns[Cells.CodeHeader]]);
            ExistingEnrollment? student = null;
            if (codeError is not null) errors.Cell(r, Cells.CodeHeader, codeError);
            else if (!rowOfCode.TryAdd(code!, r)) errors.Cell(r, Cells.CodeHeader, $"รหัสนักเรียน {code} ซ้ำกับแถว {rowOfCode[code!]}");
            else if (!enrollmentOf.TryGetValue(code!, out student))
                errors.Cell(r, Cells.CodeHeader, $"ไม่มีนักเรียนรหัส {code} ในห้องนี้ · เพิ่มนักเรียนหรือนำเข้ารายชื่อนักเรียนก่อน");
            else CheckIdentity(row, student, columns, errors);

            var rowChanges = new List<ScoreChange>();
            // ตรวจทุกช่องแม้แถวนี้ผิดไปแล้ว ครูจะได้เห็นจุดผิดครบในรอบเดียว
            foreach (var item in items)
            {
                var cell = row[item.Index];
                if (cell.Kind == CellKind.Blank) continue;

                var (value, scoreError) = Cells.Score(cell, item.MaxScore);
                if (scoreError is not null)
                {
                    errors.Cell(r, item.Header, scoreError);
                    continue;
                }
                if (student is null) continue;

                decimal? old = item.ItemId is { } itemId && snapshot.Scores.TryGetValue((itemId, student.StudentId), out var current)
                    ? current
                    : null;
                if (old != value)
                    rowChanges.Add(new ScoreChange(r, student.StudentId, $"{student.FirstName} {student.LastName} ({student.Code})",
                        item.Name, item.ItemId, old, value!.Value));
            }

            if (errors.Count == errorsBefore) changes.AddRange(rowChanges);
        }

        var newItems = items.Where(i => i.ItemId is null).Select(i => new NewItem(i.Name, i.MaxScore)).ToList();
        return errors.Result(new ScorePlan(rows.Count, newItems, changes));
    }

    static List<ItemColumn> ReadColumns(Sheet sheet, ScoreSnapshot snapshot, Dictionary<string, int> columns, ImportErrors errors)
    {
        var existingItems = new Dictionary<string, ExistingItem>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items) existingItems.TryAdd(Cells.Normalize(item.Name), item);

        var headers = Cells.Headers(sheet, errors);
        var items = new List<ItemColumn>();
        var columnOfItem = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < headers.Count; i++)
        {
            if (headers[i].Length == 0 || Cells.TryClaim(columns, StudentHeaders, headers[i], i, errors)) continue;

            var (column, error) = ReadItemHeader(i, headers[i], existingItems);
            if (error is not null)
            {
                errors.File(error, Cells.ColumnLetter(i));
                continue;
            }
            if (!columnOfItem.TryAdd(column!.Name, i))
            {
                errors.File($"มีคอลัมน์รายการ \"{column.Name}\" ซ้ำ (คอลัมน์ {Cells.ColumnLetter(columnOfItem[column.Name])} กับ {Cells.ColumnLetter(i)})",
                    Cells.ColumnLetter(i));
                continue;
            }
            items.Add(column);
        }

        if (!columns.ContainsKey(Cells.CodeHeader)) errors.File(Cells.MissingColumns([Cells.CodeHeader]));
        if (items.Count == 0 && errors.Count == 0)
            errors.File("ไม่พบคอลัมน์คะแนน · หัวคอลัมน์คะแนนเขียนเป็น \"ชื่อรายการ (คะแนนเต็ม)\" เช่น \"สอบกลางภาค (20)\"");
        if (items.Count > Limits.ImportMaxItems) errors.File($"มีคอลัมน์คะแนนเกิน {Limits.ImportMaxItems} รายการ");
        return items;
    }

    static (ItemColumn? Column, string? Error) ReadItemHeader(int index, string header, Dictionary<string, ExistingItem> existingItems)
    {
        string name;
        decimal maxScore;
        var match = ItemHeaderPattern().Match(header);
        if (match.Success)
        {
            name = match.Groups["name"].Value.Trim();
            if (!Cells.TryParseNumber(match.Groups["max"].Value, out maxScore))
                return (null, $"คะแนนเต็มในวงเล็บของ \"{Cells.Quote(header)}\" ต้องเป็นตัวเลข เช่น \"สอบกลางภาค (20)\"");
            if (Validate.MaxScore(maxScore) is { } maxError) return (null, $"\"{Cells.Quote(header)}\": {maxError}");
            if (maxScore != Math.Round(maxScore, 2))
                return (null, $"\"{Cells.Quote(header)}\": คะแนนเต็มมีทศนิยมได้ไม่เกิน 2 ตำแหน่ง");
        }
        else if (existingItems.TryGetValue(header, out var known))
        {
            // รายการที่มีอยู่แล้วไม่ต้องเขียนคะแนนเต็มซ้ำก็ได้
            name = header;
            maxScore = known.MaxScore;
        }
        else
        {
            return (null,
                $"หัวคอลัมน์ \"{Cells.Quote(header)}\" ไม่ได้บอกคะแนนเต็ม ให้เขียนเป็น \"{Cells.Quote(header)} (10)\" · ถ้าไม่ใช่คอลัมน์คะแนนให้ลบคอลัมน์นี้ออก");
        }

        if (Validate.Name(name, "ชื่อรายการคะแนน") is { } nameError) return (null, $"คอลัมน์ \"{Cells.Quote(header)}\": {nameError}");

        if (!existingItems.TryGetValue(name, out var existing)) return (new ItemColumn(index, header, name, maxScore, null), null);

        // เปลี่ยนคะแนนเต็มผ่านไฟล์อาจทำให้คะแนนเดิมเกินเต็ม ให้ไปแก้ที่หน้ารายการซึ่งตรวจเรื่องนี้อยู่แล้ว
        if (existing.MaxScore != maxScore)
            return (null,
                $"\"{name}\" ในไฟล์เต็ม {Cells.Format(maxScore)} แต่ในระบบเต็ม {Cells.Format(existing.MaxScore)} · ถ้าจะเปลี่ยนคะแนนเต็มให้แก้ที่แท็บรายการคะแนนก่อน");
        return (new ItemColumn(index, header, name, maxScore, existing.Id), null);
    }

    /// เลขที่และชื่อในไฟล์เป็นตัวช่วยให้ครูอ่านง่าย แต่ถ้ามีต้องตรงกับในระบบ ไม่งั้นคะแนนแถวนี้อาจเป็นของคนอื่น
    static void CheckIdentity(SheetRow row, ExistingEnrollment student, Dictionary<string, int> columns, ImportErrors errors)
    {
        var r = row.RowNumber;
        if (columns.TryGetValue(Cells.NoHeader, out var noColumn))
        {
            var (no, noError) = Cells.No(row[noColumn]);
            if (noError is not null) errors.Cell(r, Cells.NoHeader, noError);
            else if (no != student.No)
                errors.Cell(r, Cells.NoHeader,
                    $"เลขที่ในไฟล์ ({no}) ไม่ตรงกับรหัส {student.Code} ในระบบ (เลขที่ {student.No}) · ตรวจว่าคะแนนแถวนี้เป็นของคนนี้จริง");
        }

        (string? Value, string? Error) first = columns.TryGetValue(Cells.FirstNameHeader, out var firstColumn)
            ? Cells.Name(row[firstColumn], "ชื่อนักเรียน")
            : (Cells.Normalize(student.FirstName), null);
        (string? Value, string? Error) last = columns.TryGetValue(Cells.LastNameHeader, out var lastColumn)
            ? Cells.Name(row[lastColumn], "นามสกุลนักเรียน")
            : (Cells.Normalize(student.LastName), null);

        if (first.Error is not null) errors.Cell(r, Cells.FirstNameHeader, first.Error);
        if (last.Error is not null) errors.Cell(r, Cells.LastNameHeader, last.Error);
        if (first.Value is not null && last.Value is not null
            && !Cells.SameName(student.FirstName, student.LastName, first.Value, last.Value))
            errors.Cell(r, Cells.CodeHeader, Cells.NameMismatch(student.Code, student.FirstName, student.LastName, first.Value, last.Value));
    }
}
