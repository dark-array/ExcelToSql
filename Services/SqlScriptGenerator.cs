using System.Text;
using ExcelToSql.Models;

namespace ExcelToSql.Services;

public sealed class SqlScriptGenerator
{
    public string Generate(SpreadsheetDocument document, ConversionForm form)
    {
        var dialect = SqlDialectFactory.Create(form.Engine);

        var sb = new StringBuilder();
        sb.AppendLine(dialect.ScriptHeader(document.FileName).TrimEnd());
        sb.AppendLine();

        var anySheet = false;
        foreach (var sheetForm in form.Sheets)
        {
            var sheet = document.Sheets.FirstOrDefault(s =>
                string.Equals(s.Name, sheetForm.SheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet is null)
                continue;

            var columns = ResolveColumns(sheetForm, dialect);
            if (columns.Count == 0)
                continue;

            anySheet = true;
            var tableName = IdentifierSanitizer.Sanitize(
                string.IsNullOrWhiteSpace(sheetForm.TableName) ? sheet.Name : sheetForm.TableName,
                dialect.MaxIdentifierLength);

            sb.AppendLine($"-- Hoja: {sheet.Name} -> {tableName}");

            if (form.Action is SqlAction.CreateTable or SqlAction.Both)
            {
                sb.AppendLine(BuildCreateTable(dialect, tableName, columns, sheetForm.IdentityColumn));
                sb.AppendLine();
            }

            if (form.Action is SqlAction.Insert or SqlAction.Both)
            {
                var hasIdentity = columns.Any(c =>
                    !string.IsNullOrWhiteSpace(sheetForm.IdentityColumn) &&
                    string.Equals(c.OriginalName, sheetForm.IdentityColumn, StringComparison.OrdinalIgnoreCase));
                var rows = FilterRows(sheet, columns, form.SkipBlankRows, form.TrimWhitespace);
                sb.Append(BuildInserts(dialect, tableName, columns, rows, form.TrimWhitespace, hasIdentity));
                sb.AppendLine();
            }
        }

        if (!anySheet)
            throw new InvalidOperationException("Seleccione al menos una hoja y una columna.");

        if (!string.IsNullOrWhiteSpace(dialect.ScriptFooter))
            sb.AppendLine(dialect.ScriptFooter);

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static List<ColumnDefinition> ResolveColumns(SheetForm sheetForm, ISqlDialect dialect)
    {
        var used = new HashSet<string>();
        var columns = new List<ColumnDefinition>();

        foreach (var col in sheetForm.Columns.Where(c => c.Selected))
        {
            columns.Add(new ColumnDefinition
            {
                OriginalName = col.OriginalName,
                SqlName = IdentifierSanitizer.Unique(
                    string.IsNullOrWhiteSpace(col.SqlName) ? col.OriginalName : col.SqlName,
                    used,
                    dialect.MaxIdentifierLength),
                DataType = col.DataType,
                Length = col.Length,
                Precision = col.Precision,
                Scale = col.Scale,
                Selected = true,
                StripChars = col.StripChars
            });
        }

        return columns;
    }

    private static string BuildCreateTable(
        ISqlDialect dialect,
        string tableName,
        List<ColumnDefinition> columns,
        string? identityColumn)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {dialect.QuoteIdentifier(tableName)} (");

        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var isIdentity = !string.IsNullOrWhiteSpace(identityColumn)
                             && string.Equals(col.OriginalName, identityColumn, StringComparison.OrdinalIgnoreCase);
            var comma = i < columns.Count - 1 ? "," : string.Empty;
            sb.AppendLine("  " + dialect.ColumnDeclaration(col, isIdentity) + comma);
        }

        sb.Append(dialect.CreateTableSuffix);
        return sb.ToString();
    }

    private static List<List<string>> FilterRows(
        SheetData sheet,
        List<ColumnDefinition> columns,
        bool skipBlankRows,
        bool trimWhitespace)
    {
        var indexes = columns
            .Select(c => sheet.Headers.FindIndex(h =>
                string.Equals(h, c.OriginalName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var result = new List<List<string>>();
        foreach (var row in sheet.Rows)
        {
            var values = new List<string>(columns.Count);
            for (var i = 0; i < columns.Count; i++)
            {
                var idx = indexes[i];
                var raw = idx >= 0 && idx < row.Count ? row[idx] : string.Empty;
                raw = DataCleaner.Apply(raw, columns[i].StripChars);
                values.Add(trimWhitespace ? raw.Trim() : raw);
            }

            if (skipBlankRows && values.All(string.IsNullOrWhiteSpace))
                continue;

            result.Add(values);
        }

        return result;
    }

    private static string BuildInserts(
        ISqlDialect dialect,
        string tableName,
        List<ColumnDefinition> columns,
        List<List<string>> rows,
        bool trimWhitespace,
        bool hasIdentity)
    {
        if (rows.Count == 0)
            return "-- Sin filas para insertar." + Environment.NewLine;

        var quotedTable = dialect.QuoteIdentifier(tableName);
        var quotedCols = string.Join(", ", columns.Select(c => dialect.QuoteIdentifier(c.SqlName)));
        var sb = new StringBuilder();
        var begin = dialect.BeginDmlBlock(quotedTable, hasIdentity);
        var inBlock = begin == "BEGIN";
        if (!string.IsNullOrEmpty(begin))
            sb.AppendLine(begin);

        var batchSize = Math.Max(1, dialect.InsertBatchSize);
        for (var offset = 0; offset < rows.Count; offset += batchSize)
        {
            var batch = rows.Skip(offset).Take(batchSize).ToList();
            if (batchSize == 1)
            {
                var prefix = inBlock ? "  " : string.Empty;
                sb.Append(prefix);
                sb.Append($"INSERT INTO {quotedTable} ({quotedCols}) VALUES (");
                sb.Append(FormatRow(dialect, columns, batch[0], trimWhitespace));
                sb.AppendLine(");");
            }
            else
            {
                sb.AppendLine($"INSERT INTO {quotedTable} ({quotedCols}) VALUES");
                for (var i = 0; i < batch.Count; i++)
                {
                    var comma = i < batch.Count - 1 ? "," : ";";
                    sb.Append("  (");
                    sb.Append(FormatRow(dialect, columns, batch[i], trimWhitespace));
                    sb.AppendLine(")" + comma);
                }
            }
        }

        var end = dialect.EndDmlBlock(quotedTable, hasIdentity);
        if (!string.IsNullOrEmpty(end))
            sb.AppendLine(end);

        return sb.ToString();
    }

    private static string FormatRow(
        ISqlDialect dialect,
        List<ColumnDefinition> columns,
        List<string> values,
        bool trimWhitespace)
    {
        var parts = new string[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var raw = i < values.Count ? values[i] : string.Empty;
            parts[i] = dialect.FormatValue(raw, columns[i].DataType, trimWhitespace);
        }

        return string.Join(", ", parts);
    }
}
