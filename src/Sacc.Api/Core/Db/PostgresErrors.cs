using Npgsql;

namespace Sacc.Api.Core.Db;

public static class PostgresErrors
{
    public const string UniqueViolation = "23505";

    public static bool IsUniqueViolation(this Exception ex) =>
        ex is PostgresException pg && pg.SqlState == UniqueViolation;
}
