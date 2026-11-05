using DatabaseAPI.APIModels;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Controllers;

/// <summary>
/// Measurements overview: visits with .tdb files, evaluation-state filter and
/// the manual "mark as evaluated" action shared with the visit report.
/// </summary>
public class MeasurementsControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly Mock<IEmployeeScopeService> _scopeMock = new();
    private readonly Mock<IActorCountryContextService> _actorMock = new();

    public MeasurementsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new DatabaseContext(options);

        _actorMock.Setup(a => a.GetActorEmployeeId()).Returns(1);
        _scopeMock.Setup(s => s.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(1, new List<string> { "SysAdmin" }, new List<int>(), new List<int>()));
    }

    public void Dispose() => _context.Dispose();

    private MeasurementsController CreateController() => new(
        _context,
        _scopeMock.Object,
        _actorMock.Object,
        NullLogger<MeasurementsController>.Instance);

    private EvaluationsController CreateEvaluationsController() => new(
        _context,
        _actorMock.Object,
        _scopeMock.Object,
        NullLogger<EvaluationsController>.Instance);

    private (Patient Patient, Visit Visit, EvaluationCase Case) SeedMeasurement(
        string firstName = "Eva",
        string lastName = "Kralova",
        bool manuallyEvaluated = false,
        int reviews = 0)
    {
        var person = new Person
        {
            FirstName = firstName,
            LastName = lastName,
            UID = "00000042",
            Gender = "F",
            CountryCode = "CZ",
            Active = true
        };
        _context.Persons.Add(person);

        var patient = new Patient
        {
            Person = person,
            BirthDate = new DateTime(1980, 1, 1),
            InsuranceNumber = 111,
            Alive = true,
            CountryCode = "CZ"
        };
        _context.Patients.Add(patient);

        var visit = new Visit
        {
            Patient = patient,
            VisitDate = new DateTime(2026, 10, 20),
            CountryCode = "CZ"
        };
        _context.Visits.Add(visit);

        var document = new PatientDocument
        {
            Patient = patient,
            FileName = "stored.enc",
            OriginalFileName = "measurement.TDB",
            UploadedAt = DateTime.UtcNow,
            FileSize = 1024,
            EncryptedPath = "stored.enc"
        };
        _context.PatientDocuments.Add(document);

        var visitDocument = new VisitDocument
        {
            Visit = visit,
            PatientDocument = document,
            SortOrder = 1
        };
        _context.VisitDocuments.Add(visitDocument);

        var evaluationCase = new EvaluationCase
        {
            Patient = patient,
            PatientDocument = document,
            Visit = visit,
            Title = "MEIK – TOMODB",
            ImageKey = "tdb",
            SlicesJson = "{}",
            CreatedAtUtc = DateTime.UtcNow,
            EvaluatedAtUtc = manuallyEvaluated ? DateTime.UtcNow : null
        };
        _context.EvaluationCases.Add(evaluationCase);

        for (var i = 0; i < reviews; i++)
        {
            _context.EvaluationReviews.Add(new EvaluationReview
            {
                EvaluationCase = evaluationCase,
                EmployeeId = null,
                Outcome = "OK",
                EvaluatedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        _context.SaveChanges();
        return (patient, visit, evaluationCase);
    }

    [Fact]
    public async Task GetMeasurements_ListsPatientVisitAndTdbFiles()
    {
        var (_, visit, _) = SeedMeasurement();

        var result = await CreateController().GetMeasurements();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<MeasurementListDto>(ok.Value);
        var item = Assert.Single(list.Items);
        Assert.Equal(visit.Id, item.VisitId);
        Assert.Equal("Kralova, Eva", item.PatientName);
        Assert.Equal("pending", item.Status);
        Assert.Single(item.Documents);
        Assert.Equal("measurement.TDB", item.Documents[0].OriginalFileName);
    }

    [Fact]
    public async Task GetMeasurements_FiltersByEvaluatedState()
    {
        SeedMeasurement("Eva", "Kralova");
        SeedMeasurement("Jan", "Novak", manuallyEvaluated: true);

        var pending = await CreateController().GetMeasurements(evaluated: "no");
        var evaluated = await CreateController().GetMeasurements(evaluated: "yes");

        var pendingList = Assert.IsType<MeasurementListDto>(
            Assert.IsType<OkObjectResult>(pending.Result).Value);
        var evaluatedList = Assert.IsType<MeasurementListDto>(
            Assert.IsType<OkObjectResult>(evaluated.Result).Value);

        Assert.Single(pendingList.Items);
        Assert.Equal("pending", pendingList.Items[0].Status);
        Assert.Single(evaluatedList.Items);
        Assert.Equal("evaluated", evaluatedList.Items[0].Status);
    }

    [Fact]
    public async Task MarkVisitEvaluated_RemovesMeasurementFromQueueAndOpensReport()
    {
        var (_, visit, evaluationCase) = SeedMeasurement();

        var result = await CreateController().MarkVisitEvaluated(visit.Id);
        Assert.IsType<OkObjectResult>(result);

        var stored = await _context.EvaluationCases.AsNoTracking()
            .SingleAsync(c => c.Id == evaluationCase.Id);
        Assert.NotNull(stored.EvaluatedAtUtc);
        Assert.Equal(1, stored.EvaluatedByEmployeeId);

        var next = await CreateEvaluationsController().GetNext();
        Assert.IsType<NoContentResult>(next.Result);

        var report = await CreateEvaluationsController().GetReport(evaluationCase.Id);
        var ok = Assert.IsType<OkObjectResult>(report.Result);
        var dto = Assert.IsType<EvaluationsController.EvaluationCaseDto>(ok.Value);
        Assert.True(dto.IsEvaluated);
        Assert.True(dto.IsManuallyEvaluated);
        Assert.Equal(2, dto.ReviewCount);
    }
}
