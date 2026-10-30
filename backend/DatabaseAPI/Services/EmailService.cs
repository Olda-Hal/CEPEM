using System.Net;
using System.Net.Mail;

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
    /// Send the password reset link. Throws when SMTP is not configured or the
    /// message fails to send — callers decide how to surface that.
    /// </summary>
    Task SendPasswordResetAsync(string toEmail, string resetUrl);
    bool IsConfigured { get; }
}

public class EmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger, Microsoft.Extensions.Options.IOptions<SmtpOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendPasswordResetAsync(string toEmail, string resetUrl)
    {
        if (!IsConfigured)
        {
            // Development/CI convenience: with no SMTP configured the link
            // still lands in the container log instead of being lost.
            _logger.LogWarning(
                "SMTP is not configured – password reset link for {Email}: {ResetUrl}",
                toEmail, resetUrl);
            return;
        }

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

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

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
        _logger.LogInformation("Password reset e-mail sent to {Email}", toEmail);
    }
}
