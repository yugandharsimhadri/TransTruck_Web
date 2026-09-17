using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransTrack.Api.Auth;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

/// <summary>
/// Is this thing running, and is it talking to the database it thinks it is?
///
/// The anonymous check is deliberately thin: enough to tell a deployment
/// apart from a dead port, and enough to catch the failure that actually
/// happens here — the API starting fine while pointed at the wrong database,
/// or at a database it cannot reach. It names the provider and whether the
/// connection opens, never the connection string or any path.
///
/// The log tail underneath it needs EnterpriseAdmin, because a server log
/// spans every company on the installation.
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController(IDbContextFactory<AppDbContext> factory) : ControllerBase
{
    public record HealthResponse(
        string Status,
        string Version,
        string Environment,
        string Database,
        bool DatabaseConnected,
        DateTime ServerTimeUtc);

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<HealthResponse>> Get()
    {
        var connected = false;
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            connected = await db.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            // Worth a log line — an unreachable database is the thing this
            // endpoint exists to surface, and the caller only gets a bool.
            AppLog.Error("Health check could not reach the database.", ex);
        }

        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "unknown";

        var response = new HealthResponse(
            Status: connected ? "Healthy" : "Degraded",
            Version: version,
            Environment: System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Database: DbBootstrapper.UsePostgres ? "PostgreSQL" : "SQLite",
            DatabaseConnected: connected,
            ServerTimeUtc: DateTime.UtcNow);

        // 503 when the database is unreachable, so anything watching this
        // endpoint (a cloud host's health probe, a monitor) sees a failure
        // rather than a 200 with a field it never reads.
        return connected ? Ok(response) : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    public record LogResponse(string Directory, string File, int Lines, string[] Entries, string[] AvailableFiles);

    /// <summary>The tail of the current log file. EnterpriseAdmin only: this
    /// is the whole installation's log, not one company's.</summary>
    [HttpGet("logs")]
    [Authorize(Policy = Policies.RecoveryToken)]
    public ActionResult<LogResponse> GetLogs([FromQuery] int lines = 200)
    {
        lines = Math.Clamp(lines, 1, 2000);

        var directory = AppLog.LogDirectory;
        var file = AppLog.CurrentFile;

        var available = Directory.Exists(directory)
            ? new DirectoryInfo(directory).GetFiles("transtrack-*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => f.Name)
                .ToArray()
            : [];

        if (!System.IO.File.Exists(file))
            return Ok(new LogResponse(directory, Path.GetFileName(file), 0, [], available));

        // FileShare.ReadWrite: AppLog holds this file open to append, and a
        // read that insists on exclusive access would fail exactly when
        // something is being written — which is when the log is wanted.
        var all = new List<string>();
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            while (reader.ReadLine() is { } line) all.Add(line);
        }

        var tail = all.Count <= lines ? all.ToArray() : all.Skip(all.Count - lines).ToArray();

        return Ok(new LogResponse(directory, Path.GetFileName(file), tail.Length, tail, available));
    }
}
