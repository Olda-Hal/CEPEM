using Cepem.Shared;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Services;

/// <summary>
/// Extra metadata attached to one outgoing e-mail (used for the admin log).
/// </summary>
public sealed record EmailLogContext(
    string Kind,
    int? PersonId = null,
    string? RecipientName = null,
    int? ReservationSlotId = null);

public interface IEmailLogWriter
{
    Task WriteAsync(
        string toAddress,
        string subject,
        string status,
        string? error,
        EmailLogContext? context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes e-mail log rows through their own DI scope so the singleton e-mail
/// service never captures a scoped DbContext. Logging failures must never
/// break the actual e-mail flow.
/// </summary>
public class EmailLogWriter : IEmailLogWriter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailLogWriter> _logger;

    public EmailLogWriter(IServiceScopeFactory scopeFactory, ILogger<EmailLogWriter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task WriteAsync(
        string toAddress,
        string subject,
        string status,
        string? error,
        EmailLogContext? context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var contextDb = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            contextDb.EmailLogs.Add(new EmailLog
            {
                CreatedAtUtc = DateTime.UtcNow,
                ToAddress = PiiRedactor.MaskEmail(Truncate(toAddress, 320)) ?? string.Empty,
                Subject = PiiRedactor.MaskEmails(Truncate(subject, 200)) ?? string.Empty,
                Kind = context?.Kind ?? "Other",
                Status = status,
                Error = PiiRedactor.MaskEmails(Truncate(error, 1000)),
                PersonId = context?.PersonId,
                RecipientName = PiiRedactor.MaskName(Truncate(context?.RecipientName, 200)),
                ReservationSlotId = context?.ReservationSlotId
            });

            await contextDb.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not write e-mail log for {Email}",
                PiiRedactor.MaskEmail(toAddress));
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
