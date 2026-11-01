using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

/// <summary>
/// Matches a public reservation request to an existing person. The rule is
/// deliberately strict: both the e-mail and the phone number must be on the
/// same person. When the contacts are ambiguous (more than one person) no
/// match is returned so the caller can create a fresh record instead of
/// attaching a booking to the wrong patient.
/// </summary>
public interface IPersonMatchingService
{
    Task<Person?> FindUniquePersonByContactsAsync(string? email, string? phoneNumber, CancellationToken cancellationToken = default);
}

public class PersonMatchingService : IPersonMatchingService
{
    private readonly DatabaseContext _context;

    public PersonMatchingService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<Person?> FindUniquePersonByContactsAsync(string? email, string? phoneNumber, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizePhone(phoneNumber);
        if (normalizedEmail.Length == 0 || normalizedPhone.Length == 0)
        {
            return null;
        }

        var candidateIds = await _context.ContactEmails
            .Where(entry => entry.Email.ToLower() == normalizedEmail)
            .SelectMany(entry => entry.Contact.ContactToObjects)
            .Where(link => link.ObjectType == ContactObjectType.Person && link.PersonId != null)
            .Select(link => link.PersonId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return null;
        }

        var candidates = await _context.Persons
            .Include(person => person.Employee)
            .Include(person => person.Patient)
            .Include(person => person.ContactToObjects)
                .ThenInclude(link => link.Contact)
                    .ThenInclude(contact => contact.PhoneNumbers)
            .Where(person => candidateIds.Contains(person.Id))
            .ToListAsync(cancellationToken);

        var matches = candidates
            .Where(person => person.Employee == null)
            .Where(person => person.Patient == null || !person.Patient.IsDeleted)
            .Where(person => person.ContactToObjects
                .SelectMany(link => link.Contact.PhoneNumbers)
                .Any(phone => NormalizePhone(phone.PhoneNumber) == normalizedPhone))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    internal static string NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();

    /// <summary>Compares phone numbers by digits only (spaces, dashes and + are ignored).</summary>
    internal static string NormalizePhone(string? phoneNumber) =>
        string.IsNullOrWhiteSpace(phoneNumber)
            ? string.Empty
            : new string(phoneNumber.Where(char.IsDigit).ToArray());
}
