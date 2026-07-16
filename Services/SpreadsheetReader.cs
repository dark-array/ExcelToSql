using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using ExcelToSql.Models;

namespace ExcelToSql.Services;

public sealed class SpreadsheetReader
{
    public const int MaxRowsPerSheet = 100_000;
    private static readonly HashSet<string> ExcelExtensions = [".xls", ".xlsx", ".xlsm"];
    private static readonly HashSet<string> CsvExtensions = [".csv", ".tsv", ".txt"];

    public SpreadsheetDocument Read(string filePath, string originalFileName, bool firstRowIsHeader, DatabaseEngine engine)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!IsSupported(ext))
            throw new InvalidOperationException($"Formato no soportado: {ext}. Use xls, xlsx, xlsm, csv o tsv.");

        List<SheetData> sheets = CsvExtensions.Contains(ext)
            ? [ReadCsv(filePath, originalFileName, firstRowIsHeader)]
            : ReadExcel(filePath, firstRowIsHeader);

        var maxIdent = SqlDialectFactory.Create(engine).MaxIdentifierLength;
        foreach (var sheet in sheets)
            sheet.Columns = BuildColumns(sheet, maxIdent);

        return new SpreadsheetDocument
        {
            FileName = originalFileName,
            Sheets = sheets
        };
    }

    public static bool IsSupported(string extension)
    {
        var ext = extension.ToLowerInvariant();
        return ExcelExtensions.Contains(ext) || CsvExtensions.Contains(ext);
    }

    private static List<ColumnDefinition> BuildColumns(SheetData sheet, int maxIdent)
    {
        var used = new HashSet<string>();
        var columns = new List<ColumnDefinition>();

        for (var i = 0; i < sheet.Headers.Count; i++)
        {
            var original = sheet.Headers[i];
            var sqlName = IdentifierSanitizer.Unique(original, used, maxIdent);
            var values = sheet.Rows.Select(r => i < r.Count ? r[i] : string.Empty);
            columns.Add(TypeInference.Infer(original, sqlName, values));
        }

        return columns;
    }

    private static List<SheetData> ReadExcel(string filePath, bool firstRowIsHeader)
    {
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false
            }
        });

        var sheets = new List<SheetData>();
        foreach (DataTable table in dataSet.Tables)
        {
            var grid = new List<List<string>>();
            var maxCols = 0;

            foreach (DataRow row in table.Rows)
            {
                var cells = new List<string>(table.Columns.Count);
                for (var i = 0; i < table.Columns.Count; i++)
                    cells.Add(FormatCell(row[i]));

                while (cells.Count > 0 && string.IsNullOrWhiteSpace(cells[^1]))
                    cells.RemoveAt(cells.Count - 1);

                if (cells.Count == 0)
                {
                    grid.Add([]);
                    continue;
                }

                maxCols = Math.Max(maxCols, cells.Count);
                grid.Add(cells);
            }

            if (maxCols == 0)
                continue;

            foreach (var row in grid)
            {
                while (row.Count < maxCols)
                    row.Add(string.Empty);
            }

            sheets.Add(ToSheet(table.TableName, grid, firstRowIsHeader));
        }

        if (sheets.Count == 0)
            throw new InvalidOperationException("El archivo no contiene hojas con datos.");

        return sheets;
    }

    private static SheetData ReadCsv(string filePath, string originalFileName, bool firstRowIsHeader)
    {
        var bytes = File.ReadAllBytes(filePath);
        var text = DecodeText(bytes);
        var delimiter = DetectDelimiter(text, Path.GetExtension(originalFileName));
        var grid = ParseCsv(text, delimiter);
        var name = Path.GetFileNameWithoutExtension(originalFileName);
        var sheet = ToSheet(string.IsNullOrWhiteSpace(name) ? "datos" : name, grid, firstRowIsHeader);
        if (sheet.Headers.Count == 0)
            throw new InvalidOperationException("El CSV no contiene columnas.");
        return sheet;
    }

    private static SheetData ToSheet(string name, List<List<string>> grid, bool firstRowIsHeader)
    {
        while (grid.Count > 0 && grid[^1].All(string.IsNullOrWhiteSpace))
            grid.RemoveAt(grid.Count - 1);

        if (grid.Count == 0)
        {
            return new SheetData { Name = name };
        }

        var width = grid.Max(r => r.Count);
        foreach (var row in grid)
        {
            while (row.Count < width)
                row.Add(string.Empty);
        }

        List<string> headers;
        List<List<string>> rows;

        if (firstRowIsHeader)
        {
            headers = NormalizeHeaders(grid[0], width);
            rows = grid.Skip(1).Take(MaxRowsPerSheet).Select(r => r.ToList()).ToList();
        }
        else
        {
            headers = Enumerable.Range(1, width).Select(i => "COL_" + i).ToList();
            rows = grid.Take(MaxRowsPerSheet).Select(r => r.ToList()).ToList();
        }

        return new SheetData
        {
            Name = name,
            Headers = headers,
            Rows = rows
        };
    }

    private static List<string> NormalizeHeaders(List<string> firstRow, int width)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var headers = new List<string>(width);

        for (var i = 0; i < width; i++)
        {
            var raw = i < firstRow.Count ? firstRow[i].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
                raw = "COL_" + (i + 1);

            var candidate = raw;
            var n = 2;
            while (used.Contains(candidate))
            {
                candidate = raw + "_" + n;
                n++;
            }

            used.Add(candidate);
            headers.Add(candidate);
        }

        return headers;
    }

    private static string FormatCell(object? value)
    {
        switch (value)
        {
            case null:
            case DBNull:
                return string.Empty;
            case DateTime dt:
                return dt.TimeOfDay == TimeSpan.Zero
                    ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            case DateTimeOffset dto:
                return FormatCell(dto.DateTime);
            case bool b:
                return b ? "1" : "0";
            case double d when Math.Abs(d % 1) < double.Epsilon * 10:
                return Convert.ToInt64(d).ToString(CultureInfo.InvariantCulture);
            case float f when Math.Abs(f % 1) < float.Epsilon * 10:
                return Convert.ToInt64(f).ToString(CultureInfo.InvariantCulture);
            case decimal m when m == decimal.Truncate(m):
                return decimal.Truncate(m).ToString(CultureInfo.InvariantCulture);
            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        }
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        var utf8 = new UTF8Encoding(false, true);
        try
        {
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static char DetectDelimiter(string text, string extension)
    {
        if (extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase))
            return '\t';

        var firstLine = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var candidates = new[] { ',', ';', '\t', '|' };
        return candidates.MaxBy(d => CountUnquoted(firstLine, d));
    }

    private static int CountUnquoted(string line, char delimiter)
    {
        var count = 0;
        var inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (!inQuotes && c == delimiter)
                count++;
        }

        return count;
    }

    private static List<List<string>> ParseCsv(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\n')
            {
                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = [];
            }
            else if (c != '\r')
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
