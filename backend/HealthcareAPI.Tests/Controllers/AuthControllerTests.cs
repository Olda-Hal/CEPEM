using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using HealthcareAPI.Controllers;
using HealthcareAPI.Models;
using HealthcareAPI.Services;

namespace HealthcareAPI.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _controller = new AuthController(_mockAuthService.Object);
    }

    [Fact]
    public async Task Login_ReturnsOk_WithValidCredentials()
    {
        // Arrange
        var request = new LoginRequest { Email = "user@example.com", Password = "password123" };
        var response = new LoginResponse { Token = "test-token", ExpiresIn = 3600 };
        
        _mockAuthService.Setup(s => s.LoginAsync(request))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(response, okResult.Value);
        _mockAuthService.Verify(s => s.LoginAsync(request), Times.Once);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WithInvalidCredentials()
    {
        // Arrange
        var request = new LoginRequest { Email = "user@example.com", Password = "wrongpassword" };
        
        _mockAuthService.Setup(s => s.LoginAsync(request))
            .ReturnsAsync((LoginResponse?)null);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Contains("Neplatné přihlašovací údaje", unauthorizedResult.Value?.ToString());
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenAccountDeactivated()
    {
        // Arrange
        var request = new LoginRequest { Email = "user@example.com", Password = "password123" };
        
        _mockAuthService.Setup(s => s.LoginAsync(request))
            .ThrowsAsync(new UnauthorizedAccessException("Account is deactivated"));

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task ChangePassword_ReturnsOk_WithValidData()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "oldpassword",
            NewPassword = "newpassword123",
            ConfirmPassword = "newpassword123"
        };

        _mockAuthService.Setup(s => s.ChangePasswordAsync(1, request))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenPasswordTooShort()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "oldpassword",
            NewPassword = "short",
            ConfirmPassword = "short"
        };

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("alespoň 6 znaků", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenPasswordsDoNotMatch()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "oldpassword",
            NewPassword = "newpassword123",
            ConfirmPassword = "differentpassword"
        };

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Neplatné údaje", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenCurrentPasswordWrong()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "wrongpassword",
            NewPassword = "newpassword123",
            ConfirmPassword = "newpassword123"
        };

        _mockAuthService.Setup(s => s.ChangePasswordAsync(It.IsAny<int>(), request))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task CreateEmployee_ReturnsOk_WithValidData()
    {
        // Arrange
        var request = new CreateEmployeeRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            PhoneNumber = "123456789",
            UID = "12345678",
            Gender = "M",
            Password = "password123"
        };

        var response = new CreateEmployeeResponse { EmployeeId = 1, Message = "Employee created" };
        
        _mockAuthService.Setup(s => s.CreateEmployeeAsync(request))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.CreateEmployee(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task CreateEmployee_ReturnsBadRequest_WhenMissingFields()
    {
        // Arrange
        var request = new CreateEmployeeRequest
        {
            FirstName = "John",
            LastName = null,
            Email = "john@example.com",
            PhoneNumber = "123456789",
            UID = "12345678",
            Gender = "M",
            Password = "password123"
        };

        // Act
        var result = await _controller.CreateEmployee(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("povinná pole", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task CreateEmployee_ReturnsBadRequest_WhenPasswordTooShort()
    {
        // Arrange
        var request = new CreateEmployeeRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            PhoneNumber = "123456789",
            UID = "12345678",
            Gender = "M",
            Password = "short"
        };

        // Act
        var result = await _controller.CreateEmployee(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("6 znaků", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task CreateEmployee_ReturnsBadRequest_WhenEmailAlreadyExists()
    {
        // Arrange
        var request = new CreateEmployeeRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "existing@example.com",
            PhoneNumber = "123456789",
            UID = "12345678",
            Gender = "M",
            Password = "password123"
        };

        _mockAuthService.Setup(s => s.CreateEmployeeAsync(request))
            .ReturnsAsync((CreateEmployeeResponse?)null);

        // Act
        var result = await _controller.CreateEmployee(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("Email nebo UID", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task GetNextAvailableUid_ReturnsNextUid()
    {
        // Arrange
        var nextUid = "00000010";
        _mockAuthService.Setup(s => s.GetNextAvailableUidAsync())
            .ReturnsAsync(nextUid);

        // Act
        var result = await _controller.GetNextAvailableUid();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(nextUid, okResult.Value);
    }
}
