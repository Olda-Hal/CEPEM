using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public class SmsReminderOptions
{
    public const string SectionName = "SmsReminder";

    public bool Enabled { get; set; } = true;

    /// <summary>Send the SMS this many hours before the appointment. Default: 24.</summary>
    public int LeadHours { get; set; } = 24;

    public int CheckIntervalMinutes { get; set; } = 30;
}

/// <summary>
/// Day-before SMS reminder for confirmed reservations. Uses the editable
/// appointment_reminder SMS template (center &gt; country &gt; global) and is
/// idempotent through ReservationSlot.SmsReminderSentAtUtc.
/// </summary>
public class SmsReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISmsService _smsService;
    private readonly ILogger<SmsReminderService> _logger;

    public SmsReminderService(
        IServiceScopeFactory scopeFactory,
        ISmsService smsService,
        IConfiguration configuration,
        ILogger<SmsReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _smsService = smsService;
        _logger = logger;
        Options = configuration.GetSection(SmsReminderOptions.SectionName)
            .Get<SmsReminderOptions>() ?? new SmsReminderOptions();
    }

    public SmsReminderOptions Options { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Options.Enabled)
        {
            _logger.LogInformation("SMS appointment reminders are disabled");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, Options.CheckIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var templates = scope.ServiceProvider.GetRequiredService<ICommunicationTemplateService>();
                await RunOnceAsync(context, templates, _smsService, Options.LeadHours, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS reminder scan failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(
        DatabaseContext context,
        ICommunicationTemplateService templateService,
        ISmsService smsService,
        int leadHours,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var due = await SmsReminderPlanner.FindDueSlotsAsync(context, now, leadHours, cancellationToken);
        var sent = 0;

        var hospitalIds = due.Select(slot => slot.HospitalId).Distinct().ToList();
        var hospitals = hospitalIds.Count == 0
            ? new Dictionary<int, Hospital>()
            : await context.Hospitals
                .Where(hospital => hospitalIds.Contains(hospital.Id))
                .ToDictionaryAsync(hospital => hospital.Id, cancellationToken);

        foreach (var slot in due)
        {
            hospitals.TryGetValue(slot.HospitalId, out var hospital);
            var phone = slot.Person?.ContactToObjects
                .SelectMany(link => link.Contact.PhoneNumbers)
                .Select(number => number.PhoneNumber)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (string.IsNullOrWhiteSpace(phone))
            {
                continue;
            }

            var language = CommunicationLanguages.FromCountry(slot.CountryCode);
            var template = await templateService.ResolveAsync(
                CommunicationTemplateCodes.AppointmentReminderSms,
                CommunicationChannels.Sms,
                language,
                slot.HospitalId,
                hospital?.CountryScopeId,
                cancellationToken);
            if (template == null)
            {
                _logger.LogDebug("No SMS template for slot {SlotId} ({Language})", slot.Id, language);
                continue;
            }

            var body = templateService.Render(template.Body, new Dictionary<string, string?>
            {
                ["patientName"] = $"{slot.Person?.FirstName} {slot.Person?.LastName}".Trim(),
                ["date"] = slot.StartDateTime.ToString("dd.MM.yyyy"),
                ["time"] = slot.StartDateTime.ToString("HH:mm"),
                ["clinic"] = hospital?.Name,
                ["doctor"] = slot.Doctor?.Person is { } doctor
                    ? $"{doctor.FirstName} {doctor.LastName}".Trim()
                    : string.Empty
            });

            slot.SmsReminderSentAtUtc = now;
            await context.SaveChangesAsync(cancellationToken);

            try
            {
                await smsService.SendAsync(
                    phone,
                    body,
                    CommunicationTemplateCodes.AppointmentReminderSms,
                    new SmsLogContext(
                        slot.PersonId,
                        $"{slot.Person?.FirstName} {slot.Person?.LastName}".Trim(),
                        slot.Id),
                    cancellationToken);
                sent++;
            }
            catch (Exception ex)
            {
                slot.SmsReminderSentAtUtc = null;
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogError(ex, "Could not send SMS reminder for slot {SlotId}", slot.Id);
            }
        }

        return sent;
    }
}

public static class SmsReminderPlanner
{
    public static Task<List<ReservationSlot>> FindDueSlotsAsync(
        DatabaseContext context, DateTime nowUtc, int leadHours, CancellationToken cancellationToken = default)
    {
        var windowEnd = nowUtc.AddHours(Math.Max(1, leadHours));
        return context.ReservationSlots
            .Include(slot => slot.Person!)
                .ThenInclude(person => person.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.PhoneNumbers)
            .Include(slot => slot.Doctor!)
                .ThenInclude(doctor => doctor.Person)
            .Where(slot => slot.Status == SlotStatus.Reserved)
            .Where(slot => slot.PersonId != null)
            .Where(slot => slot.SmsReminderSentAtUtc == null)
            .Where(slot => slot.StartDateTime > nowUtc && slot.StartDateTime <= windowEnd)
            .OrderBy(slot => slot.StartDateTime)
            .ToListAsync(cancellationToken);
    }
}
