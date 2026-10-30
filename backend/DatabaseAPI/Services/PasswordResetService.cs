using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Services;

public interface IPasswordResetService
{
    /// <summary>
    /// If an employee with the given e-mail exists, generate a fresh reset
    /// token, invalidate older ones and send the link. Returns Ok=true when
    /// the flow completed (or there is silently nothing to send) — the
    /// endpoint must stay non-enumerating. Ok=false means the reset link
    /// could not be delivered even though the account exists.
    /// </summary>
    Task<(bool Ok, string? Error)> RequestResetForEmailAsync(string email);

    /// <summary>Supervisor-triggered variant of RequestResetForEmailAsync.</summary>
    Task<(bool Ok, string? Error)> RequestResetForEmployeeAsync(int employeeId);

    /// <summary>Consume the token and set a new password. Fails on unknown/expired/used token.</summary>
    Task<(bool Ok, string? Error)> ResetWithTokenAsync(string token, string newPassword);
}

public class PasswordResetService : IPasswordResetService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    public const int MinPasswordLength = 6;

    private readonly DatabaseContext _context;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        DatabaseContext context,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<PasswordResetService> logger)
    {
        _context = context;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error)> RequestResetForEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return (true, null); // nothing to do, stays non-enumerating
        }

        var employee = await FindEmployeeByEmailAsync(email);
        if (employee == null)
        {
            return (true, null); // unknown e-mail — pretend success
        }

        return await IssueTokenAndSendAsync(employee);
    }

    public async Task<(bool Ok, string? Error)> RequestResetForEmployeeAsync(int employeeId)
    {
        var employee = await _context.Employees
            .Include(e => e.Person)
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee == null)
        {
            return (false, "Employee not found");
        }

        return await IssueTokenAndSendAsync(employee);
    }

    public async Task<(bool Ok, string? Error)> ResetWithTokenAsync(string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "Token is required");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
        {
            return (false, $"Nové heslo musí mít alespoň {MinPasswordLength} znaků");
        }

        var tokenHash = ComputeHash(token);
        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.Employee)
                .ThenInclude(e => e.Person)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (resetToken == null || resetToken.UsedAtUtc != null || resetToken.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return (false, "Resetový odkaz je neplatný nebo již vypršel");
        }

        var employee = resetToken.Employee;
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, salt);
        employee.Salt = salt;
        employee.PasswordExpiration = DateTime.UtcNow.AddDays(90);
        employee.FailedLoginAttempts = 0;
        employee.LockedUntil = null;

        resetToken.UsedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    private async Task<Employee?> FindEmployeeByEmailAsync(string email)
    {
        return await _context.Employees
            .Include(e => e.Person)
                .ThenInclude(p => p.ContactToObjects)
                    .ThenInclude(cto => cto.Contact)
                        .ThenInclude(c => c.Emails)
            .Where(e => e.Person.ContactToObjects.Any(cto =>
                cto.Contact.Emails.Any(em => em.Email == email)))
            .FirstOrDefaultAsync();
    }

    private async Task<string?> GetEmployeeEmailAsync(Employee employee)
    {
        return await _context.ContactToObjects
            .Where(cto => cto.ObjectType == ContactObjectType.Person && cto.ObjectId == employee.PersonId)
            .Join(_context.ContactEmails, cto => cto.ContactId, ce => ce.ContactId, (_, ce) => ce.Email)
            .FirstOrDefaultAsync();
    }

    private async Task<(bool Ok, string? Error)> IssueTokenAndSendAsync(Employee employee)
    {
        var email = await GetEmployeeEmailAsync(employee);
        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "Zaměstnanec nemá nastavenou e-mailovou adresu");
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        // Invalidate any outstanding tokens for this employee.
        var pending = await _context.PasswordResetTokens
            .Where(t => t.EmployeeId == employee.Id && t.UsedAtUtc == null)
            .ToListAsync();
        foreach (var old in pending)
        {
            old.UsedAtUtc = DateTime.UtcNow;
        }

        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            EmployeeId = employee.Id,
            TokenHash = ComputeHash(token),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow + TokenLifetime
        });
        await _context.SaveChangesAsync();

        var baseUrl = _configuration["App:FrontendBaseUrl"] ?? "http://localhost:3000";
        var resetUrl = $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

        try
        {
            await _emailService.SendPasswordResetAsync(email, resetUrl);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset e-mail for employee {EmployeeId}", employee.Id);
            return (false, "E-mail s odkazem se nepodařilo odeslat");
        }
    }

    private static string ComputeHash(string token)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
