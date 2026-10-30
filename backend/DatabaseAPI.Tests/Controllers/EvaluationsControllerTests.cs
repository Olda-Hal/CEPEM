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
/// TDB evaluation workflow: anonymous presentation, two independent reviews,
/// "next measurement" ordering and the interactive visit report.
/// </summary>
public class EvaluationsControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly Mock<IActorCountryContextService> _actorMock = new();
    private readonly Mock<IEmployeeScopeService> _scopeMock = new();

    public EvaluationsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new DatabaseContext(options);

        SetActor(new[] { "SysAdmin" }, Array.Empty<int>());
    }

    public void Dispose() => _context.Dispose();

    private void SetActor(string[] roles, int[] countryScopeIds, int employeeId = 1)
    {
        _actorMock.Setup(a => a.GetActorEmployeeId()).Returns(employeeId);
        _scopeMock.Setup(s => s.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(employeeId, roles, new List<int>(), countryScopeIds));
    }

    private EvaluationsController CreateController() => new(
        _context,
        _actorMock.Object,
        _scopeMock.Object,
        NullLogger<EvaluationsController>.Instance);

    private Employee SeedEmployee(string firstName)
    {
        var person = new Person
        {
            FirstName = firstName,
            LastName = "Doctor",
            UID = firstName,
            Gender = "F",
            CountryCode = "CZ",
            Active = true
        };
        _context.Persons.Add(person);
        var employee = new Employee
        {
            Person = person,
            PasswordHash = "x",
            Salt = "x"
        };
        _context.Employees.Add(employee);
        _context.SaveChanges();
        return employee;
    }

    private EvaluationCase SeedCase(
        string countryCode = "CZ",
        string imageKey = "tdb",
        DateTime? createdAt = null,
        bool withVisit = false)
    {
        var person = new Person
        {
            FirstName = "Jan",
            LastName = "Pacient",
            UID = "1",
            Gender = "M",
            CountryCode = countryCode,
            Active = true
        };
        _context.Persons.Add(person);

        var patient = new Patient
        {
            Person = person,
            BirthDate = new DateTime(1980, 1, 1),
            CountryCode = countryCode,
            InsuranceNumber = 1
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();

        Visit? visit = null;
        if (withVisit)
        {
            visit = new Visit
            {
                PatientId = patient.Id,
                VisitDate = DateTime.UtcNow.Date,
                CountryCode = countryCode
            };
            _context.Visits.Add(visit);
            _context.SaveChanges();
        }

        var evaluationCase = new EvaluationCase
        {
            Patient = patient,
            VisitId = visit?.Id,
            Title = "MEIK – TOMODB",
            ImageKey = imageKey,
            SourceSide = "Left",
            SourcePosture = "Lying",
            SlicesJson = "{\"width\":36,\"height\":36,\"windowLow\":0,\"windowHigh\":1,\"slices\":[]}",
            CreatedAtUtc = createdAt ?? DateTime.UtcNow
        };
        _context.EvaluationCases.Add(evaluationCase);
        _context.SaveChanges();
        return evaluationCase;
    }

    private EvaluationReview AddReview(EvaluationCase evaluationCase, int? employeeId, string outcome = "OK")
    {
        var review = new EvaluationReview
        {
            EvaluationCaseId = evaluationCase.Id,
            EmployeeId = employeeId,
            AnnotationsJson = "[]",
            Outcome = outcome,
            EvaluatedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.EvaluationReviews.Add(review);
        _context.SaveChanges();
        return review;
    }

    [Fact]
    public async Task GetCase_Tdb_HidesPatientIdentityButReturnsSlices()
    {
        var evaluationCase = SeedCase();

        var result = await CreateController().GetCase(evaluationCase.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EvaluationsController.EvaluationCaseDto>(ok.Value);
        Assert.Null(dto.PatientId);
        Assert.Null(dto.PatientName);
        Assert.NotNull(dto.Tdb);
        Assert.Equal("Left", dto.Side);
        Assert.Equal("Lying", dto.Posture);
        Assert.Equal(0, dto.ReviewCount);
    }

    [Fact]
    public async Task Evaluate_CreatesReview_ButDoesNotRevealItInCaseFields()
    {
        var evaluationCase = SeedCase();

        var result = await CreateController().Evaluate(evaluationCase.Id,
            new EvaluationsController.SaveEvaluationRequest
            {
                AnnotationsJson = "[{\"slice\":2,\"x\":0.4,\"y\":0.5,\"note\":\"bulka\"}]",
                Recommendation = "Kontrola za 3 měsíce",
                Outcome = "Findings"
            });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EvaluationsController.EvaluationCaseDto>(ok.Value);
        Assert.Null(dto.Outcome); // case-level fields stay hidden for TDB
        Assert.True(dto.IsEvaluated);
        Assert.Equal(1, dto.ReviewCount);

        var review = await _context.EvaluationReviews.SingleAsync();
        Assert.Equal("Findings", review.Outcome);
        Assert.Equal(1, review.EmployeeId);
        Assert.Contains("bulka", review.AnnotationsJson);
    }

    [Fact]
    public async Task Evaluate_MissingOutcomeForTdb_ReturnsBadRequest()
    {
        var evaluationCase = SeedCase();

        var result = await CreateController().Evaluate(evaluationCase.Id,
            new EvaluationsController.SaveEvaluationRequest());

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Evaluate_WithUnknownOutcome_ReturnsBadRequest()
    {
        var evaluationCase = SeedCase();

        var result = await CreateController().Evaluate(evaluationCase.Id,
            new EvaluationsController.SaveEvaluationRequest { Outcome = "Something" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Evaluate_SameDoctorTwice_ReturnsConflict()
    {
        var evaluationCase = SeedCase();
        AddReview(evaluationCase, 1);

        var result = await CreateController().Evaluate(evaluationCase.Id,
            new EvaluationsController.SaveEvaluationRequest { Outcome = "OK" });

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task Evaluate_ThirdReview_ReturnsConflict()
    {
        var evaluationCase = SeedCase();
        AddReview(evaluationCase, 2);
        AddReview(evaluationCase, 3);

        var result = await CreateController().Evaluate(evaluationCase.Id,
            new EvaluationsController.SaveEvaluationRequest { Outcome = "OK" });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetNext_ReturnsOldestUnreviewedMeasurement()
    {
        var oldest = SeedCase(createdAt: DateTime.UtcNow.AddHours(-2));
        SeedCase(createdAt: DateTime.UtcNow.AddHours(-1));

        var result = await CreateController().GetNext();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EvaluationsController.EvaluationCaseDto>(ok.Value);
        Assert.Equal(oldest.Id, dto.Id);
        Assert.Equal(0, dto.ReviewCount);
    }

    [Fact]
    public async Task GetNext_PrefersUnreviewedOverAlreadyReviewed()
    {
        var reviewed = SeedCase(createdAt: DateTime.UtcNow.AddHours(-2));
        AddReview(reviewed, 2);
        var unreviewed = SeedCase(createdAt: DateTime.UtcNow.AddHours(-1));

        var result = await CreateController().GetNext();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EvaluationsController.EvaluationCaseDto>(ok.Value);
        Assert.Equal(unreviewed.Id, dto.Id);
    }

    [Fact]
    public async Task GetNext_NeverReturnsMeasurementAlreadyReviewedByActor()
    {
        var reviewedByActor = SeedCase(createdAt: DateTime.UtcNow.AddHours(-2));
        AddReview(reviewedByActor, 1);
        var reviewedByOther = SeedCase(createdAt: DateTime.UtcNow.AddHours(-1));
        AddReview(reviewedByOther, 2);

        var result = await CreateController().GetNext();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EvaluationsController.EvaluationCaseDto>(ok.Value);
        Assert.Equal(reviewedByOther.Id, dto.Id);
    }

    [Fact]
    public async Task GetNext_WhenOnlyAvailableWasReviewedByActor_ReturnsNoContent()
    {
        var reviewedByActor = SeedCase();
        AddReview(reviewedByActor, 1);

        var result = await CreateController().GetNext();

        Assert.IsType<NoContentResult>(result.Result);
    }

    [Fact]
    public async Task GetNext_WhenEverythingHasTwoReviews_ReturnsNoContent()
    {
        var first = SeedCase();
        AddReview(first, 2);
        AddReview(first, 3);

        var result = await CreateController().GetNext();

        Assert.IsType<NoContentResult>(result.Result);
    }

    [Fact]
    public async Task GetReport_ShowsAllReviewsAndDoctorNames()
    {
        var evaluationCase = SeedCase(withVisit: true);
        var doctorA = SeedEmployee("Alice");
        var doctorB = SeedEmployee("Bob");
        AddReview(evaluationCase, doctorA.Id, "OK");
        AddReview(evaluationCase, doctorB.Id, "Findings");

        var result = await CreateController().GetReport(evaluationCase.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EvaluationsController.EvaluationCaseDto>(ok.Value);
        Assert.NotNull(dto.Reviews);
        Assert.Equal(2, dto.Reviews!.Count);
        Assert.Contains(dto.Reviews, r => r.DoctorName!.StartsWith("Alice"));
        Assert.Contains(dto.Reviews, r => r.DoctorName!.StartsWith("Bob"));
    }

    [Fact]
    public async Task GetCases_CountryAdmin_OnlySeesOwnCountries()
    {
        SetActor(new[] { "Country Admin" }, new[] { 276 }); // DE
        SeedCase("CZ");
        var germanCase = SeedCase("DE");

        var result = await CreateController().GetCases(includeEvaluated: true);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<EvaluationsController.EvaluationCaseDto>>(ok.Value);
        Assert.Single(list);
        Assert.Equal(germanCase.Id, list[0].Id);
    }
}
