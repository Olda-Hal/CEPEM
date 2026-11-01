using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// The shared send path records every attempt in the admin e-mail log:
/// Sent, LoggedOnly (no SMTP configured) or Failed.
/// </summary>
public class EmailServiceTests
{
    private readonly Mock<IEmailLogWriter> _logWriter = new();

    private EmailService CreateService(string? smtpHost = null)
    {
        var options = Options.Create(new SmtpOptions { Host = smtpHost });
        return new EmailService(NullLogger<EmailService>.Instance, options, _logWriter.Object);
    }

    [Fact]
    public async Task PasswordReset_WithoutSmtp_IsLoggedAsLoggedOnly()
    {
        var service = CreateService();
        var context = new EmailLogContext(EmailLogKinds.PasswordReset, PersonId: 7, RecipientName: "Eva Test");

        await service.SendPasswordResetAsync("eva@example.com", "https://app/reset?token=secret", context);

        _logWriter.Verify(writer => writer.WriteAsync(
            "eva@example.com",
            It.IsAny<string>(),
            EmailLogStatuses.LoggedOnly,
            "SMTP is not configured",
            It.Is<EmailLogContext>(entry =>
                entry.Kind == EmailLogKinds.PasswordReset &&
                entry.PersonId == 7 &&
                entry.RecipientName == "Eva Test"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IntakeReminder_WithoutSmtp_IsLoggedAsLoggedOnlyWithSlot()
    {
        var service = CreateService();
        var context = new EmailLogContext(
            EmailLogKinds.IntakeReminder,
            PersonId: 9,
            RecipientName: "Jan Test",
            ReservationSlotId: 42);

        await service.SendIntakeFormReminderAsync(
            "jan@example.com",
            "https://app/intake/token",
            "cs",
            new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc),
            context);

        _logWriter.Verify(writer => writer.WriteAsync(
            "jan@example.com",
            It.IsAny<string>(),
            EmailLogStatuses.LoggedOnly,
            "SMTP is not configured",
            It.Is<EmailLogContext>(entry =>
                entry.Kind == EmailLogKinds.IntakeReminder &&
                entry.PersonId == 9 &&
                entry.ReservationSlotId == 42),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
