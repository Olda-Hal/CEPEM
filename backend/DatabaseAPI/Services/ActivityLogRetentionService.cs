using DatabaseAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

/// <summary>
/// Configuration for activity-log retention: the table grows without bound
/// otherwise (every gateway request is logged there).
/// </summary>
public class ActivityLogRetentionOptions
{
    public const string SectionName = "ActivityLogRetention";

    /// <summary>Logs older than this are deleted. Default: one year.</summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>E-mail log rows older than this are deleted. Default: one year.</summary>
    public int EmailLogRetentionDays { get; set; } = 365;

    /// <summary>How often the cleanup runs. Default: daily with a startup delay.</summary>
    public int CleanupIntervalHours { get; set; } = 24;

    /// <summary>Set false to disable the background cleanup entirely.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Periodic background cleanup of old ActivityLog rows. The actual deletion
/// is isolated in <see cref="ActivityLogRetention"/> for testability.
/// </summary>
public class ActivityLogRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityLogRetentionService> _logger;

    public ActivityLogRetentionService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ActivityLogRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        Options = configuration.GetSection(ActivityLogRetentionOptions.SectionName)
            .Get<ActivityLogRetentionOptions>() ?? new ActivityLogRetentionOptions();
    }

    public ActivityLogRetentionOptions Options { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Options.Enabled)
        {
            _logger.LogInformation("Activity log retention cleanup is disabled");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, Options.CleanupIntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var deleted = await ActivityLogRetention.DeleteOlderThanAsync(context, Options.RetentionDays, stoppingToken);
                if (deleted > 0)
                {
                    _logger.LogInformation("Activity log retention deleted {Count} rows older than {Days} days", deleted, Options.RetentionDays);
                }

                var deletedEmails = await EmailLogRetention.DeleteOlderThanAsync(
                    context, Options.EmailLogRetentionDays, stoppingToken);
                if (deletedEmails > 0)
                {
                    _logger.LogInformation(
                        "E-mail log retention deleted {Count} rows older than {Days} days",
                        deletedEmails, Options.EmailLogRetentionDays);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Activity log retention cleanup failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}

/// <summary>Pure deletion helper for the admin e-mail log.</summary>
public static class EmailLogRetention
{
    public static async Task<int> DeleteOlderThanAsync(DatabaseContext context, int retentionDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var query = context.EmailLogs.Where(log => log.CreatedAtUtc < cutoff);

        // ExecuteDelete keeps the whole table out of memory on MySQL; the
        // in-memory provider used by unit tests needs the tracked fallback.
        if (context.Database.IsRelational())
        {
            return await query.ExecuteDeleteAsync(cancellationToken);
        }

        var oldLogs = await query.ToListAsync(cancellationToken);
        context.EmailLogs.RemoveRange(oldLogs);
        await context.SaveChangesAsync(cancellationToken);
        return oldLogs.Count;
    }
}

/// <summary>Pure deletion helper so the retention rule can be unit tested.</summary>
public static class ActivityLogRetention
{
    public static async Task<int> DeleteOlderThanAsync(DatabaseContext context, int retentionDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var query = context.ActivityLogs.Where(log => log.Timestamp < cutoff);

        if (context.Database.IsRelational())
        {
            return await query.ExecuteDeleteAsync(cancellationToken);
        }

        var oldLogs = await query.ToListAsync(cancellationToken);
        context.ActivityLogs.RemoveRange(oldLogs);
        await context.SaveChangesAsync(cancellationToken);
        return oldLogs.Count;
    }
}
