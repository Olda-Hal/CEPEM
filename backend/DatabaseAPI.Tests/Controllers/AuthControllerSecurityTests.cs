using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Controllers;

/// <summary>Token revocation primitives used by the gateway.</summary>
public class AuthControllerSecurityTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly AuthController _controller;

    public AuthControllerSecurityTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);

        _controller = new AuthController(
            new Mock<IEmployeeAuthService>().Object,
            new Mock<IPasswordResetService>().Object,
            _context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Employee SeedEmployee(bool active, int accessControlVersion = 1)
    {
        var person = new Person
        {
            FirstName = "Jan",
            LastName = "Novak",
            UID = "1",
            Gender = "M",
            CountryCode = "CZ",
            Active = active,
            CreatedAt = DateTime.UtcNow
        };
        _context.Persons.Add(person);
        _context.SaveChanges();

        var employee = new Employee
        {
            PersonId = person.Id,
            PasswordHash = "x",
            Salt = "x",
            AccessControlVersion = accessControlVersion
        };
        _context.Employees.Add(employee);
        _context.SaveChanges();
        return employee;
    }

    private static bool IsValid(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        return (bool)value.GetType().GetProperty("valid")!.GetValue(value)!;
    }

    [Fact]
    public async Task TokenValid_MatchesActiveEmployeeAndVersion()
    {
        var employee = SeedEmployee(active: true, accessControlVersion: 4);

        Assert.True(IsValid(await _controller.ValidateToken(employee.Id, 4)));
    }

    [Fact]
    public async Task TokenValid_RejectsOldVersion()
    {
        var employee = SeedEmployee(active: true, accessControlVersion: 4);

        Assert.False(IsValid(await _controller.ValidateToken(employee.Id, 3)));
    }

    [Fact]
    public async Task TokenValid_RejectsDeactivatedEmployee()
    {
        var employee = SeedEmployee(active: false, accessControlVersion: 4);

        Assert.False(IsValid(await _controller.ValidateToken(employee.Id, 4)));
    }

    [Fact]
    public async Task Logout_IncrementsAccessControlVersion()
    {
        var employee = SeedEmployee(active: true, accessControlVersion: 4);

        var result = await _controller.Logout(employee.Id);

        Assert.IsType<NoContentResult>(result);
        await _context.Entry(employee).ReloadAsync();
        Assert.Equal(5, employee.AccessControlVersion);
    }
}
