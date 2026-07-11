namespace ExcelToSql.Models;

public sealed class SpreadsheetDocument
{
    public string FileName { get; set; } = string.Empty;
    public List<SheetData> Sheets { get; set; } = [];
}
