using System.Buffers.Binary;
using System.Text;
using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>Visit lifecycle: create, note/date update, document attachment, intake linkage.</summary>
public class VisitServiceTests : IDisposable
{
    private readonly DatabaseContext _context;

    public VisitServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
    }

    public void Dispose() => _context.Dispose();

    private Patient SeedPatient()
    {
        var person = new Person
        {
            FirstName = "Pac",
            LastName = "Test",
            UID = "9",
            Gender = "M",
            CountryCode = "CZ",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Persons.Add(person);
        var patient = new Patient
        {
            PersonId = person.Id,
            BirthDate = new DateTime(1990, 1, 1),
            CountryCode = "CZ"
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();
        return patient;
    }

    private VisitService CreateService()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentEncryption:Key"] = Convert.ToBase64String(new byte[32]),
                ["DocumentStorage:Path"] = Path.Combine(Path.GetTempPath(), "cepem-visits-tests-" + Guid.NewGuid())
            })
            .Build();
        var encryption = new DocumentEncryptionService(
            configuration,
            NullLogger<DocumentEncryptionService>.Instance);
        var documents = new PatientDocumentService(
            _context,
            encryption,
            new TdbAnalysisService(),
            configuration,
            NullLogger<PatientDocumentService>.Instance);
        return new VisitService(_context, documents);
    }

    private static byte[] BuildTdbMeasurement()
    {
        var data = new byte[456909];
        Encoding.ASCII.GetBytes("TOMODB v2.0").CopyTo(data, 0);
        Encoding.ASCII.GetBytes("left lying").CopyTo(data, 0x0081);

        var tail = data.Length - 7 * 36 * 36 * 4;
        for (var i = 0; i < 7 * 36 * 36; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(
                data.AsSpan(tail + i * 4, 4), 1f + i / 5000f);
        }

        return data;
    }

    [Fact]
    public async Task AttachDocument_Tdb_CreatesAnonymisedEvaluationCase()
    {
        var patient = SeedPatient();
        var service = CreateService();
        var visit = await service.CreateVisitAsync(
            patient.Id, new CreateVisitRequest { VisitDate = DateTime.UtcNow.Date }, null, "CZ");

        var document = await service.AttachDocumentAsync(
            patient.Id, visit!.Id, BuildTdbMeasurement(), "mereni.tdb");

        Assert.NotNull(document);
        var evaluationCase = Assert.Single(_context.EvaluationCases);
        Assert.Equal("tdb", evaluationCase.ImageKey);
        Assert.Equal("Left", evaluationCase.SourceSide);
        Assert.Equal("Lying", evaluationCase.SourcePosture);
        Assert.False(string.IsNullOrWhiteSpace(evaluationCase.SlicesJson));
        Assert.Equal(patient.Id, evaluationCase.PatientId);
        Assert.Equal(document!.DocumentId, evaluationCase.PatientDocumentId);
    }

    [Fact]
    public async Task AttachDocument_InvalidTdb_IsRejected()
    {
        var patient = SeedPatient();
        var service = CreateService();
        var visit = await service.CreateVisitAsync(
            patient.Id, new CreateVisitRequest { VisitDate = DateTime.UtcNow.Date }, null, "CZ");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AttachDocumentAsync(patient.Id, visit!.Id, new byte[1024], "broken.tdb"));

        Assert.Empty(_context.PatientDocuments);
        Assert.Empty(_context.EvaluationCases);
    }

    [Fact]
    public async Task CreateVisit_MissingPatient_ReturnsNull()
    {
        var result = await CreateService().CreateVisitAsync(999, new CreateVisitRequest { VisitDate = DateTime.UtcNow.Date }, null, "CZ");
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAndUpdateVisit_PersistsDateAndNote()
    {
        var patient = SeedPatient();
        var service = CreateService();

        var created = await service.CreateVisitAsync(patient.Id,
            new CreateVisitRequest { VisitDate = new DateTime(2026, 3, 5, 15, 30, 0), Note = "První návštěva" }, null, "CZ");

        Assert.NotNull(created);
        Assert.Equal(new DateTime(2026, 3, 5), created!.VisitDate);
        Assert.Equal("První návštěva", created.Note);

        var updated = await service.UpdateVisitAsync(patient.Id, created.Id,
            new UpdateVisitRequest { VisitDate = new DateTime(2026, 3, 6), Note = "Upraveno" });

        Assert.NotNull(updated);
        Assert.Equal(new DateTime(2026, 3, 6), updated!.VisitDate);
        Assert.Equal("Upraveno", updated.Note);
    }

    [Fact]
    public async Task AttachDocument_LinksDocumentToVisit()
    {
        var patient = SeedPatient();
        var service = CreateService();
        var visit = await service.CreateVisitAsync(patient.Id, new CreateVisitRequest { VisitDate = DateTime.UtcNow.Date }, null, "CZ");

        var doc = await service.AttachDocumentAsync(patient.Id, visit!.Id, new byte[] { 1, 2, 3 }, "napoveda.pdf");

        Assert.NotNull(doc);
        var reloaded = await service.GetVisitAsync(patient.Id, visit.Id);
        Assert.Single(reloaded!.Documents);
        Assert.Equal("napoveda.pdf", reloaded.Documents[0].OriginalFileName);

        // Second attach increments sort order
        await service.AttachDocumentAsync(patient.Id, visit.Id, new byte[] { 4, 5 }, "druhy.pdf");
        reloaded = await service.GetVisitAsync(patient.Id, visit.Id);
        Assert.Equal(2, reloaded!.Documents.Count);
    }

    [Fact]
    public async Task UpdateVisit_WithForeignEvent_IsRejected()
    {
        var patient = SeedPatient();
        var otherPatient = SeedPatient();
        var service = CreateService();
        var visit = await service.CreateVisitAsync(patient.Id, new CreateVisitRequest { VisitDate = DateTime.UtcNow.Date }, null, "CZ");

        var eventType = new EventType { NameTranslation = new Translation { EN = "Intake", CS = "Intake" } };
        _context.EventTypes.Add(eventType);
        await _context.SaveChangesAsync();
        var foreignEvent = new Event { PatientId = otherPatient.Id, EventTypeId = eventType.Id, HappenedAt = DateTime.UtcNow };
        _context.Events.Add(foreignEvent);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<ScopeViolationException>(
            () => service.UpdateVisitAsync(patient.Id, visit!.Id, new UpdateVisitRequest { SourceEventId = foreignEvent.Id }));
    }
}
