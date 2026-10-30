using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Employee listing visibility: deactivated accounts must not appear in the
/// default working list, but remain reachable through includeInactive so an
/// administrator can re-activate them.
/// </summary>
public class EmployeeManagementServiceTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly Mock<IEmployeeScopeService> _scopeMock = new();

    public EmployeeManagementServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);

        _scopeMock
            .Setup(s => s.GetVisibleEmployeeIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((HashSet<int>?)null);
    }

    public void Dispose() => _context.Dispose();

    private EmployeeManagementService CreateService() => new(_context, _scopeMock.Object);

    private Employee SeedEmployee(string uid, bool active)
    {
        var person = new Person
        {
            FirstName = uid,
            LastName = "Test",
            UID = uid,
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
            PasswordExpiration = DateTime.UtcNow.AddDays(30)
        };
        _context.Employees.Add(employee);
        _context.SaveChanges();
        return employee;
    }

    [Fact]
    public async Task GetAllEmployees_HidesInactiveByDefault()
    {
        var active = SeedEmployee("ACTIVE", active: true);
        SeedEmployee("INACTIVE", active: false);

        var employees = await CreateService().GetAllEmployeesAsync();

        Assert.Single(employees);
        Assert.Equal(active.Id, employees[0].EmployeeId);
    }

    [Fact]
    public async Task GetAllEmployees_IncludeInactive_ReturnsEveryone()
    {
        SeedEmployee("ACTIVE", active: true);
        SeedEmployee("INACTIVE", active: false);

        var employees = await CreateService().GetAllEmployeesAsync(includeInactive: true);

        Assert.Equal(2, employees.Count);
    }
}
