using Cepem.Shared;
using DatabaseAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public sealed class PiiLogBackfillOptions
{
    public const string SectionName = "PiiRedaction";

    /// <summary>Mask PII in rows written before redaction was introduced.</summary>
    public bool BackfillOnStartup { get; set; } = true;

    public int BatchSize { get; set; } = 500;
}

/// <summary>
/// One-shot startup migration for historical log rows. New rows are masked by
/// the writers; this makes sure the admin page/CSV and the database itself do
/// not keep plaintext e-mails, names, IPs or intake-link tokens forever.
/// </summary>
public class PiiLogBackfillService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PiiLogBackfillService> _logger;

    public PiiLogBackfillService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PiiLogBackfillService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _configuration
            .GetSection(PiiLogBackfillOptions.SectionName)
            .Get<PiiLogBackfillOptions>() ?? new PiiLogBackfillOptions();
        if (!options.BackfillOnStartup)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var emailRows = await BackfillEmailLogsAsync(context, options.BatchSize, stoppingToken);
            var activityRows = await BackfillActivityLogsAsync(context, options.BatchSize, stoppingToken);

            if (emailRows + activityRows > 0)
            {
                _logger.LogInformation(
                    "PII backfill masked {EmailRows} e-mail-log and {ActivityRows} activity-log rows",
                    emailRows, activityRows);
            }
        }
        catch (OperationCanceledException)
        {
            // Application is stopping.
        }
        catch (Exception ex)
        {
            // Read/export masking still protects the UI and CSV if the backfill
            // cannot run (e.g. read-only database).
            _logger.LogWarning(ex, "PII log backfill failed");
        }
    }

    private static async Task<int> BackfillEmailLogsAsync(
        DatabaseContext context, int batchSize, CancellationToken cancellationToken)
    {
        var changed = 0;
        var lastId = 0;
        batchSize = Math.Clamp(batchSize, 1, 5000);

        while (true)
        {
            var rows = await context.EmailLogs
                .Where(log => log.Id > lastId)
                .OrderBy(log => log.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                break;
            }

            foreach (var row in rows)
            {
                var toAddress = PiiRedactor.MaskEmail(row.ToAddress) ?? string.Empty;
                var recipientName = PiiRedactor.MaskName(row.RecipientName);
                var subject = PiiRedactor.MaskEmails(row.Subject) ?? string.Empty;
                var error = PiiRedactor.MaskEmails(row.Error);

                if (toAddress != row.ToAddress ||
                    recipientName != row.RecipientName ||
                    subject != row.Subject ||
                    error != row.Error)
                {
                    row.ToAddress = toAddress;
                    row.RecipientName = recipientName;
                    row.Subject = subject;
                    row.Error = error;
                    changed++;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            lastId = rows[^1].Id;
        }

        return changed;
    }

    private static async Task<int> BackfillActivityLogsAsync(
        DatabaseContext context, int batchSize, CancellationToken cancellationToken)
    {
        var changed = 0;
        var lastId = 0;
        batchSize = Math.Clamp(batchSize, 1, 5000);

        while (true)
        {
            var rows = await context.ActivityLogs
                .Where(log => log.Id > lastId)
                .OrderBy(log => log.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                break;
            }

            foreach (var row in rows)
            {
                var action = PiiRedactor.RedactPath(row.Action);
                var employeeEmail = PiiRedactor.MaskEmail(row.EmployeeEmail);
                var details = PiiRedactor.RedactIpAddresses(row.Details);

                if (action != row.Action ||
                    employeeEmail != row.EmployeeEmail ||
                    details != row.Details)
                {
                    row.Action = action;
                    row.EmployeeEmail = employeeEmail;
                    row.Details = details;
                    changed++;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            lastId = rows[^1].Id;
        }

        return changed;
    }
}
