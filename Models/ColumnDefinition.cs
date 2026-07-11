namespace ExcelToSql.Models;

public sealed class ColumnDefinition
{
    public string OriginalName { get; set; } = string.Empty;
    public string SqlName { get; set; } = string.Empty;
    public LogicalType DataType { get; set; } = LogicalType.Text;
    public int Length { get; set; } = 255;
    public int Precision { get; set; } = 18;
    public int Scale { get; set; } = 2;
    public bool Selected { get; set; } = true;
    public string StripChars { get; set; } = string.Empty;
}
