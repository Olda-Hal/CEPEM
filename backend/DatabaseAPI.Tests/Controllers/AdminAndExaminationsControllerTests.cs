using DatabaseAPI.APIModels;
using DatabaseAPI.Controllers;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Controllers;

/// <summary>
/// The admin controller is a thin pass-through over IEmployeeManagementService;
/// the tests pin its empty/failure mapping to HTTP results.
/// </summary>
public class AdminControllerTests
{
    private readonly Mock<IEmployeeManagementService> _service = new();
    private readonly Mock<IEmployeeScopeService> _scopeService = new();
    private readonly Mock<IPasswordResetService> _passwordResetService = new();
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _scopeService.Setup(s => s.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(null, new List<string>(), new List<int>(), new List<int>()));
        _controller = new AdminController(_service.Object, _scopeService.Object, _passwordResetService.Object);
    }

    [Fact]
    public async Task GetAllEmployees_ReturnsOk()
    {
        _service.Setup(s => s.GetAllEmployeesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeListItem> { new() { EmployeeId = 1 } });

        var result = await _controller.GetAllEmployees();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(Assert.IsType<List<EmployeeListItem>>(ok.Value));
    }

    [Fact]
    public async Task GetEmployee_ForMissingEmployee_ReturnsNotFound()
    {
        _service.Setup(s => s.GetEmployeeByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((EmployeeListItem?)null);

        var result = await _controller.GetEmployee(99999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateEmployee_OnFailure_ReturnsBadRequest()
    {
        _service.Setup(s => s.UpdateEmployeeAsync(It.IsAny<int>(), It.IsAny<UpdateEmployeeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateEmployeeResponse { Success = false, Message = "bad" });

        var result = await _controller.UpdateEmployee(1, new UpdateEmployeeRequest());

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeactivateEmployee_ForMissingEmployee_ReturnsNotFound()
    {
        _service.Setup(s => s.DeactivateEmployeeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((false, (string?)null));

        var result = await _controller.DeactivateEmployee(99999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeactivateEmployee_OnSuccess_ReturnsOk()
    {
        _service.Setup(s => s.DeactivateEmployeeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((true, (string?)null));

        var result = await _controller.DeactivateEmployee(1);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CreateRole_WhenInvalid_ReturnsBadRequest()
    {
        _service.Setup(s => s.CreateRoleAsync(It.IsAny<CreateRoleRequest>()))
            .ReturnsAsync((RoleDto?)null);

        var result = await _controller.CreateRole(new CreateRoleRequest { Name = "Admin" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}

public class SettingsControllerTests
{
    private readonly SettingsController _controller = new(NullLogger<SettingsController>.Instance);

    [Fact]
    public async Task GetQuickPreviewSettings_ReturnsDefaults()
    {
        var result = await _controller.GetQuickPreviewSettings();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<QuickPreviewSettingsDto>(ok.Value);
    }

    [Fact]
    public async Task UpdateQuickPreviewSettings_EchoesRequest()
    {
        var result = await _controller.UpdateQuickPreviewSettings(new UpdateQuickPreviewSettingsRequest
        {
            ShowDiabetes = false,
            ShowRecentEvents = true
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<QuickPreviewSettingsDto>(ok.Value);
        Assert.False(dto.ShowDiabetes);
        Assert.True(dto.ShowRecentEvents);
    }
}

public class ExaminationsControllerTests
{
    private readonly Mock<IExaminationsService> _service = new();
    private readonly ExaminationsController _controller;

    public ExaminationsControllerTests()
    {
        _controller = new ExaminationsController(_service.Object, NullLogger<ExaminationsController>.Instance);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        _service.Setup(s => s.SearchAsync(It.IsAny<ExaminationSearchFilters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExaminationSearchResponse());

        var result = await _controller.GetAll(new ExaminationSearchFilters(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAll_OnCancellation_Returns499()
    {
        _service.Setup(s => s.SearchAsync(It.IsAny<ExaminationSearchFilters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _controller.GetAll(new ExaminationSearchFilters(), CancellationToken.None);

        Assert.Equal(499, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task GetAll_OnException_Returns500()
    {
        _service.Setup(s => s.SearchAsync(It.IsAny<ExaminationSearchFilters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _controller.GetAll(new ExaminationSearchFilters(), CancellationToken.None);

        Assert.Equal(500, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task Export_ReturnsZipFile()
    {
        _service.Setup(s => s.ExportAsync(It.IsAny<ExaminationSearchFilters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExaminationExportResponse { Content = new byte[] { 1, 2 }, FileName = "export.zip" });

        var result = await _controller.Export(new ExaminationSearchFilters(), CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/zip", file.ContentType);
        Assert.Equal("export.zip", file.FileDownloadName);
    }

    [Fact]
    public async Task Export_OnCancellation_Returns499()
    {
        _service.Setup(s => s.ExportAsync(It.IsAny<ExaminationSearchFilters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _controller.Export(new ExaminationSearchFilters(), CancellationToken.None);

        Assert.Equal(499, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }
}
