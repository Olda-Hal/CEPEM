using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using HealthcareAPI.Controllers;
using HealthcareAPI.Models;
using HealthcareAPI.Services;

namespace HealthcareAPI.Tests.Controllers;

public class AdminControllerTests
{
    private readonly Mock<IAdminService> _mockAdminService;
    private readonly Mock<ILogger<AdminController>> _mockLogger;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _mockAdminService = new Mock<IAdminService>();
        _mockLogger = new Mock<ILogger<AdminController>>();
        _controller = new AdminController(_mockAdminService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAllEmployees_ReturnsOk_WithEmployeeList()
    {
        // Arrange
        var employees = new List<EmployeeListItem>
        {
            new EmployeeListItem { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" },
            new EmployeeListItem { Id = 2, FirstName = "Jane", LastName = "Smith", Email = "jane@example.com" }
        };

        _mockAdminService.Setup(s => s.GetAllEmployeesAsync())
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
        _mockAdminService.Setup(s => s.GetAllEmployeesAsync())
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
            Id = employeeId, 
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
        Assert.Equal(employeeId, returnedEmployee.Id);
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
            .ReturnsAsync(true);

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
            .ReturnsAsync(false);

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
