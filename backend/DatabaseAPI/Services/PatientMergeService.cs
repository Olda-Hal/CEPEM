using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public interface IPatientMergeService
{
    Task<PatientMergePreviewDto?> PreviewAsync(
        int sourcePatientId, int targetPatientId, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error, PatientMergeResultDto? Result)> MergeAsync(
        int sourcePatientId,
        int targetPatientId,
        int? actorEmployeeId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Moves all patient-linked records from a duplicate (source) to the record
/// that should be kept (target), then softly deletes the source. The operation
/// is transactional and writes an audit row.
/// </summary>
public class PatientMergeService : IPatientMergeService
{
    private readonly DatabaseContext _context;
    private readonly ILogger<PatientMergeService> _logger;

    public PatientMergeService(DatabaseContext context, ILogger<PatientMergeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PatientMergePreviewDto?> PreviewAsync(
        int sourcePatientId, int targetPatientId, CancellationToken cancellationToken = default)
    {
        if (sourcePatientId == targetPatientId)
        {
            return null;
        }

        var source = await _context.Patients
            .Include(patient => patient.Person)
            .FirstOrDefaultAsync(patient => patient.Id == sourcePatientId, cancellationToken);
        var target = await _context.Patients
            .Include(patient => patient.Person)
            .FirstOrDefaultAsync(patient => patient.Id == targetPatientId, cancellationToken);

        if (source == null || target == null)
        {
            return null;
        }

        return new PatientMergePreviewDto
        {
            SourcePatientId = source.Id,
            TargetPatientId = target.Id,
            SourceName = $"{source.Person.FirstName} {source.Person.LastName}".Trim(),
            TargetName = $"{target.Person.FirstName} {target.Person.LastName}".Trim(),
            SourceUid = source.Person.UID,
            TargetUid = target.Person.UID,
            Events = await _context.Events.CountAsync(e => e.PatientId == source.Id, cancellationToken),
            Visits = await _context.Visits.CountAsync(v => v.PatientId == source.Id, cancellationToken),
            Documents = await _context.PatientDocuments.CountAsync(d => d.PatientId == source.Id, cancellationToken),
            FormSubmissions = await _context.FormSubmissions.CountAsync(f => f.PatientId == source.Id, cancellationToken),
            Consents = await _context.PatientConsents.CountAsync(c => c.PatientId == source.Id, cancellationToken),
            EvaluationCases = await _context.EvaluationCases.CountAsync(e => e.PatientId == source.Id, cancellationToken),
            IntakeChanges = await _context.IntakeChangeRequests.CountAsync(c => c.PatientId == source.Id, cancellationToken),
            GdprRequests = await _context.GdprRequests.CountAsync(r => r.PatientId == source.Id, cancellationToken),
            ReservationSlots = await _context.ReservationSlots.CountAsync(s => s.PersonId == source.PersonId, cancellationToken),
            Reservations = await _context.Reservations.CountAsync(r => r.PersonId == source.PersonId, cancellationToken),
            Appointments = await _context.Appointments.CountAsync(a => a.PersonId == source.PersonId, cancellationToken),
            EmailLogs = await _context.EmailLogs.CountAsync(l => l.PersonId == source.PersonId, cancellationToken),
            SmsLogs = await _context.SmsLogs.CountAsync(l => l.PersonId == source.PersonId, cancellationToken),
            IntakeLinks = await _context.IntakeFormLinks.CountAsync(l => l.PersonId == source.PersonId, cancellationToken)
        };
    }

    public async Task<(bool Ok, string? Error, PatientMergeResultDto? Result)> MergeAsync(
        int sourcePatientId,
        int targetPatientId,
        int? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        if (sourcePatientId == targetPatientId)
        {
            return (false, "Source and target patient must differ", null);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var source = await _context.Patients
                .Include(patient => patient.Person)
                    .ThenInclude(person => person.Address)
                .Include(patient => patient.Person)
                    .ThenInclude(person => person.ContactToObjects)
                        .ThenInclude(link => link.Contact)
                            .ThenInclude(contact => contact.Emails)
                .Include(patient => patient.Person)
                    .ThenInclude(person => person.ContactToObjects)
                        .ThenInclude(link => link.Contact)
                            .ThenInclude(contact => contact.PhoneNumbers)
                .FirstOrDefaultAsync(patient => patient.Id == sourcePatientId, cancellationToken);
            var target = await _context.Patients
                .Include(patient => patient.Person)
                    .ThenInclude(person => person.Address)
                .Include(patient => patient.Person)
                    .ThenInclude(person => person.ContactToObjects)
                        .ThenInclude(link => link.Contact)
                            .ThenInclude(contact => contact.Emails)
                .Include(patient => patient.Person)
                    .ThenInclude(person => person.ContactToObjects)
                        .ThenInclude(link => link.Contact)
                            .ThenInclude(contact => contact.PhoneNumbers)
                .FirstOrDefaultAsync(patient => patient.Id == targetPatientId, cancellationToken);

            if (source == null || target == null)
            {
                return (false, "Patient not found", null);
            }

            var movedEvents = await MovePatientRecordsAsync(source.Id, target.Id, cancellationToken);
            var movedPersonReferences = await MovePersonReferencesAsync(
                source.PersonId, target.PersonId, cancellationToken);

            MergeContactData(source.Person, target.Person);

            if (target.PhotoPath == null && source.PhotoPath != null)
            {
                target.PhotoPath = source.PhotoPath;
                source.PhotoPath = null;
            }
            if (target.InsuranceNumber <= 0 && source.InsuranceNumber > 0)
            {
                target.InsuranceNumber = source.InsuranceNumber;
            }

            source.IsDeleted = true;
            source.DeletedAtUtc = DateTime.UtcNow;
            source.DeletedBy = $"Merge into patient {target.Id}";

            // Do not deactivate a person who is also an employee record.
            var sourceIsEmployee = await _context.Employees
                .AnyAsync(employee => employee.PersonId == source.PersonId, cancellationToken);
            if (!sourceIsEmployee)
            {
                source.Person.Active = false;
            }

            _context.ActivityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                Service = "DatabaseAPI",
                Action = "/api/patients/merge",
                Details =
                    $"Merged patient {source.Id} ({source.Person.UID}) into {target.Id} ({target.Person.UID}); " +
                    $"events={movedEvents.Events}, visits={movedEvents.Visits}, " +
                    $"documents={movedEvents.Documents}, submissions={movedEvents.FormSubmissions}, " +
                    $"personRefs={movedPersonReferences}",
                EmployeeId = actorEmployeeId,
                Method = "POST",
                StatusCode = 200
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Merged patient {SourceId} into {TargetId} (actor {ActorId})",
                source.Id, target.Id, actorEmployeeId);

            return (true, null, new PatientMergeResultDto
            {
                SourcePatientId = source.Id,
                TargetPatientId = target.Id,
                TargetName = $"{target.Person.FirstName} {target.Person.LastName}".Trim(),
                TargetUid = target.Person.UID,
                MovedEvents = movedEvents.Events,
                MovedVisits = movedEvents.Visits,
                MovedDocuments = movedEvents.Documents,
                MovedFormSubmissions = movedEvents.FormSubmissions,
                MovedConsents = movedEvents.Consents,
                MovedEvaluationCases = movedEvents.EvaluationCases,
                MovedIntakeChanges = movedEvents.IntakeChanges,
                MovedPersonReferences = movedPersonReferences
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Could not merge patient {SourceId} into {TargetId}",
                sourcePatientId, targetPatientId);
            return (false, "Merge failed", null);
        }
    }

    private async Task<(int Events, int Visits, int Documents, int FormSubmissions, int Consents,
        int EvaluationCases, int IntakeChanges)> MovePatientRecordsAsync(
        int sourceId, int targetId, CancellationToken cancellationToken)
    {
        var events = await _context.Events.Where(e => e.PatientId == sourceId).ToListAsync(cancellationToken);
        var visits = await _context.Visits.Where(v => v.PatientId == sourceId).ToListAsync(cancellationToken);
        var documents = await _context.PatientDocuments.Where(d => d.PatientId == sourceId).ToListAsync(cancellationToken);
        var submissions = await _context.FormSubmissions.Where(f => f.PatientId == sourceId).ToListAsync(cancellationToken);
        var consents = await _context.PatientConsents.Where(c => c.PatientId == sourceId).ToListAsync(cancellationToken);
        var evaluations = await _context.EvaluationCases.Where(e => e.PatientId == sourceId).ToListAsync(cancellationToken);
        var changes = await _context.IntakeChangeRequests.Where(c => c.PatientId == sourceId).ToListAsync(cancellationToken);
        var gdprRequests = await _context.GdprRequests.Where(r => r.PatientId == sourceId).ToListAsync(cancellationToken);

        foreach (var item in events) item.PatientId = targetId;
        foreach (var item in visits) item.PatientId = targetId;
        foreach (var item in documents) item.PatientId = targetId;
        foreach (var item in submissions) item.PatientId = targetId;
        foreach (var item in consents) item.PatientId = targetId;
        foreach (var item in evaluations) item.PatientId = targetId;
        foreach (var item in changes) item.PatientId = targetId;
        foreach (var item in gdprRequests) item.PatientId = targetId;

        return (events.Count, visits.Count, documents.Count, submissions.Count,
            consents.Count, evaluations.Count, changes.Count);
    }

    private async Task<int> MovePersonReferencesAsync(
        int sourcePersonId, int targetPersonId, CancellationToken cancellationToken)
    {
        if (sourcePersonId == targetPersonId)
        {
            return 0;
        }

        var reservationSlots = await _context.ReservationSlots
            .Where(slot => slot.PersonId == sourcePersonId).ToListAsync(cancellationToken);
        var reservations = await _context.Reservations
            .Where(reservation => reservation.PersonId == sourcePersonId).ToListAsync(cancellationToken);
        var appointments = await _context.Appointments
            .Where(appointment => appointment.PersonId == sourcePersonId).ToListAsync(cancellationToken);
        var emailLogs = await _context.EmailLogs
            .Where(log => log.PersonId == sourcePersonId).ToListAsync(cancellationToken);
        var smsLogs = await _context.SmsLogs
            .Where(log => log.PersonId == sourcePersonId).ToListAsync(cancellationToken);
        var intakeLinks = await _context.IntakeFormLinks
            .Where(link => link.PersonId == sourcePersonId).ToListAsync(cancellationToken);

        foreach (var item in reservationSlots) item.PersonId = targetPersonId;
        foreach (var item in reservations) item.PersonId = targetPersonId;
        foreach (var item in appointments) item.PersonId = targetPersonId;
        foreach (var item in emailLogs) item.PersonId = targetPersonId;
        foreach (var item in smsLogs) item.PersonId = targetPersonId;
        foreach (var item in intakeLinks) item.PersonId = targetPersonId;

        return reservationSlots.Count + reservations.Count + appointments.Count +
               emailLogs.Count + smsLogs.Count + intakeLinks.Count;
    }

    private static void MergeContactData(Person source, Person target)
    {
        var sourceContact = source.ContactToObjects.FirstOrDefault()?.Contact;
        var targetContact = target.ContactToObjects.FirstOrDefault()?.Contact;
        if (sourceContact == null)
        {
            return;
        }

        if (targetContact == null)
        {
            // Copy contact values into a new Contact; the source person keeps
            // its own contact row for audit/history.
            var copied = new Contact();
            foreach (var email in sourceContact.Emails)
            {
                copied.Emails.Add(new ContactEmail { Email = email.Email });
            }
            foreach (var phone in sourceContact.PhoneNumbers)
            {
                copied.PhoneNumbers.Add(new ContactPhoneNumber { PhoneNumber = phone.PhoneNumber });
            }

            target.ContactToObjects.Add(new ContactToObject
            {
                Contact = copied,
                ObjectId = target.Id,
                ObjectType = ContactObjectType.Person,
                PersonId = target.Id
            });
        }
        else
        {
            var sourceEmail = sourceContact.Emails.FirstOrDefault()?.Email;
            if (string.IsNullOrWhiteSpace(targetContact.Emails.FirstOrDefault()?.Email) &&
                !string.IsNullOrWhiteSpace(sourceEmail))
            {
                targetContact.Emails.Add(new ContactEmail
                {
                    ContactId = targetContact.Id,
                    Email = sourceEmail
                });
            }

            var sourcePhone = sourceContact.PhoneNumbers.FirstOrDefault()?.PhoneNumber;
            if (string.IsNullOrWhiteSpace(targetContact.PhoneNumbers.FirstOrDefault()?.PhoneNumber) &&
                !string.IsNullOrWhiteSpace(sourcePhone))
            {
                targetContact.PhoneNumbers.Add(new ContactPhoneNumber
                {
                    ContactId = targetContact.Id,
                    PhoneNumber = sourcePhone
                });
            }
        }

        if (target.Address == null && source.Address != null)
        {
            target.Address = new Address
            {
                Street = source.Address.Street,
                City = source.Address.City,
                PostalCode = source.Address.PostalCode,
                Country = source.Address.Country
            };
        }
    }
}
