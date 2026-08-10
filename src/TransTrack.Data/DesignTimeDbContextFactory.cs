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
        var path = Environment.GetEnvironmentVariable(DbBootstrapper.PathOverrideVariable);
        if (string.IsNullOrWhiteSpace(path)) path = "design.db";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        return new AppDbContext(options);
    }
}
