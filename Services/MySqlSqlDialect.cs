namespace ExcelToSql.Services;

public sealed class MySqlSqlDialect : MariaDbSqlDialect
{
    public override string DisplayName => "MySQL";
}
