using Microsoft.EntityFrameworkCore;

namespace SecWeb.Data
{
    // =============================================================
    // POSTGRESQL PRODUCTION CONTEXT
    // =============================================================
    //
    // ApplicationDbContext remains the normal SQL Server / LocalDB
    // context used during Windows development.
    //
    // This derived context uses exactly the same SecWeb model but
    // maintains a separate PostgreSQL migration history.
    //

    public sealed class PostgresApplicationDbContext
        : ApplicationDbContext
    {
        public PostgresApplicationDbContext(
            DbContextOptions<PostgresApplicationDbContext> options)
            : base(options)
        {
        }
    }
}