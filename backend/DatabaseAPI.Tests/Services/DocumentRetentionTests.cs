using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabaseAPI.Tests.Services;

public class DocumentRetentionTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly string _storagePath;

    public DocumentRetentionTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _storagePath = Path.Combine(Path.GetTempPath(), $"cepem-retention-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_storagePath);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, recursive: true);
        }
    }

    private Patient SeedPatient()
    {
        var person = new Person
        {
            FirstName = "Eva",
            LastName = "Kralova",
            UID = "1",
            Gender = "F",
            CountryCode = "CZ",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Persons.Add(person);
        _context.SaveChanges();

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

    private Examination SeedExamination(Patient patient)
    {
        var type = new ExaminationType { NameTranslation = new Translation { EN = "Blood" } };
        var eventType = new EventType { NameTranslation = new Translation { EN = "Visit" } };
        var eventEntry = new Event
        {
            PatientId = patient.Id,
            EventType = eventType,
            HappenedAt = DateTime.UtcNow,
            CountryCode = "CZ"
        };
        var examination = new Examination
        {
            Event = eventEntry,
            ExaminationType = type,
            CountryCode = "CZ"
        };
        _context.Examinations.Add(examination);
        _context.SaveChanges();
        return examination;
    }

    private string SeedFile(string name)
    {
        var path = Path.Combine(_storagePath, name);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        return name;
    }

    [Fact]
    public async Task RunOnce_PurgesOnlyOldSoftDeletedDocumentsAndTheirFiles()
    {
        var patient = SeedPatient();
        var examination = SeedExamination(patient);

        var oldPatientFile = SeedFile("old-patient.enc");
        var recentPatientFile = SeedFile("recent-patient.enc");
        var oldExamFile = SeedFile("old-exam.enc");

        _context.PatientDocuments.AddRange(
            new PatientDocument
            {
                PatientId = patient.Id,
                FileName = oldPatientFile,
                EncryptedPath = oldPatientFile,
                OriginalFileName = "old.pdf",
                UploadedAt = DateTime.UtcNow.AddDays(-60),
                IsDeleted = true
            },
            new PatientDocument
            {
                PatientId = patient.Id,
                FileName = recentPatientFile,
                EncryptedPath = recentPatientFile,
                OriginalFileName = "recent.pdf",
                UploadedAt = DateTime.UtcNow.AddDays(-1),
                IsDeleted = true
            });
        _context.ExaminationDocuments.Add(new ExaminationDocument
        {
            ExaminationId = examination.Id,
            FileName = oldExamFile,
            EncryptedPath = oldExamFile,
            OriginalFileName = "exam.pdf",
            UploadedAt = DateTime.UtcNow.AddDays(-60),
            IsDeleted = true
        });
        await _context.SaveChangesAsync();

        var purged = await DocumentRetentionService.RunOnceAsync(
            _context, _storagePath, retentionDays: 30);

        Assert.Equal(2, purged);
        Assert.False(File.Exists(Path.Combine(_storagePath, oldPatientFile)));
        Assert.False(File.Exists(Path.Combine(_storagePath, oldExamFile)));
        Assert.True(File.Exists(Path.Combine(_storagePath, recentPatientFile)));
        Assert.Single(await _context.PatientDocuments.ToListAsync());
        Assert.Empty(await _context.ExaminationDocuments.ToListAsync());
    }
}
