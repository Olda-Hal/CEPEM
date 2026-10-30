using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Security.Claims;
using System.Net.Http;
using HealthcareAPI.Controllers;
using HealthcareAPI.Models;
using HealthcareAPI.Services;

namespace HealthcareAPI.Tests.Controllers;

public class AdminControllerTests
{
    private readonly Mock<IAdminService> _mockAdminService;
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<AdminController>> _mockLogger;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _mockAdminService = new Mock<IAdminService>();
        _mockAuthService = new Mock<IAuthService>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<AdminController>>();
        _controller = new AdminController(_mockAdminService.Object, _mockAuthService.Object, _mockHttpClientFactory.Object, _mockLogger.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role, "SysAdmin")
                }, "TestAuthentication"))
            }
        };

    }

    [Fact]
    public async Task GetAllEmployees_ReturnsOk_WithEmployeeList()
    {
        // Arrange
        var employees = new List<EmployeeListItem>
        {
            new EmployeeListItem { EmployeeId = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" },
            new EmployeeListItem { EmployeeId = 2, FirstName = "Jane", LastName = "Smith", Email = "jane@example.com" }
        };

        _mockAdminService.Setup(s => s.GetAllEmployeesAsync(It.IsAny<bool>()))
            .ReturnsAsync(employees);

        // Act
        var result = await _controller.GetAllEmployees();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedEmployees = Assert.IsType<List<EmployeeListItem>>(okResult.Value);
        Assert.Equal(2, returnedEmployees.Count);
    }

    [Fact]
    public async Task GetAllEmployees_ReturnsEmptyList_WhenNoEmployees()
    {
        // Arrange
        _mockAdminService.Setup(s => s.GetAllEmployeesAsync(It.IsAny<bool>()))
            .ReturnsAsync(new List<EmployeeListItem>());

        // Act
        var result = await _controller.GetAllEmployees();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedEmployees = Assert.IsType<List<EmployeeListItem>>(okResult.Value);
        Assert.Empty(returnedEmployees);
    }

    [Fact]
    public async Task GetEmployee_ReturnsOk_WhenEmployeeExists()
    {
        // Arrange
        var employeeId = 1;
        var employee = new EmployeeListItem 
        { 
            EmployeeId = employeeId,
            FirstName = "John", 
            LastName = "Doe", 
            Email = "john@example.com" 
        };

        _mockAdminService.Setup(s => s.GetEmployeeByIdAsync(employeeId))
            .ReturnsAsync(employee);

        // Act
        var result = await _controller.GetEmployee(employeeId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedEmployee = Assert.IsType<EmployeeListItem>(okResult.Value);
        Assert.Equal(employeeId, returnedEmployee.EmployeeId);
    }

    [Fact]
    public async Task GetEmployee_ReturnsNotFound_WhenEmployeeDoesNotExist()
    {
        // Arrange
        var employeeId = 999;
        _mockAdminService.Setup(s => s.GetEmployeeByIdAsync(employeeId))
            .ReturnsAsync((EmployeeListItem?)null);

        // Act
        var result = await _controller.GetEmployee(employeeId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateEmployee_ReturnsOk_WithSuccessfulUpdate()
    {
        // Arrange
        var employeeId = 1;
        var request = new UpdateEmployeeRequest { FirstName = "John", LastName = "Updated" };
        var response = new UpdateEmployeeResponse { Success = true, Message = "Employee updated" };

        _mockAdminService.Setup(s => s.UpdateEmployeeAsync(employeeId, request))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.UpdateEmployee(employeeId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedResponse = Assert.IsType<UpdateEmployeeResponse>(okResult.Value);
        Assert.True(returnedResponse.Success);
    }

    [Fact]
    public async Task UpdateEmployee_ReturnsBadRequest_WhenUpdateFails()
    {
        // Arrange
        var employeeId = 1;
        var request = new UpdateEmployeeRequest { FirstName = "John", LastName = "Updated" };
        var response = new UpdateEmployeeResponse { Success = false, Message = "Update failed" };

        _mockAdminService.Setup(s => s.UpdateEmployeeAsync(employeeId, request))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.UpdateEmployee(employeeId, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task DeactivateEmployee_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var employeeId = 1;
        _mockAdminService.Setup(s => s.DeactivateEmployeeAsync(employeeId))
            .ReturnsAsync((true, (string?)null));

        // Act
        var result = await _controller.DeactivateEmployee(employeeId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task DeactivateEmployee_ReturnsNotFound_WhenEmployeeDoesNotExist()
    {
        // Arrange
        var employeeId = 999;
        _mockAdminService.Setup(s => s.DeactivateEmployeeAsync(employeeId))
            .ReturnsAsync((false, (string?)null));

        // Act
        var result = await _controller.DeactivateEmployee(employeeId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAllRoles_ReturnsOk_WithRoleList()
    {
        // Arrange
        var roles = new List<RoleDto>
        {
            new RoleDto { Id = 1, Name = "Doctor" },
            new RoleDto { Id = 2, Name = "Nurse" },
            new RoleDto { Id = 3, Name = "Admin" }
        };

        _mockAdminService.Setup(s => s.GetAllRolesAsync())
            .ReturnsAsync(roles);

        // Act
        var result = await _controller.GetAllRoles();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedRoles = Assert.IsType<List<RoleDto>>(okResult.Value);
        Assert.Equal(3, returnedRoles.Count);
    }

    [Fact]
    public async Task ImpersonateEmployee_ForOwnAccount_ReturnsBadRequest()
    {
        // Act — the controller's test identity is employee 1
        var result = await _controller.ImpersonateEmployee(1);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockAuthService.Verify(
            s => s.ImpersonateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ImpersonateEmployee_ReturnsOk_WithPreviewToken()
    {
        // Arrange
        var response = new ImpersonationResponse
        {
            Token = "preview-token",
            EmployeeId = 42,
            FullName = "Jane Doe",
            Email = "jane@example.com",
            ImpersonatorName = "Admin"
        };
        _mockAuthService
            .Setup(s => s.ImpersonateAsync(42, 1, It.IsAny<string>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.ImpersonateEmployee(42);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<ImpersonationResponse>(okResult.Value);
        Assert.Equal("preview-token", returned.Token);
        Assert.Equal(42, returned.EmployeeId);
    }

    [Fact]
    public async Task ImpersonateEmployee_ForUnknownTarget_ReturnsNotFound()
    {
        // Arrange
        _mockAuthService
            .Setup(s => s.ImpersonateAsync(999, 1, It.IsAny<string>()))
            .ReturnsAsync((ImpersonationResponse?)null);

        // Act
        var result = await _controller.ImpersonateEmployee(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAllRoles_ReturnsEmptyList_WhenNoRoles()
    {
        // Arrange
        _mockAdminService.Setup(s => s.GetAllRolesAsync())
            .ReturnsAsync(new List<RoleDto>());

        // Act
        var result = await _controller.GetAllRoles();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedRoles = Assert.IsType<List<RoleDto>>(okResult.Value);
        Assert.Empty(returnedRoles);
    }
}
