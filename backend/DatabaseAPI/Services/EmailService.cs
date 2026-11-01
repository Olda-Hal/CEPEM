using System.Net;
using System.Net.Mail;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Services;

public class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@cepem.local";
    public string FromName { get; set; } = "CEPEM";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}

public interface IEmailService
{
    /// <summary>
    /// Send the password reset link. Every attempt is recorded in the admin
    /// e-mail log; without SMTP the message only goes to the container log.
    /// A transport failure throws after being recorded — callers decide how
    /// to surface that.
    /// </summary>
    Task SendPasswordResetAsync(string toEmail, string resetUrl, EmailLogContext? context = null);

    /// <summary>
    /// Send the pre-visit intake form link in the patient's language
    /// (cs / nl / en).
    /// </summary>
    Task SendIntakeFormReminderAsync(string toEmail, string intakeUrl, string language, DateTime visitStart, EmailLogContext? context = null);

    /// <summary>Send admin-editable template output (reservation confirmations, etc.).</summary>
    Task SendTemplatedAsync(string toEmail, string subject, string body, string kind, EmailLogContext? context = null);

    bool IsConfigured { get; }
}

public class EmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly IEmailLogWriter _emailLogWriter;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        ILogger<EmailService> logger,
        Microsoft.Extensions.Options.IOptions<SmtpOptions> options,
        IEmailLogWriter emailLogWriter)
    {
        _logger = logger;
        _options = options.Value;
        _emailLogWriter = emailLogWriter;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendPasswordResetAsync(string toEmail, string resetUrl, EmailLogContext? context = null)
    {
        var subject = "CEPEM – obnovení hesla / password reset";
        var body =
            $"Dobrý den,\n\n" +
            $"byla podána žádost o obnovení hesla k Vašemu účtu v systému CEPEM. " +
            $"K pokračování otevřete následující odkaz (platí 15 minut):\n\n" +
            $"{resetUrl}\n\n" +
            $"Pokud jste o změnu hesla nepožádali, tento e-mail ignorujte.\n\n" +
            $"Hello,\n\n" +
            $"A password reset was requested for your CEPEM account. " +
            $"Open the link below to continue (valid for 15 minutes):\n\n" +
            $"{resetUrl}\n\n" +
            $"If you did not request this, you can ignore this e-mail.";

        await SendAsync(
            toEmail,
            subject,
            body,
            WithKind(context, EmailLogKinds.PasswordReset));
    }

    public async Task SendIntakeFormReminderAsync(
        string toEmail,
        string intakeUrl,
        string language,
        DateTime visitStart,
        EmailLogContext? context = null)
    {
        var (subject, body) = BuildIntakeReminder(language, intakeUrl, visitStart);

        await SendAsync(
            toEmail,
            subject,
            body,
            WithKind(context, EmailLogKinds.IntakeReminder));
    }

    public Task SendTemplatedAsync(
        string toEmail, string subject, string body, string kind, EmailLogContext? context = null)
    {
        return SendAsync(toEmail, subject, body, WithKind(context, kind));
    }

    /// <summary>
    /// Shared SMTP send path. Every attempt is recorded in the admin e-mail
    /// log, including attempts that only fall back to the container log.
    /// </summary>
    private async Task SendAsync(string toEmail, string subject, string body, EmailLogContext context)
    {
        if (!IsConfigured)
        {
            // Development/CI convenience: with no SMTP configured the message
            // (including its link) still lands in the container log.
            _logger.LogWarning(
                "SMTP is not configured – e-mail to {Email} ({Subject}):\n{Body}",
                toEmail, subject, body);

            await _emailLogWriter.WriteAsync(
                toEmail, subject, EmailLogStatuses.LoggedOnly,
                "SMTP is not configured", context);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        try
        {
            using var client = new SmtpClient(_options.Host!, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
            }

            await client.SendMailAsync(message);
            _logger.LogInformation("E-mail sent to {Email} ({Subject})", toEmail, subject);

            await _emailLogWriter.WriteAsync(
                toEmail, subject, EmailLogStatuses.Sent, null, context);
        }
        catch (Exception ex)
        {
            await _emailLogWriter.WriteAsync(
                toEmail, subject, EmailLogStatuses.Failed, ex.Message, context);
            throw;
        }
    }

    private static EmailLogContext WithKind(EmailLogContext? context, string kind) =>
        context is null ? new EmailLogContext(kind) : context with { Kind = kind };

    private static (string Subject, string Body) BuildIntakeReminder(string language, string intakeUrl, DateTime visitStart)
    {
        var dateText = visitStart.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        return language switch
        {
            "nl" => (
                "CEPEM – vul uw intakeformulier in",
                $"Goedendag,\n\n" +
                $"uw bezoek is gepland op {dateText}. Vul vóór het bezoek het intakeformulier in via onderstaande link:\n\n" +
                $"{intakeUrl}\n\n" +
                $"Bekende gegevens zijn al ingevuld; u hoeft alleen wijzigingen aan te passen. " +
                $"De link blijft geldig tot het begin van het bezoek.\n\n" +
                $"Heeft u dit formulier niet aangevraagd? Negeer deze e-mail dan."),
            "en" => (
                "CEPEM – please complete your intake form",
                $"Hello,\n\n" +
                $"your visit is scheduled for {dateText}. Please complete the intake form before the visit:\n\n" +
                $"{intakeUrl}\n\n" +
                $"Known details are pre-filled; only update what has changed. " +
                $"The link remains valid until the visit starts.\n\n" +
                $"If you did not request this form, you can ignore this e-mail."),
            _ => (
                "CEPEM – vyplňte prosím vstupní formulář",
                $"Dobrý den,\n\n" +
                $"Vaše návštěva je naplánována na {dateText}. Před návštěvou prosím vyplňte vstupní formulář:\n\n" +
                $"{intakeUrl}\n\n" +
                $"Známé údaje jsou předvyplněné, upravte jen to, co se změnilo. " +
                $"Odkaz zůstává platný do začátku návštěvy.\n\n" +
                $"Pokud jste o formulář nežádali, tento e-mail ignorujte.")
        };
    }
}
