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

    public async Task ApplyMigrationsAsync()
    {
        try
        {
            _logger.LogInformation("Applying database migrations...");
            
            var pendingMigrations = await GetPendingMigrationsAsync();
            var pendingCount = pendingMigrations.Count();
            
            if (pendingCount > 0)
            {
                _logger.LogInformation($"Found {pendingCount} pending migrations: {string.Join(", ", pendingMigrations)}");
                await _context.Database.MigrateAsync();
                _logger.LogInformation("Database migrations applied successfully.");
            }
            else
            {
                _logger.LogInformation("No pending migrations found. Database is up to date.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply database migrations.");
            throw;
        }
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
        try
        {
            return await _context.Database.GetPendingMigrationsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending migrations.");
            return Enumerable.Empty<string>();
        }
    }

    public async Task<IEnumerable<string>> GetAppliedMigrationsAsync()
    {
        try
        {
            return await _context.Database.GetAppliedMigrationsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get applied migrations.");
            return Enumerable.Empty<string>();
        }
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
