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

    /// <summary>
    /// Creates a per-visit link bound to a confirmed reservation slot. The
    /// form stays editable until <paramref name="expiresAtUtc"/> (visit start).
    /// </summary>
    Task<(IntakeFormLink Link, string RawToken)?> CreateLinkForReservationSlotAsync(int personId, int reservationSlotId, DateTime expiresAtUtc);

    /// <summary>
    /// Creates a per-visit link for the doctor's tablet, bound to an explicit
    /// visit (and optionally the originating reservation slot).
    /// </summary>
    Task<(IntakeFormLink Link, string RawToken)?> CreateLinkForVisitAsync(int personId, int visitId, int? reservationSlotId, DateTime expiresAtUtc);

    /// <summary>Finds the link by raw token (validity checks are separate).</summary>
    Task<IntakeFormLink?> FindLinkAsync(string token);

    /// <summary>Returns why a link is unusable, or null when it can be used.</summary>
    string? GetInvalidityReason(IntakeFormLink link);

    /// <summary>Marks the link as submitted; per-visit links can be submitted again.</summary>
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

    public async Task<(IntakeFormLink Link, string RawToken)?> CreateLinkForReservationSlotAsync(int personId, int reservationSlotId, DateTime expiresAtUtc)
    {
        var slot = await _context.ReservationSlots.FirstOrDefaultAsync(s => s.Id == reservationSlotId);
        if (slot == null)
            return null;
        if (slot.PersonId != personId)
            throw new InvalidOperationException("Reservation slot does not belong to this person");

        return await CreateLinkAsync(personId, reservationSlotId: slot.Id, visitId: null, expiresAtUtc);
    }

    public async Task<(IntakeFormLink Link, string RawToken)?> CreateLinkForVisitAsync(int personId, int visitId, int? reservationSlotId, DateTime expiresAtUtc)
    {
        var visit = await _context.Visits
            .Include(v => v.Patient)
            .FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit == null)
            return null;
        if (visit.Patient?.PersonId != personId)
            throw new InvalidOperationException("Visit does not belong to this person");

        if (reservationSlotId.HasValue &&
            !await _context.ReservationSlots.AnyAsync(s => s.Id == reservationSlotId.Value && s.PersonId == personId))
        {
            reservationSlotId = null;
        }

        return await CreateLinkAsync(personId, reservationSlotId, visit.Id, expiresAtUtc);
    }

    public async Task<IntakeFormLink?> FindLinkAsync(string token)
    {
        return await _context.IntakeFormLinks
            .Include(l => l.Person)
                .ThenInclude(p => p.Address)
            .Include(l => l.Person)
                .ThenInclude(p => p.Patient)
            .Include(l => l.Person)
                .ThenInclude(p => p.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.Emails)
            .Include(l => l.Person)
                .ThenInclude(p => p.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.PhoneNumbers)
            .Include(l => l.ReservationSlot)
            .Include(l => l.Visit)
            .FirstOrDefaultAsync(l => l.TokenHash == ComputeTokenHash(token));
    }

    public string? GetInvalidityReason(IntakeFormLink link)
    {
        if (link.RevokedAtUtc.HasValue) return "Intake link has been revoked";
        // Legacy one-off links stay single-use; per-visit links can be edited.
        if (link.UsedAtUtc.HasValue && !link.AllowsEdits) return "Intake link has already been used";
        if (link.ExpiresAtUtc < DateTime.UtcNow) return "Intake link has expired";
        return null;
    }

    public async Task MarkUsedAsync(IntakeFormLink link)
    {
        var now = DateTime.UtcNow;
        link.UsedAtUtc ??= now;
        link.LastSubmittedAtUtc = now;
        await _context.SaveChangesAsync();
    }

    private async Task<(IntakeFormLink Link, string RawToken)?> CreateLinkAsync(int personId, int? reservationSlotId, int? visitId, DateTime expiresAtUtc)
    {
        var personExists = await _context.Persons.AnyAsync(p => p.Id == personId);
        if (!personExists)
            return null;

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        var link = new IntakeFormLink
        {
            TokenHash = ComputeTokenHash(rawToken),
            PersonId = personId,
            ReservationSlotId = reservationSlotId,
            VisitId = visitId,
            ExpiresAtUtc = expiresAtUtc
        };

        _context.IntakeFormLinks.Add(link);
        await _context.SaveChangesAsync();

        return (link, rawToken);
    }

    private static string ComputeTokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
