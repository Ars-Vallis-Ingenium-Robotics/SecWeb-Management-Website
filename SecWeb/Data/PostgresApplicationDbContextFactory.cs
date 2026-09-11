using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SecWeb.Data
{
    // =============================================================
    // POSTGRESQL DESIGN-TIME CONTEXT FACTORY
    // =============================================================
    //
    // Entity Framework uses this class when creating PostgreSQL
    // migrations.
    //
    // This avoids starting:
    //
    // - Chipy
    // - Identity seeding
    // - Email
    // - the website
    //
    // just to generate a migration.
    //

    public sealed class PostgresApplicationDbContextFactory
        : IDesignTimeDbContextFactory<
            PostgresApplicationDbContext>
    {
        public PostgresApplicationDbContext CreateDbContext(
            string[] args)
        {
            string connectionString =
                Environment.GetEnvironmentVariable(
                    "ConnectionStrings__DefaultConnection")

                ?? Environment.GetEnvironmentVariable(
                    "SECWEB_POSTGRES_DESIGN_CONNECTION")

                ??
                "Host=127.0.0.1;Port=5432;Database=secweb;Username=secweb_app;Password=design-time-only";


            var optionsBuilder =
                new DbContextOptionsBuilder<
                    PostgresApplicationDbContext>();


            optionsBuilder.UseNpgsql(
                connectionString);


            return new PostgresApplicationDbContext(
                optionsBuilder.Options);
        }
    }
}