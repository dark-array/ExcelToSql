namespace ExcelToSql.Models;

public sealed class SheetData
{
    public string Name { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
    public List<ColumnDefinition> Columns { get; set; } = [];
}
