using ExcelToSql.Models;

namespace ExcelToSql.Services;

public interface ISqlDialect
{
    string DisplayName { get; }
    int MaxIdentifierLength { get; }
    int InsertBatchSize { get; }
    string CreateTableSuffix { get; }
    string ScriptFooter { get; }
    string QuoteIdentifier(string name);
    string ColumnDeclaration(ColumnDefinition column, bool isIdentity);
    string FormatValue(string? raw, LogicalType type, bool trimWhitespace);
    string BeginDmlBlock(string quotedTable, bool hasIdentity);
    string EndDmlBlock(string quotedTable, bool hasIdentity);
    string ScriptHeader(string fileName);
}
