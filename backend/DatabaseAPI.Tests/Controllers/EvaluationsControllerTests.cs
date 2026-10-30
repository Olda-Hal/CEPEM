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
/// TDB evaluation cases: anonymity towards the evaluating doctor, outcome
/// validation and the country-admin visibility rule.
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
            .Options;
        _context = new DatabaseContext(options);

        _actorMock.Setup(a => a.GetActorEmployeeId()).Returns(1);
        SetActor(new[] { "SysAdmin" }, Array.Empty<int>());
    }

    public void Dispose() => _context.Dispose();

    private void SetActor(string[] roles, int[] countryScopeIds)
    {
        _scopeMock.Setup(s => s.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(1, roles, new List<int>(), countryScopeIds));
    }

    private EvaluationsController CreateController() => new(
        _context,
        _actorMock.Object,
        _scopeMock.Object,
        NullLogger<EvaluationsController>.Instance);

    private EvaluationCase SeedCase(string countryCode = "CZ", string imageKey = "tdb")
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

        var evaluationCase = new EvaluationCase
        {
            Patient = patient,
            Title = "MEIK – TOMODB",
            ImageKey = imageKey,
            SourceSide = "Left",
            SourcePosture = "Lying",
            SlicesJson = "{\"width\":36,\"height\":36,\"windowLow\":0,\"windowHigh\":1,\"slices\":[]}",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.EvaluationCases.Add(evaluationCase);
        _context.SaveChanges();
        return evaluationCase;
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
    }

    [Fact]
    public async Task Evaluate_WithValidOutcome_MarksCaseEvaluated()
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
        Assert.Equal("Findings", dto.Outcome);
        Assert.True(dto.IsEvaluated);

        var stored = await _context.EvaluationCases.FindAsync(evaluationCase.Id);
        Assert.NotNull(stored);
        Assert.Equal("Findings", stored!.Outcome);
        Assert.Equal(1, stored.EvaluatedByEmployeeId);
        Assert.NotNull(stored.EvaluatedAtUtc);
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
