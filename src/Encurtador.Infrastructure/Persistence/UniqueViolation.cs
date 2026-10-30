using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Encurtador.Infrastructure.Persistence;

/// <summary>Detecta violação de índice único, por provider (research.md D1, D6).</summary>
internal static class UniqueViolation
{
    private const int SqliteConstraint = 19;
    private const int SqliteConstraintUnique = 2067;

    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException
        {
            SqliteErrorCode: SqliteConstraint,
            SqliteExtendedErrorCode: SqliteConstraintUnique,
        };

    // Ponto de extensão para PostgreSQL: Npgsql.PostgresException com SqlState == "23505".
}
