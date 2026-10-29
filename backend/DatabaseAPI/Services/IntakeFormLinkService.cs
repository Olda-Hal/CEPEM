using System.Security.Cryptography;
using System.Text;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

/// <summary>
/// Handles the public intake-form magic links: creation (hash-only token
/// storage) and lookup. The raw one-time token leaves this service only via
/// the creation result; it is never stored.
/// </summary>
public interface IIntakeFormLinkService
{
    /// <summary>
    /// Creates a link for an existing person (optionally bound to one of
    /// their reservations). Returns null when the person or the reservation
    /// doesn't exist; throws InvalidOperationException when the reservation
    /// belongs to a different person.
    /// </summary>
    Task<(IntakeFormLink Link, string RawToken)?> CreateLinkAsync(int personId, int? reservationId, int expiresInHours);

    /// <summary>Finds the link by raw token (validity checks are separate).</summary>
    Task<IntakeFormLink?> FindLinkAsync(string token);

    /// <summary>Returns why a link is unusable, or null when it can be used.</summary>
    string? GetInvalidityReason(IntakeFormLink link);

    /// <summary>Marks the link as used (single-use semantics).</summary>
    Task MarkUsedAsync(IntakeFormLink link);
}

public class IntakeFormLinkService : IIntakeFormLinkService
{
    private const int DefaultExpirationHours = 72;

    private readonly DatabaseContext _context;

    public IntakeFormLinkService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<(IntakeFormLink Link, string RawToken)?> CreateLinkAsync(int personId, int? reservationId, int expiresInHours)
    {
        var person = await _context.Persons.FirstOrDefaultAsync(p => p.Id == personId);
        if (person == null)
            return null;

        if (reservationId.HasValue)
        {
            var reservation = await _context.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId.Value);
            if (reservation == null)
                return null;
            if (reservation.PersonId != personId)
                throw new InvalidOperationException("Reservation does not belong to this person");
        }

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        var link = new IntakeFormLink
        {
            TokenHash = ComputeTokenHash(rawToken),
            PersonId = person.Id,
            ReservationId = reservationId,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(expiresInHours > 0 ? expiresInHours : DefaultExpirationHours)
        };

        _context.IntakeFormLinks.Add(link);
        await _context.SaveChangesAsync();

        return (link, rawToken);
    }

    public async Task<IntakeFormLink?> FindLinkAsync(string token)
    {
        return await _context.IntakeFormLinks
            .Include(l => l.Person)
            .FirstOrDefaultAsync(l => l.TokenHash == ComputeTokenHash(token));
    }

    public string? GetInvalidityReason(IntakeFormLink link)
    {
        if (link.RevokedAtUtc.HasValue) return "Intake link has been revoked";
        if (link.UsedAtUtc.HasValue) return "Intake link has already been used";
        if (link.ExpiresAtUtc < DateTime.UtcNow) return "Intake link has expired";
        return null;
    }

    public async Task MarkUsedAsync(IntakeFormLink link)
    {
        link.UsedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    private static string ComputeTokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
