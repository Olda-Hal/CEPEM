using System.Linq.Expressions;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

/// <summary>
/// Configuration for activity-log retention. Every gateway request is logged,
/// so without a tiered policy the table grows without bound.
///
/// Routine traffic (successful GET/HEAD requests) is kept for a short window;
/// security/audit-relevant rows (errors, mutations, auth/admin/GDPR/evaluation
/// operations) are kept longer. A hard row cap guarantees that even a request
/// flood cannot make the table unbounded.
/// </summary>
public class ActivityLogRetentionOptions
{
    public const string SectionName = "ActivityLogRetention";

    /// <summary>Legacy alias for <see cref="ImportantRetentionDays"/>.</summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>How long routine successful read requests are kept.</summary>
    public int RoutineRetentionDays { get; set; } = 30;

    /// <summary>How long audit-relevant rows are kept. 0 = use RetentionDays.</summary>
    public int ImportantRetentionDays { get; set; }

    /// <summary>Hard upper bound for ActivityLog rows.</summary>
    public int MaxRows { get; set; } = 500_000;

    /// <summary>E-mail log rows older than this are deleted.</summary>
    public int EmailLogRetentionDays { get; set; } = 365;

    /// <summary>Hard upper bound for EmailLog rows.</summary>
    public int MaxEmailLogRows { get; set; } = 200_000;

    /// <summary>Rows removed per statement, keeping locks short.</summary>
    public int BatchSize { get; set; } = 5_000;

    /// <summary>How often the cleanup runs. Default: every six hours.</summary>
    public int CleanupIntervalHours { get; set; } = 6;

    /// <summary>Set false to disable the background cleanup entirely.</summary>
    public bool Enabled { get; set; } = true;

    public int EffectiveImportantRetentionDays =>
        ImportantRetentionDays > 0 ? ImportantRetentionDays : RetentionDays;
}

/// <summary>
/// Periodic bounded cleanup of ActivityLog and EmailLog. The actual deletion
/// is isolated in the static helpers for testability.
/// </summary>
public class ActivityLogRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityLogRetentionService> _logger;

    public ActivityLogRetentionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ActivityLogRetentionService> logger)
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

        // Let migrations/seeding and the first requests settle before a large
        // cleanup starts competing for the database.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, Options.CleanupIntervalHours));
        var importantDays = Options.EffectiveImportantRetentionDays;
        var routineDays = Math.Clamp(Options.RoutineRetentionDays, 1, importantDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                var deletedExpired = await ActivityLogRetention.DeleteExpiredAsync(
                    context, routineDays, importantDays, Options.BatchSize, stoppingToken);
                var capped = await ActivityLogRetention.EnforceMaxRowsAsync(
                    context, Options.MaxRows, Options.BatchSize, stoppingToken);

                var deletedEmails = await EmailLogRetention.DeleteOlderThanAsync(
                    context, Options.EmailLogRetentionDays, Options.BatchSize, stoppingToken);
                var cappedEmails = await EmailLogRetention.EnforceMaxRowsAsync(
                    context, Options.MaxEmailLogRows, Options.BatchSize, stoppingToken);

                if (deletedExpired + capped + deletedEmails + cappedEmails > 0)
                {
                    _logger.LogInformation(
                        "Log retention: activity expired={Expired}, activity cap={Capped}, " +
                        "e-mail expired={EmailExpired}, e-mail cap={EmailCapped} " +
                        "(routine {RoutineDays}d, important {ImportantDays}d, max {MaxRows}/{MaxEmailRows})",
                        deletedExpired, capped, deletedEmails, cappedEmails,
                        routineDays, importantDays, Options.MaxRows, Options.MaxEmailLogRows);
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

/// <summary>Pure deletion helpers for the admin e-mail log.</summary>
public static class EmailLogRetention
{
    public static async Task<int> DeleteOlderThanAsync(
        DatabaseContext context,
        int retentionDays,
        CancellationToken cancellationToken = default)
        => await DeleteOlderThanAsync(context, retentionDays, batchSize: 0, cancellationToken);

    public static async Task<int> DeleteOlderThanAsync(
        DatabaseContext context,
        int retentionDays,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var query = context.EmailLogs.Where(log => log.CreatedAtUtc < cutoff);

        if (batchSize <= 0)
        {
            // Legacy callers (and small deployments) keep the single statement.
            if (context.Database.IsRelational())
            {
                return await query.ExecuteDeleteAsync(cancellationToken);
            }

            var oldLogs = await query.ToListAsync(cancellationToken);
            context.EmailLogs.RemoveRange(oldLogs);
            await context.SaveChangesAsync(cancellationToken);
            return oldLogs.Count;
        }

        return await LogRetention.DeleteBatchedAsync(
            context,
            context.EmailLogs.Where(log => log.CreatedAtUtc < cutoff),
            log => log.CreatedAtUtc,
            batchSize,
            maxRows: null,
            cancellationToken);
    }

    /// <summary>Delete oldest rows until the table is at most <paramref name="maxRows"/>.</summary>
    public static async Task<int> EnforceMaxRowsAsync(
        DatabaseContext context,
        int maxRows,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var total = await context.EmailLogs.LongCountAsync(cancellationToken);
        var excess = total - Math.Max(0, maxRows);
        if (excess <= 0)
        {
            return 0;
        }

        return await LogRetention.DeleteBatchedAsync(
            context,
            context.EmailLogs,
            log => log.CreatedAtUtc,
            Math.Max(1, batchSize),
            (int)Math.Min(excess, int.MaxValue),
            cancellationToken);
    }
}

/// <summary>Pure deletion helpers so the retention rules can be unit tested.</summary>
public static class ActivityLogRetention
{
    /// <summary>
    /// Audit-relevant rows: failures, state-changing requests and sensitive
    /// areas. Everything else is routine read traffic with a short lifetime.
    /// </summary>
    private static readonly Expression<Func<ActivityLog, bool>> Important = log =>
        (log.StatusCode != null && log.StatusCode >= 400) ||
        (log.Method != null && log.Method != "GET" && log.Method != "HEAD" && log.Method != "OPTIONS") ||
        log.Action.Contains("/api/auth") ||
        log.Action.Contains("/api/admin") ||
        log.Action.Contains("/api/gdpr") ||
        log.Action.Contains("/api/ai") ||
        log.Action.Contains("/api/evaluations") ||
        log.Action.Contains("/merge") ||
        log.Action.Contains("/export");

    private static readonly Expression<Func<ActivityLog, bool>> Routine = log =>
        !((log.StatusCode != null && log.StatusCode >= 400) ||
          (log.Method != null && log.Method != "GET" && log.Method != "HEAD" && log.Method != "OPTIONS") ||
          log.Action.Contains("/api/auth") ||
          log.Action.Contains("/api/admin") ||
          log.Action.Contains("/api/gdpr") ||
          log.Action.Contains("/api/ai") ||
          log.Action.Contains("/api/evaluations") ||
          log.Action.Contains("/merge") ||
          log.Action.Contains("/export"));

    /// <summary>Legacy: delete every row older than one cutoff.</summary>
    public static async Task<int> DeleteOlderThanAsync(
        DatabaseContext context,
        int retentionDays,
        CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Delete rows past their tier's cutoff in short batches:
    /// everything older than <paramref name="importantDays"/>, then routine
    /// rows older than <paramref name="routineDays"/>.
    /// </summary>
    public static async Task<int> DeleteExpiredAsync(
        DatabaseContext context,
        int routineDays,
        int importantDays,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var batch = Math.Max(1, batchSize);
        var importantCutoff = DateTime.UtcNow.AddDays(-Math.Max(0, importantDays));
        var routineCutoff = DateTime.UtcNow.AddDays(-Math.Max(0, routineDays));

        var deleted = await LogRetention.DeleteBatchedAsync(
            context,
            context.ActivityLogs.Where(log => log.Timestamp < importantCutoff),
            log => log.Timestamp,
            batch,
            maxRows: null,
            cancellationToken);

        deleted += await LogRetention.DeleteBatchedAsync(
            context,
            context.ActivityLogs.Where(log => log.Timestamp < routineCutoff).Where(Routine),
            log => log.Timestamp,
            batch,
            maxRows: null,
            cancellationToken);

        return deleted;
    }

    /// <summary>
    /// Guarantee the table stays bounded. Routine rows are removed oldest-first;
    /// if the cap is still exceeded, the oldest audit rows go as well.
    /// </summary>
    public static async Task<int> EnforceMaxRowsAsync(
        DatabaseContext context,
        int maxRows,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var batch = Math.Max(1, batchSize);
        var total = await context.ActivityLogs.CountAsync(cancellationToken);
        var excess = total - Math.Max(0, maxRows);
        if (excess <= 0)
        {
            return 0;
        }

        var deleted = await LogRetention.DeleteBatchedAsync(
            context,
            context.ActivityLogs.Where(Routine),
            log => log.Timestamp,
            batch,
            excess,
            cancellationToken);
        excess -= deleted;

        if (excess > 0)
        {
            deleted += await LogRetention.DeleteBatchedAsync(
                context,
                context.ActivityLogs,
                log => log.Timestamp,
                batch,
                excess,
                cancellationToken);
        }

        return deleted;
    }

    /// <summary>Exposed for diagnostics/tests.</summary>
    public static bool IsImportant(ActivityLog log) => Important.Compile()(log);
}

/// <summary>Shared batched-delete mechanics for both log tables.</summary>
internal static class LogRetention
{
    /// <summary>
    /// Deletes oldest matching rows in bounded batches. <paramref name="maxRows"/>
    /// limits how many rows this call removes (null = every match).
    /// </summary>
    public static async Task<int> DeleteBatchedAsync<TEntity>(
        DatabaseContext context,
        IQueryable<TEntity> query,
        Expression<Func<TEntity, DateTime>> orderBy,
        int batchSize,
        int? maxRows,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var total = 0;
        var limit = maxRows ?? int.MaxValue;

        while (total < limit)
        {
            var take = Math.Min(Math.Max(1, batchSize), limit - total);
            var batch = await query
                .OrderBy(orderBy)
                .Take(take)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            total += await DeleteEntitiesAsync(context, batch, cancellationToken);
            if (batch.Count < take)
            {
                break;
            }
        }

        return total;
    }

    private static async Task<int> DeleteEntitiesAsync<TEntity>(
        DatabaseContext context,
        List<TEntity> batch,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (context.Database.IsRelational())
        {
            var ids = batch
                .Select(entity => (int)context.Entry(entity).Property("Id").CurrentValue!)
                .ToList();

            var deleted = await context.Set<TEntity>()
                .Where(entity => ids.Contains(EF.Property<int>(entity, "Id")))
                .ExecuteDeleteAsync(cancellationToken);

            // ExecuteDelete bypasses the change tracker; detach the loaded
            // rows so a long cleanup does not keep every batch in memory.
            foreach (var entity in batch)
            {
                context.Entry(entity).State = EntityState.Detached;
            }

            return deleted;
        }

        context.RemoveRange(batch);
        await context.SaveChangesAsync(cancellationToken);
        return batch.Count;
    }
}
