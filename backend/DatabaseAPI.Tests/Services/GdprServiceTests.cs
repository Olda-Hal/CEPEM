using System.Text.Json;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>GDPR data map, export and anonymization.</summary>
public class GdprServiceTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly GdprService _service;
    private readonly string _documentDirectory;
    private readonly string _photoDirectory;

    public GdprServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);

        _documentDirectory = Path.Combine(Path.GetTempPath(), $"cepem-gdpr-docs-{Guid.NewGuid():N}");
        _photoDirectory = Path.Combine(Path.GetTempPath(), $"cepem-gdpr-photos-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentEncryption:Key"] = "gdpr-document-test-key-32chars!!",
                ["DocumentStorage:Path"] = _documentDirectory,
                ["PhotoEncryption:Key"] = "gdpr-photo-test-key-32chars!!!",
                ["PhotoStorage:Path"] = _photoDirectory
            })
            .Build();

        var documentService = new PatientDocumentService(
            _context,
            new DocumentEncryptionService(configuration, NullLogger<DocumentEncryptionService>.Instance),
            new TdbAnalysisService(),
            configuration,
            NullLogger<PatientDocumentService>.Instance);
        var photoService = new PatientPhotoService(
            _context,
            new PhotoEncryptionService(configuration, NullLogger<PhotoEncryptionService>.Instance),
            configuration,
            NullLogger<PatientPhotoService>.Instance);

        _service = new GdprService(
            _context, documentService, photoService, NullLogger<GdprService>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        if (Directory.Exists(_documentDirectory))
        {
            Directory.Delete(_documentDirectory, recursive: true);
        }
        if (Directory.Exists(_photoDirectory))
        {
            Directory.Delete(_photoDirectory, recursive: true);
        }
    }

    private async Task<Patient> SeedPatientAsync(bool asEmployee = false)
    {
        var person = new Person
        {
            FirstName = "Eva",
            LastName = "Novakova",
            UID = "00000042",
            Gender = "F",
            CountryCode = "CZ",
            Active = true,
            Address = new Address
            {
                Street = "Hlavni 1",
                City = "Praha",
                PostalCode = "11000",
                Country = "CZ"
            },
            Comment = new Comment { Text = "Person comment" }
        };
        _context.Persons.Add(person);
        await _context.SaveChangesAsync();

        var contact = new Contact();
        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();
        _context.ContactToObjects.Add(new ContactToObject
        {
            ContactId = contact.Id,
            ObjectId = person.Id,
            ObjectType = ContactObjectType.Person,
            PersonId = person.Id
        });
        _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = "eva@example.com" });
        _context.ContactPhoneNumbers.Add(new ContactPhoneNumber { ContactId = contact.Id, PhoneNumber = "777123456" });

        var patient = new Patient
        {
            PersonId = person.Id,
            BirthDate = new DateTime(1990, 5, 1),
            InsuranceNumber = 123,
            CountryCode = "CZ",
            Alive = true,
            Comment = new Comment { Text = "Patient comment" }
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        if (asEmployee)
        {
            _context.Employees.Add(new Employee
            {
                PersonId = person.Id,
                PasswordHash = "hash",
                Salt = "salt"
            });
        }

        var visit = new Visit
        {
            PatientId = patient.Id,
            VisitDate = new DateTime(2026, 11, 1),
            Note = "Citlivá poznámka"
        };
        _context.Visits.Add(visit);

        var eventType = new EventType { NameTranslation = new Translation { EN = "Visit", CS = "Návštěva" } };
        _context.EventTypes.Add(eventType);
        await _context.SaveChangesAsync();

        var eventEntry = new Event
        {
            PatientId = patient.Id,
            EventTypeId = eventType.Id,
            HappenedAt = DateTime.UtcNow,
            Comment = new Comment { Text = "Event comment" }
        };
        _context.Events.Add(eventEntry);
        await _context.SaveChangesAsync();

        var submission = new FormSubmission
        {
            PatientId = patient.Id,
            EventId = eventEntry.Id,
            SubmittedAtUtc = DateTime.UtcNow,
            Consent = new FormSubmissionConsent
            {
                ConfirmAccuracy = true,
                TermsAccepted = true,
                SignaturePlace = "Praha",
                SignatureVector = "signature-data"
            },
            Lifestyle = new FormSubmissionLifestyle { AdditionalHealthInfo = "Poznámka" }
        };
        _context.FormSubmissions.Add(submission);
        _context.PatientConsents.Add(new PatientConsent
        {
            PatientId = patient.Id,
            ConsentType = "gdpr",
            Version = "1",
            Language = "cs",
            Source = "IntakeForm",
            AcceptedAtUtc = DateTime.UtcNow
        });
        _context.EmailLogs.Add(new EmailLog
        {
            ToAddress = "eva@example.com",
            Subject = "Reminder",
            Kind = EmailLogKinds.IntakeReminder,
            Status = EmailLogStatuses.Sent,
            PersonId = person.Id,
            RecipientName = "Eva Novakova",
            Error = "smtp issue"
        });
        _context.IntakeFormLinks.Add(new IntakeFormLink
        {
            TokenHash = Guid.NewGuid().ToString("N"),
            PersonId = person.Id,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
        });
        _context.PatientDocuments.Add(new PatientDocument
        {
            PatientId = patient.Id,
            FileName = "missing.enc",
            OriginalFileName = "zprava.pdf",
            UploadedAt = DateTime.UtcNow,
            FileSize = 10,
            EncryptedPath = "missing.enc"
        });
        await _context.SaveChangesAsync();

        return patient;
    }

    [Fact]
    public async Task DataMap_CountsAllLinkedRecords()
    {
        var patient = await SeedPatientAsync();

        var map = await _service.GetDataMapAsync(patient.Id);

        Assert.NotNull(map);
        Assert.Equal(1, map!.Visits);
        Assert.Equal(1, map.Documents);
        Assert.Equal(1, map.Events);
        Assert.Equal(1, map.FormSubmissions);
        Assert.Equal(1, map.Consents);
        Assert.Equal(1, map.EmailLogs);
    }

    [Fact]
    public async Task Export_ContainsClinicalAndContactData()
    {
        var patient = await SeedPatientAsync();

        var bundle = await _service.BuildExportBundleAsync(patient.Id);

        Assert.NotNull(bundle);
        var json = JsonSerializer.Serialize(bundle!.Export);
        Assert.Contains("Novakova", json);
        Assert.Contains("Citliv", json);
        Assert.Contains("eva@example.com", json);
        Assert.Contains("zprava.pdf", json);
        Assert.Contains("Event comment", json);
    }

    [Fact]
    public async Task Anonymize_ClearsIdentityContactsAndSensitiveFreeText()
    {
        var patient = await SeedPatientAsync();

        var (ok, error, result) = await _service.AnonymizeAsync(
            patient.Id, confirmation: "00000042", requestedByEmployeeId: 7);

        Assert.True(ok, error);
        Assert.NotNull(result);

        _context.ChangeTracker.Clear();
        var storedPatient = await _context.Patients
            .Include(p => p.Person)
            .Include(p => p.Comment)
            .SingleAsync(p => p.Id == patient.Id);

        Assert.True(storedPatient.IsDeleted);
        Assert.Equal(new DateTime(1900, 1, 1), storedPatient.BirthDate);
        Assert.Equal(0, storedPatient.InsuranceNumber);
        Assert.StartsWith("ANON-", storedPatient.Person.UID);
        Assert.Equal("Anonymizovaný", storedPatient.Person.FirstName);
        Assert.False(storedPatient.Person.Active);
        Assert.Null(storedPatient.Comment?.Text);

        Assert.Empty(await _context.ContactEmails.ToListAsync());
        Assert.Empty(await _context.ContactPhoneNumbers.ToListAsync());
        Assert.Empty(await _context.PatientDocuments.ToListAsync());
        Assert.Null((await _context.Visits.SingleAsync()).Note);
        Assert.Null((await _context.Events.Include(e => e.Comment).SingleAsync()).Comment?.Text);
        Assert.Null((await _context.FormSubmissions.Include(fs => fs.Consent).SingleAsync()).Consent?.SignatureVector);
        Assert.NotNull((await _context.PatientConsents.SingleAsync()).RevokedAtUtc);
        var emailLog = await _context.EmailLogs.SingleAsync();
        Assert.Equal("anonymized@invalid", emailLog.ToAddress);
        Assert.Null(emailLog.RecipientName);
        Assert.Null(emailLog.Error);
        Assert.NotNull((await _context.IntakeFormLinks.SingleAsync()).RevokedAtUtc);
        Assert.Single(await _context.GdprRequests.Where(r => r.RequestType == "Anonymize").ToListAsync());
    }

    [Fact]
    public async Task Anonymize_WithWrongConfirmation_IsRejected()
    {
        var patient = await SeedPatientAsync();

        var (ok, error, _) = await _service.AnonymizeAsync(patient.Id, "wrong", requestedByEmployeeId: 7);

        Assert.False(ok);
        Assert.Contains("Potvrzení", error);
        Assert.False((await _context.Patients.SingleAsync(p => p.Id == patient.Id)).IsDeleted);
    }

    [Fact]
    public async Task Anonymize_ForEmployeePerson_IsRejected()
    {
        var patient = await SeedPatientAsync(asEmployee: true);

        var (ok, error, _) = await _service.AnonymizeAsync(patient.Id, "00000042", 7);

        Assert.False(ok);
        Assert.Contains("Zaměstnance", error);
    }
}
