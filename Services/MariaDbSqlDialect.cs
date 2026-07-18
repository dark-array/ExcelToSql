using System.Globalization;
using System.Text;
using ExcelToSql.Models;

namespace ExcelToSql.Services;

public class MariaDbSqlDialect : ISqlDialect
{
    public virtual string DisplayName => "MariaDB";
    public int MaxIdentifierLength => 64;
    public int InsertBatchSize => 100;
    public string CreateTableSuffix => ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
    public string ScriptFooter => "SET FOREIGN_KEY_CHECKS = 1;";

    public string QuoteIdentifier(string name) => "`" + name.Replace("`", "``") + "`";

    public string ColumnDeclaration(ColumnDefinition column, bool isIdentity)
    {
        var ident = QuoteIdentifier(column.SqlName);
        if (isIdentity)
        {
            var numeric = column.DataType is LogicalType.Integer or LogicalType.BigInteger or LogicalType.Decimal
                ? (column.DataType == LogicalType.BigInteger ? "BIGINT" : "INT")
                : "INT";
            return $"{ident} {numeric} NOT NULL AUTO_INCREMENT PRIMARY KEY";
        }

        var type = column.DataType switch
        {
            LogicalType.Integer => "INT",
            LogicalType.BigInteger => "BIGINT",
            LogicalType.Decimal => $"DECIMAL({Math.Clamp(column.Precision, 1, 38)},{Math.Clamp(column.Scale, 0, 10)})",
            LogicalType.Boolean => "TINYINT(1)",
            LogicalType.Date => "DATE",
            LogicalType.DateTime => "DATETIME",
            LogicalType.LongText => "LONGTEXT",
            _ => $"VARCHAR({Math.Clamp(column.Length <= 0 ? 255 : column.Length, 1, 16383)})"
        };

        return $"{ident} {type}";
    }

    public string FormatValue(string? raw, LogicalType type, bool trimWhitespace)
    {
        if (raw is null)
            return "NULL";

        var value = trimWhitespace ? raw.Trim() : raw;
        if (value.Length == 0)
            return "NULL";

        switch (type)
        {
            case LogicalType.Integer:
            case LogicalType.BigInteger:
                return TypeInference.IsInteger(value) ? value.Trim() : QuoteString(value);
            case LogicalType.Decimal:
                return TypeInference.TryParseDecimal(value, out var number)
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : QuoteString(value);
            case LogicalType.Boolean:
                var flag = TypeInference.ParseBoolean(value);
                return flag is null ? QuoteString(value) : (flag.Value ? "1" : "0");
            case LogicalType.Date:
                return TypeInference.TryParseDateTime(value, out var date)
                    ? $"'{date:yyyy-MM-dd}'"
                    : QuoteString(value);
            case LogicalType.DateTime:
                return TypeInference.TryParseDateTime(value, out var dt)
                    ? $"'{dt:yyyy-MM-dd HH:mm:ss}'"
                    : QuoteString(value);
            default:
                return QuoteString(value);
        }
    }

    public string BeginDmlBlock(string quotedTable, bool hasIdentity) => string.Empty;
    public string EndDmlBlock(string quotedTable, bool hasIdentity) => string.Empty;

    public string ScriptHeader(string fileName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- ExcelToSql");
        sb.AppendLine($"-- Archivo : {fileName}");
        sb.AppendLine($"-- Motor   : {DisplayName}");
        sb.AppendLine($"-- Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("-- IDENTITY se crea AUTO_INCREMENT e INSERT incluye los valores originales.");
        sb.AppendLine("SET NAMES utf8mb4;");
        sb.AppendLine("SET FOREIGN_KEY_CHECKS = 0;");
        return sb.ToString();
    }

    private static string QuoteString(string value)
        => "'" + value.Replace(@"\", @"\\").Replace("'", "''") + "'";
}
