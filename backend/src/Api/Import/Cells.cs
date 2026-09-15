using System.Globalization;
using System.Text.RegularExpressions;
using Api.Common;

namespace Api.Import;

/// <summary>
/// อ่านค่าจากช่อง Excel ทีละชนิด · คืน (ค่า, null) เมื่อผ่าน หรือ (null, ข้อความไทย) เมื่อไม่ผ่าน
/// ใช้ร่วมกันระหว่างไฟล์นักเรียนกับไฟล์คะแนน กฎการดักจะได้เหมือนกันทุกที่
/// </summary>
public static partial class Cells
{
    public const string NoHeader = "เลขที่";
    public const string CodeHeader = "รหัสนักเรียน";
    public const string FirstNameHeader = "ชื่อ";
    public const string LastNameHeader = "นามสกุล";

    /// ตัวเลขที่ใหญ่กว่านี้ไม่มีทางเป็นคะแนนหรือเลขที่ ตัดทิ้งก่อนแปลงเป็น decimal จะได้ไม่ overflow
    const double SanityLimit = 1e9;

    /// รหัสนักเรียนที่ Excel เก็บเป็นตัวเลขต้องไม่ใหญ่เกินที่ double เก็บจำนวนเต็มได้แม่น
    const double MaxNumericCode = 1e15;

    /// ข้อความจากไฟล์ที่ยกมาแสดงในจุดผิด ตัดให้สั้น ครูจะได้อ่านง่าย
    const int QuoteLength = 30;

    // ช่องว่างทุกแบบ รวม non-breaking space และ zero-width ที่ติดมาตอน copy ข้อความไทยจากเว็บหรือ LINE
    [GeneratedRegex(@"[\s​﻿]+")]
    private static partial Regex Spaces();

    public static string Normalize(string text) => Spaces().Replace(text, " ").Trim();

    /// เลขไทย ๐-๙ เป็นเลขอารบิก · ครูบางคนตั้งแป้นพิมพ์ไทยแล้วพิมพ์ตัวเลขมาเป็นเลขไทย
    static string AsciiDigits(string text) =>
        string.Concat(text.Select(c => c is >= '๐' and <= '๙' ? (char)('0' + (c - '๐')) : c));

    public static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Quote(string text) => text.Length <= QuoteLength ? text : text[..QuoteLength] + "…";

    /// 0 → A, 25 → Z, 26 → AA
    public static string ColumnLetter(int index)
    {
        var letters = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
            letters = (char)('A' + (n - 1) % 26) + letters;
        return letters;
    }

    public static string HeaderText(SheetCell cell) => cell.Kind switch
    {
        CellKind.Blank => "",
        CellKind.Number => Normalize(cell.Number.ToString(CultureInfo.InvariantCulture)),
        _ => Normalize(cell.Text),
    };

    /// หัวคอลัมน์ทุกคอลัมน์หลังตัดช่องว่าง · คอลัมน์ที่หัวว่างแต่มีข้อมูลเป็นจุดผิด
    public static List<string> Headers(Sheet sheet, ImportErrors errors)
    {
        var headers = sheet.Header.Select(HeaderText).ToList();
        for (var i = 0; i < headers.Count; i++)
        {
            var column = i;
            if (headers[column].Length == 0 && sheet.Rows.Any(r => r[column].Kind != CellKind.Blank))
                errors.File($"คอลัมน์ {ColumnLetter(column)} มีข้อมูลแต่ไม่มีหัวตาราง ใส่ชื่อหัวคอลัมน์หรือลบคอลัมน์นี้ออก",
                    ColumnLetter(column));
        }
        return headers;
    }

    /// จับหัวคอลัมน์ที่รู้จักเข้ากับตำแหน่ง · หัวซ้ำเป็นจุดผิด · คืน false ถ้าไม่ใช่หัวที่รู้จัก
    public static bool TryClaim(Dictionary<string, int> found, IReadOnlyCollection<string> known, string header, int index,
        ImportErrors errors)
    {
        if (!known.Contains(header)) return false;
        if (!found.TryAdd(header, index))
            errors.File($"มีคอลัมน์ \"{header}\" ซ้ำ (คอลัมน์ {ColumnLetter(found[header])} กับ {ColumnLetter(index)})",
                ColumnLetter(index));
        return true;
    }

    public static string MissingColumns(IEnumerable<string> headers) =>
        $"ไม่พบคอลัมน์ {string.Join(", ", headers.Select(h => $"\"{h}\""))} · แถวแรกต้องเป็นหัวตาราง ดาวน์โหลด template ไปใช้จะง่ายที่สุด";

    public static string NameMismatch(string code, string savedFirst, string savedLast, string fileFirst, string fileLast) =>
        $"รหัส {code} ในระบบเป็นของ {savedFirst} {savedLast} แต่ในไฟล์เป็น {fileFirst} {fileLast} · ถ้ารหัสผิดให้แก้ในไฟล์ ถ้าชื่อเปลี่ยนจริงให้แก้ที่แท็บนักเรียนก่อน";

    /// แถวที่มีข้อมูล (ข้ามแถวว่าง) · ไม่มีข้อมูลเลยหรือมากเกินเพดานเป็นจุดผิด
    public static List<SheetRow> DataRows(Sheet sheet, ImportErrors errors)
    {
        var rows = sheet.Rows.Where(r => r.Cells.Any(c => c.Kind != CellKind.Blank)).ToList();
        if (rows.Count == 0) errors.File("ไม่มีข้อมูลในไฟล์ ใส่ข้อมูลตั้งแต่แถวที่ 2 ใต้หัวตาราง");
        else if (rows.Count > Limits.ImportMaxRows) errors.File($"ไฟล์มีข้อมูลเกิน {Limits.ImportMaxRows} แถว ให้แยกเป็นไฟล์ละห้อง");
        return rows;
    }

    public static bool SameName(string savedFirst, string savedLast, string fileFirst, string fileLast) =>
        Normalize(savedFirst) == fileFirst && Normalize(savedLast) == fileLast;

    public static bool TryParseNumber(string text, out decimal value) =>
        decimal.TryParse(AsciiDigits(Normalize(text)), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);

    public static string? Number(SheetCell cell, out decimal value)
    {
        value = 0;
        switch (cell.Kind)
        {
            case CellKind.Number:
                if (!double.IsFinite(cell.Number) || Math.Abs(cell.Number) >= SanityLimit) return "ตัวเลขใหญ่ผิดปกติ";
                // Excel เก็บเป็น double: 0.1 + 0.2 ได้ 0.30000000000000004 ปัดเศษทิ้งก่อนเช็คทศนิยม
                value = Math.Round((decimal)cell.Number, 6);
                return null;
            case CellKind.Text:
                if (!TryParseNumber(cell.Text, out value)) return $"\"{Quote(cell.Text)}\" ไม่ใช่ตัวเลข";
                return Math.Abs(value) >= (decimal)SanityLimit ? "ตัวเลขใหญ่ผิดปกติ" : null;
            case CellKind.Date:
                return "Excel มองช่องนี้เป็นวันที่/เวลา ให้ตั้งรูปแบบเซลล์เป็นตัวเลข (Number) แล้วพิมพ์ใหม่";
            case CellKind.Error:
                return $"ช่องนี้ใช้ไม่ได้ ({Quote(cell.Text)})";
            case CellKind.Boolean:
                return "ช่องนี้ต้องเป็นตัวเลข";
            default:
                return "ช่องนี้ว่าง";
        }
    }

    public static (int? Value, string? Error) No(SheetCell cell)
    {
        if (cell.Kind == CellKind.Blank) return (null, "กรุณากรอกเลขที่");
        if (Number(cell, out var value) is { } error) return (null, error);
        if (value != decimal.Truncate(value)) return (null, "เลขที่ต้องเป็นจำนวนเต็ม");
        if (value < 1 || value > Limits.MaxStudentNo) return (null, Validate.No(0));
        return ((int)value, null);
    }

    public static (string? Value, string? Error) Code(SheetCell cell)
    {
        var code = cell.Kind switch
        {
            CellKind.Blank => "",
            CellKind.Text => AsciiDigits(Normalize(cell.Text)),
            // Excel แปลงรหัสที่พิมพ์เป็นตัวเลขล้วนให้เป็น number เอง (90001 → 90001.0)
            CellKind.Number when cell.Number is >= 0 and < MaxNumericCode && cell.Number == Math.Floor(cell.Number)
                => ((long)cell.Number).ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
        if (code is null) return (null, "รหัสนักเรียนต้องเป็นตัวเลขหรือตัวอักษร");
        return Validate.StudentCode(code) is { } error ? (null, error) : (code, null);
    }

    public static (string? Value, string? Error) Name(SheetCell cell, string what)
    {
        if (cell.Kind is not (CellKind.Text or CellKind.Blank)) return (null, $"{what}ต้องเป็นข้อความ");
        var name = Normalize(cell.Text);
        return Validate.Name(name, what) is { } error ? (null, error) : (name, null);
    }

    public static (decimal? Value, string? Error) Score(SheetCell cell, decimal maxScore)
    {
        if (Number(cell, out var value) is { } error) return (null, error);
        if (Validate.Score(value, maxScore) is { } scoreError) return (null, scoreError);
        // DB เก็บ decimal(6,2) ถ้าไม่ดักตรงนี้ 18.555 จะถูกปัดเงียบ ๆ เป็น 18.56
        if (value != Math.Round(value, 2)) return (null, "คะแนนมีทศนิยมได้ไม่เกิน 2 ตำแหน่ง");
        return (value, null);
    }
}
