using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

/// <summary>
/// Configuration for the "fill in your intake form" reminder. The form is
/// sent a few days before a confirmed reservation so the patient can complete
/// it outside the visit.
/// </summary>
public class IntakeReminderOptions
{
    public const string SectionName = "IntakeReminder";

    public bool Enabled { get; set; } = true;

    /// <summary>How many days before the visit the link is e-mailed. Default: 3.</summary>
    public int LeadDays { get; set; } = 3;

    /// <summary>How often the scanner runs. Default: every 15 minutes.</summary>
    public int CheckIntervalMinutes { get; set; } = 15;

    /// <summary>Public SPA URL used in the e-mail (App:FrontendBaseUrl).</summary>
    public string? FrontendBaseUrl { get; set; }
}

/// <summary>
/// Periodic scanner for confirmed reservations that are about to happen and
/// still have no submitted intake form. Sending is idempotent through
/// <see cref="ReservationSlot.IntakeReminderSentAtUtc"/>.
/// </summary>
public class IntakeReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailService _emailService;
    private readonly ILogger<IntakeReminderService> _logger;

    public IntakeReminderService(
        IServiceScopeFactory scopeFactory,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<IntakeReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _emailService = emailService;
        _logger = logger;
        Options = configuration.GetSection(IntakeReminderOptions.SectionName)
            .Get<IntakeReminderOptions>() ?? new IntakeReminderOptions();
        Options.FrontendBaseUrl ??= configuration["App:FrontendBaseUrl"];
    }

    public IntakeReminderOptions Options { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Options.Enabled)
        {
            _logger.LogInformation("Intake form reminders are disabled");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, Options.CheckIntervalMinutes));

        // Give the host/migrations a moment before the first scan.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var linkService = scope.ServiceProvider.GetRequiredService<IIntakeFormLinkService>();
                await RunOnceAsync(context, linkService, Options.LeadDays, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Intake form reminder scan failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    /// <summary>Single scan, separated for testability.</summary>
    public async Task<int> RunOnceAsync(DatabaseContext context, IIntakeFormLinkService linkService, int leadDays, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var due = await IntakeReminderPlanner.FindDueSlotsAsync(context, now, leadDays, cancellationToken);
        var sent = 0;

        foreach (var slot in due)
        {
            if (!slot.PersonId.HasValue)
            {
                continue;
            }

            var email = slot.Person?.ContactToObjects
                .SelectMany(link => link.Contact.Emails)
                .Select(entry => entry.Email)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogDebug("Skipping intake reminder for slot {SlotId}: no e-mail on file", slot.Id);
                continue;
            }

            // Claim the reminder first so a concurrent scan cannot send twice.
            slot.IntakeReminderSentAtUtc = now;
            await context.SaveChangesAsync(cancellationToken);

            try
            {
                var created = await linkService.CreateLinkForReservationSlotAsync(
                    slot.PersonId.Value, slot.Id, slot.StartDateTime);
                if (created == null)
                {
                    throw new InvalidOperationException($"Could not create an intake link for slot {slot.Id}");
                }

                var baseUrl = (Options.FrontendBaseUrl ?? "http://localhost:3000").TrimEnd('/');
                var intakeUrl = $"{baseUrl}/intake/{created.Value.RawToken}";

                var recipientName = slot.Person is null
                    ? null
                    : $"{slot.Person.FirstName} {slot.Person.LastName}".Trim();

                await _emailService.SendIntakeFormReminderAsync(
                    email,
                    intakeUrl,
                    MapLanguage(slot.CountryCode),
                    slot.StartDateTime,
                    new EmailLogContext(
                        EmailLogKinds.IntakeReminder,
                        slot.PersonId,
                        recipientName,
                        slot.Id));

                sent++;
            }
            catch (Exception ex)
            {
                // Release the claim so the next scan retries.
                slot.IntakeReminderSentAtUtc = null;
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogError(ex, "Could not send intake reminder for slot {SlotId}", slot.Id);
            }
        }

        return sent;
    }

    private static string MapLanguage(string? countryCode) =>
        countryCode?.Trim().ToUpperInvariant() switch
        {
            "NL" => "nl",
            "CZ" or null or "" => "cs",
            _ => "en"
        };
}

/// <summary>Pure query so the reminder selection can be unit tested.</summary>
public static class IntakeReminderPlanner
{
    public static Task<List<ReservationSlot>> FindDueSlotsAsync(
        DatabaseContext context, DateTime nowUtc, int leadDays, CancellationToken cancellationToken = default)
    {
        var windowEnd = nowUtc.AddDays(Math.Max(0, leadDays));

        return context.ReservationSlots
            .Include(slot => slot.Person!)
                .ThenInclude(person => person.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.Emails)
            .Where(slot => slot.Status == SlotStatus.Reserved)
            .Where(slot => slot.PersonId != null)
            .Where(slot => slot.IntakeReminderSentAtUtc == null)
            .Where(slot => slot.StartDateTime > nowUtc && slot.StartDateTime <= windowEnd)
            // Already submitted (by any link) for the visit created from this slot.
            .Where(slot => !context.Visits.Any(visit =>
                visit.ReservationSlotId == slot.Id && visit.SourceEventId != null))
            .OrderBy(slot => slot.StartDateTime)
            .ToListAsync(cancellationToken);
    }
}
