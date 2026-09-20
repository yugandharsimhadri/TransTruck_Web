using Npgsql;

namespace TransTrack.Automation;

/// <summary>
/// A PostgreSQL database that exists only for the length of one run.
///
/// Until the move to PostgreSQL this class did not need to exist: the API ran on a file, so pointing
/// TRANSTRUCKWEB_DB at a throwaway path under artifacts/ was the whole of the isolation story. A
/// server-hosted database has no equivalent — every run of this suite reaches the same server as the
/// real installation, so isolation has to be built rather than assumed.
///
/// It is built here as a separate database per run, created before the API starts and dropped after
/// it stops. The API then applies its own migrations into it exactly as it would on a new server,
/// which keeps the property the SQLite version had: the schema under test is the one the product
/// creates, not one the harness knows how to build.
///
/// Two things guard the real data. The database name is generated per run from <see cref="Prefix"/>
/// and a timestamp, so it cannot collide with an existing one; and <see cref="DropAsync"/> refuses
/// to drop any database whose name does not start with that prefix, so a mistake in wiring the
/// connection string up cannot turn teardown into data loss.
/// </summary>
public sealed class UatDatabase : IAsyncDisposable
{
    /// <summary>Every database this class creates starts with this, and it will drop nothing else.</summary>
    public const string Prefix = "transtruck_uat_";

    /// <summary>
    /// Connected to for CREATE and DROP, because neither can run from inside the database it targets.
    /// "postgres" exists on every standard install and is what psql defaults to.
    /// </summary>
    private const string MaintenanceDatabase = "postgres";

    private readonly string _maintenanceConnectionString;
    private readonly Action<string>? _log;
    private readonly bool _keep;
    private bool _dropped;

    private UatDatabase(string name, string connectionString, string maintenanceConnectionString, bool keep, Action<string>? log)
    {
        Name = name;
        ConnectionString = connectionString;
        _maintenanceConnectionString = maintenanceConnectionString;
        _keep = keep;
        _log = log;
    }

    /// <summary>The generated database name, e.g. transtruck_uat_20260920_143012_771.</summary>
    public string Name { get; }

    /// <summary>What the API is given as TRANSTRUCKWEB_PG_CONNECTION.</summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Creates the run's database on the server named by <paramref name="adminConnectionString"/>.
    /// That string's own Database is ignored — only the host, port and credentials are taken from it,
    /// so handing this the production connection string is safe and is the expected usage.
    /// </summary>
    public static async Task<UatDatabase> CreateAsync(
        string adminConnectionString,
        bool keep,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"TRANSTRUCK_UAT_PG_CONNECTION is not a PostgreSQL connection string: {ex.Message} " +
                "It should look like \"Host=localhost;Database=postgres;Username=...;Password=...\".", ex);
        }

        // Lowercase and unquoted-identifier-safe, so nothing downstream has to quote it. A timestamp
        // rather than a GUID because a leftover database from a killed run should say when it was
        // made — that is the difference between recognising it as debris and being afraid of it.
        var name = Prefix + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

        var maintenance = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = MaintenanceDatabase };
        var target = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = name };

        log?.Invoke($"Creating the run's database {name} on {Describe(builder)}");

        try
        {
            await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            throw new InvalidOperationException(
                $"The user '{builder.Username}' may not create databases, so this run has nowhere " +
                "isolated to put its data — and it will not fall back to an existing one. Either " +
                $"grant it the right (ALTER ROLE {builder.Username} CREATEDB) or point " +
                "TRANSTRUCK_UAT_PG_CONNECTION at a user that already has it.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not reach PostgreSQL at {Describe(builder)} to create the run's database: {ex.Message}", ex);
        }

        return new UatDatabase(name, target.ConnectionString, maintenance.ConnectionString, keep, log);
    }

    /// <summary>
    /// Drops the database. Safe to call twice, and it will not throw: teardown running after a failed
    /// run must not replace the failure that actually matters with its own.
    /// </summary>
    public async Task DropAsync()
    {
        if (_dropped) return;
        _dropped = true;

        if (_keep)
        {
            _log?.Invoke($"Keeping {Name} (TRANSTRUCK_UAT_KEEP_DB=true). Drop it with: DROP DATABASE \"{Name}\";");
            return;
        }

        // The belt to the generated name's braces. Nothing reaches this method with a name from
        // outside this class today, and this is what keeps that true if something ever does.
        if (!Name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            _log?.Invoke($"Refusing to drop '{Name}': not a database this run created.");
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_maintenanceConnectionString);
            await connection.OpenAsync();

            // The API's connection pool can outlive the process by moments, and DROP DATABASE fails
            // outright while any session is still attached — so close them first rather than leave
            // teardown to lose a race it has no reason to be in.
            await using (var disconnect = new NpgsqlCommand(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()",
                connection))
            {
                disconnect.Parameters.AddWithValue("name", Name);
                await disconnect.ExecuteNonQueryAsync();
            }

            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Name}\"", connection);
            await drop.ExecuteNonQueryAsync();

            _log?.Invoke($"Dropped the run's database {Name}");
        }
        catch (Exception ex)
        {
            // Said out loud, because the consequence is a database left on the server: silence here
            // is how a machine accumulates fifty of them before anyone notices.
            _log?.Invoke($"Could not drop {Name} ({ex.Message}). Drop it by hand: DROP DATABASE \"{Name}\";");
        }
    }

    /// <summary>Host and user only — never the password, since this goes to a console and a log file.</summary>
    private static string Describe(NpgsqlConnectionStringBuilder builder)
        => $"{builder.Host}:{builder.Port} as {builder.Username}";

    public async ValueTask DisposeAsync() => await DropAsync();
}
