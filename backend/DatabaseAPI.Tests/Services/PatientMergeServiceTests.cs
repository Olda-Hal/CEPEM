using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DatabaseAPI.Tests.Services;

public class PatientMergeServiceTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly PatientMergeService _service;

    public PatientMergeServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new DatabaseContext(options);
        _service = new PatientMergeService(
            _context, NullLogger<PatientMergeService>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Patient SeedPatient(string firstName, string uid, string? email = null)
    {
        var person = new Person
        {
            FirstName = firstName,
            LastName = "Test",
            UID = uid,
            Gender = "F",
            CountryCode = "CZ",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Persons.Add(person);
        _context.SaveChanges();

        if (email != null)
        {
            var contact = new Contact();
            _context.Contacts.Add(contact);
            _context.SaveChanges();
            _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = email });
            _context.ContactToObjects.Add(new ContactToObject
            {
                ContactId = contact.Id,
                ObjectId = person.Id,
                ObjectType = ContactObjectType.Person,
                PersonId = person.Id
            });
        }

        var patient = new Patient
        {
            PersonId = person.Id,
            BirthDate = new DateTime(1990, 1, 1),
            InsuranceNumber = 1,
            CountryCode = "CZ"
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();
        return patient;
    }

    [Fact]
    public async Task Merge_MovesPatientRecordsAndSoftDeletesSource()
    {
        var source = SeedPatient("Duplicate", "S1", email: "duplicate@example.com");
        var target = SeedPatient("Kept", "T1");

        var eventType = new EventType { NameTranslation = new Translation { EN = "Visit" } };
        var sourceEvent = new Event
        {
            PatientId = source.Id,
            EventType = eventType,
            HappenedAt = DateTime.UtcNow,
            CountryCode = "CZ"
        };
        _context.Events.Add(sourceEvent);
        await _context.SaveChangesAsync();

        var preview = await _service.PreviewAsync(source.Id, target.Id);
        Assert.NotNull(preview);
        Assert.Equal(1, preview!.Events);
        Assert.Equal("Duplicate Test", preview.SourceName);

        var (ok, error, result) = await _service.MergeAsync(source.Id, target.Id, actorEmployeeId: 7);

        Assert.True(ok, error);
        Assert.Equal(1, result!.MovedEvents);

        var reloadedEvent = await _context.Events.SingleAsync();
        Assert.Equal(target.Id, reloadedEvent.PatientId);

        var sourceAfter = await _context.Patients.Include(p => p.Person)
            .SingleAsync(p => p.Id == source.Id);
        Assert.True(sourceAfter.IsDeleted);
        Assert.False(sourceAfter.Person.Active);

        var targetContactEmail = await _context.ContactToObjects
            .Where(link => link.ObjectType == ContactObjectType.Person && link.PersonId == target.PersonId)
            .Join(_context.ContactEmails, link => link.ContactId, email => email.ContactId,
                (_, email) => email.Email)
            .FirstOrDefaultAsync();
        Assert.Equal("duplicate@example.com", targetContactEmail);

        var audit = await _context.ActivityLogs.SingleAsync();
        Assert.Equal("/api/patients/merge", audit.Action);
        Assert.Equal(7, audit.EmployeeId);
    }

    [Fact]
    public async Task Merge_SamePatient_IsRejected()
    {
        var patient = SeedPatient("Same", "S1");

        var (ok, error, _) = await _service.MergeAsync(patient.Id, patient.Id, null);

        Assert.False(ok);
        Assert.NotNull(error);
    }
}
