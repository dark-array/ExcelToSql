using System.Globalization;
using System.Text;
using ExcelToSql.Models;

namespace ExcelToSql.Services;

public sealed class SqlServerSqlDialect : ISqlDialect
{
    public string DisplayName => "SQL Server";
    public int MaxIdentifierLength => 128;
    public int InsertBatchSize => 100;
    public string CreateTableSuffix => ");";
    public string ScriptFooter => string.Empty;

    public string QuoteIdentifier(string name) => "[" + name.Replace("]", "]]") + "]";

    public string ColumnDeclaration(ColumnDefinition column, bool isIdentity)
    {
        var ident = QuoteIdentifier(column.SqlName);
        if (isIdentity)
        {
            var numeric = column.DataType == LogicalType.BigInteger ? "BIGINT" : "INT";
            return $"{ident} {numeric} IDENTITY(1,1) NOT NULL PRIMARY KEY";
        }

        var type = column.DataType switch
        {
            LogicalType.Integer => "INT",
            LogicalType.BigInteger => "BIGINT",
            LogicalType.Decimal => $"DECIMAL({Math.Clamp(column.Precision, 1, 38)},{Math.Clamp(column.Scale, 0, 10)})",
            LogicalType.Boolean => "BIT",
            LogicalType.Date => "DATE",
            LogicalType.DateTime => "DATETIME2",
            LogicalType.LongText => "NVARCHAR(MAX)",
            _ => column.Length > 4000
                ? "NVARCHAR(MAX)"
                : $"NVARCHAR({Math.Clamp(column.Length <= 0 ? 255 : column.Length, 1, 4000)})"
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

    public string BeginDmlBlock(string quotedTable, bool hasIdentity)
        => hasIdentity ? $"SET IDENTITY_INSERT {quotedTable} ON;" : string.Empty;

    public string EndDmlBlock(string quotedTable, bool hasIdentity)
        => hasIdentity ? $"SET IDENTITY_INSERT {quotedTable} OFF;" : string.Empty;

    public string ScriptHeader(string fileName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- ExcelToSql");
        sb.AppendLine($"-- Archivo : {fileName}");
        sb.AppendLine("-- Motor   : SQL Server");
        sb.AppendLine($"-- Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("-- IDENTITY se inserta con IDENTITY_INSERT para conservar los valores originales.");
        sb.AppendLine("SET NOCOUNT ON;");
        sb.AppendLine("SET XACT_ABORT ON;");
        return sb.ToString();
    }

    private static string QuoteString(string value) => "N'" + value.Replace("'", "''") + "'";
}
