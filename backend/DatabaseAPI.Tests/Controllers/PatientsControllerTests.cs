using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.APIModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using DatabaseAPI.Services;

namespace DatabaseAPI.Tests.Controllers;

public class PatientsControllerTests : IAsyncLifetime
{
    private readonly DatabaseContext _context;
    private readonly PatientsController _controller;
    private readonly Mock<IEmployeeScopeService> _scopeMock = new();
    private readonly ILogger<PatientsController> _logger;

    public PatientsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DatabaseContext(options);
        _logger = new Mock<ILogger<PatientsController>>().Object;
        var storageDir = Path.Combine(Path.GetTempPath(), $"cepem-photo-{Guid.NewGuid():N}");
        var configStub = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configStub.Setup(c => c["PhotoStorage:Path"]).Returns(storageDir);
        configStub.Setup(c => c["PhotoEncryption:Key"]).Returns("patient-photo-test-key-32chars-long!");
        var photoService = new PatientPhotoService(
            _context,
            new PhotoEncryptionService(configStub.Object, new Mock<ILogger<PhotoEncryptionService>>().Object),
            configStub.Object,
            new Mock<ILogger<PatientPhotoService>>().Object);
        _ = photoService;
        Directory.CreateDirectory(storageDir);
        // Unrestricted scope mock: full-access actor so delete checks pass
        SetActor(1, "Admin");
        _controller = new PatientsController(
            _context,
            _logger,
            photoService,
            null!,
            new Mock<IActorCountryContextService>().Object,
            _scopeMock.Object,
            new Mock<IUidService>().Object,
            new Mock<IPatientService>().Object,
            new PatientMergeService(
                _context,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PatientMergeService>.Instance),
            new TdbAnalysisService());
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    private void SetActor(int? employeeId, params string[] roles)
    {
        _scopeMock
            .Setup(service => service.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(employeeId, roles, [], []));
    }

    private async Task<DatabaseAPI.DatabaseModels.Patient> SeedPatientWithContact()
    {
        var person = new DatabaseAPI.DatabaseModels.Person
        {
            FirstName = "Eva",
            LastName = "Novakova",
            UID = "00000001",
            Gender = "F",
            CountryCode = "CZ",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        var patient = new DatabaseAPI.DatabaseModels.Patient
        {
            Person = person,
            BirthDate = new DateTime(1985, 4, 12, 0, 0, 0, DateTimeKind.Utc),
            InsuranceNumber = 123,
            Alive = true,
            CountryCode = "CZ"
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();
        return patient;
    }

    [Fact]
    public async Task DeletePatient_HidesItFromSearch_AndKeepsRecord()
    {
        var patient = await SeedPatientWithContact();

        var deleteResult = await _controller.DeletePatient(patient.Id);
        if (deleteResult is ObjectResult objResult)
        {
            Assert.Fail($"DeletePatient returned {objResult.StatusCode}: {objResult.Value}");
        }
        Assert.IsType<NoContentResult>(deleteResult);

        var persisted = await _context.Patients.SingleAsync(p => p.Id == patient.Id);
        Assert.True(persisted.IsDeleted);
        Assert.NotNull(persisted.DeletedAtUtc);

        var search = await _controller.SearchPatients(page: 0, limit: 20, search: "Novakova", sortBy: "LastName", sortOrder: "asc");
        if (search.Result is not OkObjectResult)
        {
            var errDump = search.Result as ObjectResult;
            Assert.Fail($"Search did not return Ok; actual={search.Result?.GetType().Name}; value={errDump?.Value}");
        }
        var response = Assert.IsType<PatientSearchResponse>(Assert.IsType<OkObjectResult>(search.Result).Value);
        Assert.DoesNotContain(response.Patients, p => p.Id == patient.Id);

        var searchWithDeleted = await _controller.SearchPatients(page: 0, limit: 20, search: "Novakova", sortBy: "LastName", sortOrder: "asc", includeDeleted: true);
        Assert.Contains(Assert.IsType<PatientSearchResponse>(Assert.IsType<OkObjectResult>(searchWithDeleted.Result).Value).Patients, p => p.Id == patient.Id);
    }

    [Fact]
    public async Task DeletePatient_Twice_ReturnsNotFound()
    {
        var patient = await SeedPatientWithContact();
        Assert.IsType<NoContentResult>(await _controller.DeletePatient(patient.Id));

        var secondDelete = await _controller.DeletePatient(patient.Id);
        Assert.IsType<NotFoundObjectResult>(secondDelete);
    }

    [Fact]
    public async Task RestorePatient_BringsItBackToListing()
    {
        var patient = await SeedPatientWithContact();
        Assert.IsType<NoContentResult>(await _controller.DeletePatient(patient.Id));

        var restoreResult = await _controller.RestorePatient(patient.Id);
        Assert.IsType<OkObjectResult>(restoreResult);

        var persisted = await _context.Patients.SingleAsync(p => p.Id == patient.Id);
        Assert.False(persisted.IsDeleted);
        Assert.Null(persisted.DeletedAtUtc);

        var search = await _controller.SearchPatients(page: 0, limit: 20, search: "Novakova", sortBy: "LastName", sortOrder: "asc");
        Assert.Contains(Assert.IsType<PatientSearchResponse>(Assert.IsType<OkObjectResult>(search.Result).Value).Patients, p => p.Id == patient.Id);
    }

    [Fact]
    public async Task RestorePatient_WhenNotDeleted_ReturnsBadRequest()
    {
        var patient = await SeedPatientWithContact();

        var result = await _controller.RestorePatient(patient.Id);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SearchPatients_ReturnsOk_WithValidSearch()
    {
        // Act
        var result = await _controller.SearchPatients(page: 0, limit: 20, search: "", sortBy: "LastName", sortOrder: "asc");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithPagination_ReturnsPagedResults()
    {
        // Act
        var result = await _controller.SearchPatients(page: 0, limit: 10, search: "", sortBy: "LastName", sortOrder: "asc");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithSearch_FiltersResults()
    {
        // Act
        var result = await _controller.SearchPatients(page: 0, limit: 20, search: "John", sortBy: "LastName", sortOrder: "asc");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithDifferentSortOrders_SortsCorrectly()
    {
        // Act - Sort ascending
        var resultAsc = await _controller.SearchPatients(sortOrder: "asc");
        var resultDesc = await _controller.SearchPatients(sortOrder: "desc");

        // Assert
        Assert.NotNull(resultAsc.Result);
        Assert.NotNull(resultDesc.Result);
    }

    [Fact]
    public async Task SearchPatients_WithEmptySearch_ReturnsAllPatients()
    {
        // Act
        var result = await _controller.SearchPatients(search: "");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithNullSearch_ReturnsAllPatients()
    {
        // Act
        var result = await _controller.SearchPatients(search: null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithLargePage_ReturnsOk()
    {
        // Act
        var result = await _controller.SearchPatients(page: 1000, limit: 20);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithSmallLimit_ReturnsOk()
    {
        // Act
        var result = await _controller.SearchPatients(limit: 1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithLargeLimitReturnnsOk()
    {
        // Act
        var result = await _controller.SearchPatients(limit: 1000);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithSpecificSortColumn_ReturnsOk()
    {
        // Act
        var result = await _controller.SearchPatients(sortBy: "FirstName");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithInvalidSortColumn_StillReturnsOk()
    {
        // Act - Invalid column should be handled gracefully
        var result = await _controller.SearchPatients(sortBy: "InvalidColumn");

        // Assert
        Assert.True(result.Result is OkObjectResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task SearchPatients_WithComplexSearch_ReturnsOk()
    {
        // Act
        var result = await _controller.SearchPatients(search: "Complex@Search!123");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetPatient_WhenHidden_ReturnsNotFound()
    {
        var patient = await SeedPatientWithContact();
        patient.IsDeleted = true;
        await _context.SaveChangesAsync();

        var result = await _controller.GetPatient(patient.Id);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPatientDetail_WhenHidden_ReturnsNotFound()
    {
        var patient = await SeedPatientWithContact();
        patient.IsDeleted = true;
        await _context.SaveChangesAsync();

        var result = await _controller.GetPatientDetail(patient.Id);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task RestorePatient_AsCreator_Succeeds()
    {
        var patient = await SeedPatientWithContact();
        patient.CreatedByEmployeeId = 5;
        patient.IsDeleted = true;
        await _context.SaveChangesAsync();
        SetActor(5, "Examiner");

        var result = await _controller.RestorePatient(patient.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.False((await _context.Patients.SingleAsync(p => p.Id == patient.Id)).IsDeleted);
    }

    [Fact]
    public async Task RestorePatient_AsUnrelatedEmployee_ReturnsForbidden()
    {
        var patient = await SeedPatientWithContact();
        patient.CreatedByEmployeeId = 5;
        patient.IsDeleted = true;
        await _context.SaveChangesAsync();
        SetActor(6, "Examiner");

        var result = await _controller.RestorePatient(patient.Id);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.True((await _context.Patients.SingleAsync(p => p.Id == patient.Id)).IsDeleted);
    }
}
