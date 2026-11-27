using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using FluentResults;

namespace Fugue.Application.Features.Import.Parsing;

/// <summary>
/// Reads a .csv or .xlsx into an <see cref="ImportSheet"/>: the first row is the header, blank rows are skipped, each data row gets a content key.
/// The .xlsx reader needs no package: an .xlsx is a zip of XML parts, and only the first worksheet, the shared strings and the cell styles (to tell a
/// date from a number) are read. Formulas are read as their cached value.
/// </summary>
public static class SpreadsheetReader
{
    private const string SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>Built-in number formats that are dates (ECMA-376 18.8.30).</summary>
    private static readonly HashSet<int> BuiltInDateFormats = [14, 15, 16, 17, 22, 27, 30, 36, 45, 46, 47, 50, 57];

    public static Result<ImportSheet> Read(string fileName, byte[] bytes) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv" or ".txt" => ReadCsv(bytes),
            ".xlsx" => ReadXlsx(bytes),
            _ => ImportText.Unreadable<ImportSheet>("A spreadsheet import takes a .csv or .xlsx file."),
        };

    public static Result<ImportSheet> ReadCsv(byte[] bytes)
    {
        if (ImportText.LooksBinary(bytes))
        {
            return ImportText.Unreadable<ImportSheet>("This is not a text CSV file.");
        }

        string text = ImportText.Decode(bytes);
        char delimiter = DetectDelimiter(text);
        List<List<string>> rows = [];
        List<string> row = [];
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    cell.Append(c);
                }

                continue;
            }

            if (c == '"' && cell.Length == 0)
            {
                quoted = true;
            }
            else if (c == delimiter)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = [];
                if (rows.Count > ImportLimits.MaxRows + 1)
                {
                    return ImportText.Unreadable<ImportSheet>($"A spreadsheet import takes at most {ImportLimits.MaxRows} rows.");
                }
            }
            else
            {
                cell.Append(c);
            }

            if (cell.Length > ImportLimits.MaxCellChars)
            {
                return ImportText.Unreadable<ImportSheet>($"A cell is longer than {ImportLimits.MaxCellChars} characters.");
            }
        }

        if (quoted)
        {
            return ImportText.Unreadable<ImportSheet>("The CSV has a quote that is never closed.");
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return Build(rows.Select((r, i) => (Number: i + 1, Cells: (IReadOnlyList<string>)r)).ToList());
    }

    public static Result<ImportSheet> ReadXlsx(byte[] bytes)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            string? sheetPath = FirstSheetPath(zip);
            if (sheetPath is null || zip.GetEntry(sheetPath) is not { } sheetEntry)
            {
                return ImportText.Unreadable<ImportSheet>("The workbook has no worksheet.");
            }

            List<string> shared = SharedStrings(zip);
            HashSet<int> dateStyles = DateStyles(zip);
            List<(int Number, IReadOnlyList<string> Cells)> rows = [];
            using XmlReader xml = Open(sheetEntry);
            while (xml.ReadToFollowing("row", SheetNs))
            {
                int number = int.TryParse(xml.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out int r) ? r : rows.Count + 1;
                var cells = new SortedDictionary<int, string>();
                using (XmlReader rowXml = xml.ReadSubtree())
                {
                    while (rowXml.ReadToFollowing("c", SheetNs))
                    {
                        int column = ColumnIndex(rowXml.GetAttribute("r")) ?? (cells.Count == 0 ? 0 : cells.Keys.Last() + 1);
                        if (column >= ImportLimits.MaxColumns)
                        {
                            continue;
                        }

                        cells[column] = CellValue(rowXml, shared, dateStyles);
                    }
                }

                int width = cells.Count == 0 ? 0 : cells.Keys.Last() + 1;
                rows.Add((number, Enumerable.Range(0, width).Select(i => cells.TryGetValue(i, out string? v) ? v : string.Empty).ToList()));
                if (rows.Count > ImportLimits.MaxRows + 1)
                {
                    return ImportText.Unreadable<ImportSheet>($"A spreadsheet import takes at most {ImportLimits.MaxRows} rows.");
                }
            }

            return Build(rows);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException or FormatException)
        {
            return ImportText.Unreadable<ImportSheet>("This is not a readable .xlsx workbook.");
        }
    }

    private static Result<ImportSheet> Build(List<(int Number, IReadOnlyList<string> Cells)> rows)
    {
        List<(int Number, IReadOnlyList<string> Cells)> filled = rows
            .Select(r => (r.Number, Cells: (IReadOnlyList<string>)r.Cells.Select(c => c.Trim()).ToList()))
            .Where(r => r.Cells.Any(c => c.Length > 0))
            .ToList();
        if (filled.Count == 0)
        {
            return ImportText.Unreadable<ImportSheet>("The spreadsheet is empty.");
        }

        IReadOnlyList<string> headers = filled[0].Cells;
        if (headers.Count > ImportLimits.MaxColumns)
        {
            return ImportText.Unreadable<ImportSheet>($"A spreadsheet import takes at most {ImportLimits.MaxColumns} columns.");
        }

        if (filled.Count == 1)
        {
            return ImportText.Unreadable<ImportSheet>("The spreadsheet has a header row but no data rows.");
        }

        var data = filled.Skip(1)
            .Select(r => new ImportSheetRow(r.Number, RowKey(r.Cells), r.Cells.Take(ImportLimits.MaxColumns).ToList()))
            .ToList();
        return Result.Ok(new ImportSheet(headers, data));
    }

    /// <summary>The same row content gives the same key in any file, so a row imported from one workbook is recognised in the next.</summary>
    private static string RowKey(IReadOnlyList<string> cells) =>
        ImportText.Sha256("spreadsheet\n" + string.Join('\u001f', cells.Reverse().SkipWhile(c => c.Length == 0).Reverse()));

    private static char DetectDelimiter(string text)
    {
        int end = text.IndexOfAny(['\r', '\n']);
        string first = end < 0 ? text : text[..end];
        char[] candidates = [',', ';', '\t'];
        return candidates.OrderByDescending(c => first.Count(x => x == c)).First();
    }

    private static XmlReader Open(ZipArchiveEntry entry)
    {
        if (entry.Length > ImportLimits.MaxXlsxPartBytes)
        {
            throw new InvalidDataException("A workbook part is too large.");
        }

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = false };
        return XmlReader.Create(entry.Open(), settings);
    }

    /// <summary>The first sheet listed in the workbook, resolved through its relationship; falls back to the conventional path.</summary>
    private static string? FirstSheetPath(ZipArchive zip)
    {
        if (zip.GetEntry("xl/workbook.xml") is not { } workbook)
        {
            return null;
        }

        string? relId = null;
        using (XmlReader xml = Open(workbook))
        {
            if (xml.ReadToFollowing("sheet", SheetNs))
            {
                relId = xml.GetAttribute("id", RelNs);
            }
        }

        if (relId is not null && zip.GetEntry("xl/_rels/workbook.xml.rels") is { } rels)
        {
            using XmlReader xml = Open(rels);
            while (xml.ReadToFollowing("Relationship", PackageRelNs))
            {
                if (xml.GetAttribute("Id") == relId && xml.GetAttribute("Target") is { } target)
                {
                    return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
                }
            }
        }

        return zip.GetEntry("xl/worksheets/sheet1.xml") is null ? null : "xl/worksheets/sheet1.xml";
    }

    private static List<string> SharedStrings(ZipArchive zip)
    {
        List<string> strings = [];
        if (zip.GetEntry("xl/sharedStrings.xml") is not { } entry)
        {
            return strings;
        }

        using XmlReader xml = Open(entry);
        while (xml.ReadToFollowing("si", SheetNs))
        {
            strings.Add(RichText(xml));
        }

        return strings;
    }

    /// <summary>The text of an <c>si</c> or <c>is</c> element: every <c>t</c> run, ignoring phonetic runs.</summary>
    private static string RichText(XmlReader xml)
    {
        var text = new StringBuilder();
        using XmlReader inner = xml.ReadSubtree();
        int phonetic = -1;
        while (inner.Read())
        {
            if (inner is { NodeType: XmlNodeType.Element, LocalName: "rPh" })
            {
                phonetic = inner.Depth;
            }
            else if (inner.NodeType == XmlNodeType.EndElement && inner.Depth == phonetic)
            {
                phonetic = -1;
            }
            else if (phonetic < 0 && inner is { NodeType: XmlNodeType.Element, LocalName: "t" })
            {
                text.Append(inner.ReadString());
            }
        }

        return text.ToString();
    }

    /// <summary>Indexes of the cell formats (<c>cellXfs</c>) whose number format is a date, built-in or custom.</summary>
    private static HashSet<int> DateStyles(ZipArchive zip)
    {
        HashSet<int> styles = [];
        if (zip.GetEntry("xl/styles.xml") is not { } entry)
        {
            return styles;
        }

        HashSet<int> customDates = [];
        int index = 0;
        bool inCellXfs = false;
        using XmlReader xml = Open(entry);
        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "numFmt"
                && int.TryParse(xml.GetAttribute("numFmtId"), CultureInfo.InvariantCulture, out int id)
                && IsDateFormat(xml.GetAttribute("formatCode")))
            {
                customDates.Add(id);
            }
            else if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "cellXfs")
            {
                inCellXfs = !xml.IsEmptyElement;
            }
            else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "cellXfs")
            {
                inCellXfs = false;
            }
            else if (inCellXfs && xml.NodeType == XmlNodeType.Element && xml.LocalName == "xf")
            {
                if (int.TryParse(xml.GetAttribute("numFmtId"), CultureInfo.InvariantCulture, out int fmt)
                    && (BuiltInDateFormats.Contains(fmt) || customDates.Contains(fmt)))
                {
                    styles.Add(index);
                }

                index++;
            }
        }

        return styles;
    }

    /// <summary>A custom format is a date when, outside quoted text and brackets, it has a day, month or year token and no elapsed-time-only shape.</summary>
    private static bool IsDateFormat(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        var bare = new StringBuilder();
        bool inQuote = false;
        int bracket = 0;
        foreach (char c in code)
        {
            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (!inQuote && c == '[')
            {
                bracket++;
            }
            else if (!inQuote && c == ']')
            {
                bracket = Math.Max(0, bracket - 1);
            }
            else if (!inQuote && bracket == 0)
            {
                bare.Append(char.ToLowerInvariant(c));
            }
        }

        string s = bare.ToString();
        return s.Contains('d') || s.Contains('y') || (s.Contains('m') && !s.Contains('h') && !s.Contains('s'));
    }

    private static string CellValue(XmlReader cell, List<string> shared, HashSet<int> dateStyles)
    {
        string? type = cell.GetAttribute("t");
        int style = int.TryParse(cell.GetAttribute("s"), CultureInfo.InvariantCulture, out int s) ? s : 0;
        if (cell.IsEmptyElement)
        {
            return string.Empty;
        }

        string? value = null;
        string? inline = null;
        using (XmlReader inner = cell.ReadSubtree())
        {
            while (inner.Read())
            {
                if (inner is { NodeType: XmlNodeType.Element, LocalName: "v" })
                {
                    value = inner.ReadString();
                }
                else if (inner is { NodeType: XmlNodeType.Element, LocalName: "is" })
                {
                    inline = RichText(inner);
                }
            }
        }

        string text = type switch
        {
            "s" => int.TryParse(value, CultureInfo.InvariantCulture, out int i) && i >= 0 && i < shared.Count ? shared[i] : string.Empty,
            "inlineStr" => inline ?? string.Empty,
            "b" => value == "1" ? "TRUE" : "FALSE",
            "str" or "e" => value ?? string.Empty,
            _ => NumberText(value, dateStyles.Contains(style)),
        };
        return ImportText.Cap(text, ImportLimits.MaxCellChars);
    }

    private static string NumberText(string? value, bool isDate)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            return value ?? string.Empty;
        }

        if (isDate && number is > 0 and < 2958466)
        {
            return DateOnly.FromDateTime(DateTime.FromOADate(number)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return number.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    /// <summary>The zero-based column of a cell reference such as <c>C7</c>; null when the reference is absent or malformed.</summary>
    private static int? ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        int column = 0;
        int letters = 0;
        foreach (char c in reference)
        {
            if (c is >= 'A' and <= 'Z')
            {
                column = (column * 26) + (c - 'A' + 1);
                letters++;
            }
            else
            {
                break;
            }
        }

        return letters is 0 or > 3 ? null : column - 1;
    }
}
