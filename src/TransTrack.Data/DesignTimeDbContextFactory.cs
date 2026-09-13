using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TransTrack.Data;

/// <summary>
/// Used only by `dotnet ef`. Adding a migration needs no real database — the
/// tools read the model, not a file — so it defaults to a throwaway one. The
/// same environment variable the application honours points the tools at a
/// real file when one is needed:
///
///     $env:TRANSTRUCKWEB_DB = "C:\TransTruckWeb\DB\TransTruckWeb.db"
///     dotnet ef migrations list --project src\TransTrack.Data --startup-project src\TransTrack.Data
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Same TRANSTRUCKWEB_PG_CONNECTION switch the running app honours, so
        // `dotnet ef migrations add` scaffolds against whichever provider the
        // app would actually start against.
        if (DbBootstrapper.UsePostgres)
        {
            var pgOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(DbBootstrapper.PostgresConnectionString)
                .Options;

            return new AppDbContext(pgOptions);
        }

        var path = Environment.GetEnvironmentVariable(DbBootstrapper.PathOverrideVariable);
        if (string.IsNullOrWhiteSpace(path)) path = "design.db";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        return new AppDbContext(options);
    }
}
