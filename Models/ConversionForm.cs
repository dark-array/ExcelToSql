namespace ExcelToSql.Models;

public sealed class ConversionForm
{
    public DatabaseEngine Engine { get; set; } = DatabaseEngine.MariaDb;
    public SqlAction Action { get; set; } = SqlAction.Both;
    public bool SkipBlankRows { get; set; } = true;
    public bool TrimWhitespace { get; set; } = true;
    public bool FirstRowIsHeader { get; set; } = true;
    public List<SheetForm> Sheets { get; set; } = [];
}

public sealed class SheetForm
{
    public bool Included { get; set; } = true;
    public string SheetName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IdentityColumn { get; set; } = string.Empty;
    public List<ColumnForm> Columns { get; set; } = [];
}

public sealed class ColumnForm
{
    public bool Selected { get; set; } = true;
    public string OriginalName { get; set; } = string.Empty;
    public string SqlName { get; set; } = string.Empty;
    public LogicalType DataType { get; set; } = LogicalType.Text;
    public int Length { get; set; } = 255;
    public int Precision { get; set; } = 18;
    public int Scale { get; set; } = 2;
    public string StripChars { get; set; } = string.Empty;
}
