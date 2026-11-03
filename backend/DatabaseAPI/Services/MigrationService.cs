using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;

namespace DatabaseAPI.Services;

public interface IMigrationService
{
    Task ApplyMigrationsAsync();
    Task<bool> CanConnectAsync();
    Task<IEnumerable<string>> GetPendingMigrationsAsync();
    Task<IEnumerable<string>> GetAppliedMigrationsAsync();
    Task EnsureDatabaseCreatedAsync();
}

public class MigrationService : IMigrationService
{
    private readonly DatabaseContext _context;
    private readonly ILogger<MigrationService> _logger;

    public MigrationService(DatabaseContext context, ILogger<MigrationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // MySQL advisory lock scoped to the migration connection. Without it two
    // replicas starting together can race the same migration.
    private const string MigrationLockName = "cepem_database_migrations";
    private const int MigrationLockTimeoutSeconds = 60;

    public async Task ApplyMigrationsAsync()
    {
        _logger.LogInformation("Applying database migrations...");

        var pendingMigrations = (await _context.Database.GetPendingMigrationsAsync()).ToList();
        if (pendingMigrations.Count == 0)
        {
            _logger.LogInformation("No pending migrations found. Database is up to date.");
            return;
        }

        _logger.LogInformation(
            "Found {Count} pending migrations: {Migrations}",
            pendingMigrations.Count,
            string.Join(", ", pendingMigrations));

        var connection = _context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await _context.Database.OpenConnectionAsync();
        }

        try
        {
            await AcquireLockAsync(connection);
            try
            {
                // Another replica may have applied the migrations while we
                // waited for the lock, so re-check before running them.
                var stillPending = (await _context.Database.GetPendingMigrationsAsync()).ToList();
                if (stillPending.Count > 0)
                {
                    await _context.Database.MigrateAsync();
                }

                _logger.LogInformation("Database migrations applied successfully.");
            }
            finally
            {
                await ReleaseLockAsync(connection);
            }
        }
        finally
        {
            if (openedHere)
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
    }

    private static async Task AcquireLockAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT GET_LOCK(@lockName, @timeout)";
        AddParameter(command, "@lockName", MigrationLockName);
        AddParameter(command, "@timeout", MigrationLockTimeoutSeconds);

        var result = await command.ExecuteScalarAsync();
        if (result is null || Convert.ToInt32(result) != 1)
        {
            throw new TimeoutException(
                $"Could not acquire the database migration lock within {MigrationLockTimeoutSeconds} seconds.");
        }
    }

    private static async Task ReleaseLockAsync(DbConnection connection)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT RELEASE_LOCK(@lockName)";
            AddParameter(command, "@lockName", MigrationLockName);
            await command.ExecuteScalarAsync();
        }
        catch
        {
            // Releasing is best-effort; the lock is freed when the connection
            // closes anyway.
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public async Task<bool> CanConnectAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot connect to database.");
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetPendingMigrationsAsync()
    {
        return await _context.Database.GetPendingMigrationsAsync();
    }

    public async Task<IEnumerable<string>> GetAppliedMigrationsAsync()
    {
        return await _context.Database.GetAppliedMigrationsAsync();
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        try
        {
            _logger.LogInformation("Ensuring database is created from current models...");
            
            var created = await _context.Database.EnsureCreatedAsync();
            
            if (created)
            {
                _logger.LogInformation("Database created successfully from Entity Framework models.");
            }
            else
            {
                _logger.LogInformation("Database already exists.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure database creation.");
            throw;
        }
    }
}
