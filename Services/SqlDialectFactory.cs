using ExcelToSql.Models;

namespace ExcelToSql.Services;

public static class SqlDialectFactory
{
    public static ISqlDialect Create(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.Oracle => new OracleSqlDialect(),
        DatabaseEngine.Postgres => new PostgresSqlDialect(),
        DatabaseEngine.SqlServer => new SqlServerSqlDialect(),
        DatabaseEngine.MySql => new MySqlSqlDialect(),
        _ => new MariaDbSqlDialect()
    };
}
