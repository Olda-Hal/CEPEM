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
            .Setup(s => s.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(
                1,
                new[] { "SysAdmin" },
                Array.Empty<int>(),
                Array.Empty<int>()));
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

    [Fact]
    public async Task GetAllEmployees_NonAdminWithoutScope_SeesNobody()
    {
        SeedEmployee("ACTIVE", active: true);

        _scopeMock
            .Setup(s => s.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(
                99,
                new[] { "Doctor" },
                Array.Empty<int>(),
                Array.Empty<int>()));

        var employees = await CreateService().GetAllEmployeesAsync();

        Assert.Empty(employees);
    }

    [Fact]
    public async Task GetAllEmployees_ScopedAdmin_OnlySeesVisibleIds()
    {
        var visible = SeedEmployee("VISIBLE", active: true);
        SeedEmployee("HIDDEN", active: true);

        _scopeMock
            .Setup(s => s.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(
                99,
                new[] { "Center Admin" },
                new[] { 1 },
                new[] { 203 }));
        _scopeMock
            .Setup(s => s.GetVisibleEmployeeIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int> { visible.Id });

        var employees = await CreateService().GetAllEmployeesAsync();

        Assert.Single(employees);
        Assert.Equal(visible.Id, employees[0].EmployeeId);
    }

    [Fact]
    public async Task DeactivateEmployee_IncrementsAccessControlVersion()
    {
        var target = SeedEmployee("TARGET", active: true);
        var before = target.AccessControlVersion;

        var (found, forbidden) = await CreateService().DeactivateEmployeeAsync(target.Id);

        Assert.True(found);
        Assert.Null(forbidden);
        await _context.Entry(target).ReloadAsync();
        var person = await _context.Persons.SingleAsync(p => p.Id == target.PersonId);
        Assert.False(person.Active);
        Assert.Equal(before + 1, target.AccessControlVersion);
    }

    [Fact]
    public async Task ResetPassword_OutOfScope_ReturnsNull()
    {
        var target = SeedEmployee("TARGET", active: true);

        _scopeMock
            .Setup(s => s.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(
                99,
                new[] { "Center Admin" },
                new[] { 1 },
                new[] { 203 }));
        _scopeMock
            .Setup(s => s.GetVisibleEmployeeIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int>());

        Assert.Null(await CreateService().ResetPasswordAsync(target.Id));
    }

    [Fact]
    public async Task AdminOperator_CannotManageSysAdminAccounts()
    {
        var translation = new Translation { EN = "SysAdmin", CS = "SysAdmin" };
        _context.Translations.Add(translation);
        var role = new Role { NameTranslation = translation };
        _context.Roles.Add(role);
        var sysAdminPerson = new Person
        {
            FirstName = "System",
            LastName = "Admin",
            UID = "SYS",
            Gender = "M",
            CountryCode = "CZ",
            Active = true
        };
        _context.Persons.Add(sysAdminPerson);
        _context.UserRoles.Add(new UserRole { UserId = sysAdminPerson.Id, RoleId = role.Id });
        var sysAdmin = new Employee
        {
            Person = sysAdminPerson,
            PasswordHash = "x",
            Salt = "x"
        };
        _context.Employees.Add(sysAdmin);
        _context.SaveChanges();

        var operatorEmployee = SeedEmployee("OPERATOR", active: true);
        _scopeMock
            .Setup(s => s.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(
                1,
                new[] { "Admin" },
                Array.Empty<int>(),
                Array.Empty<int>()));

        var service = CreateService();
        var employees = await service.GetAllEmployeesAsync();

        Assert.DoesNotContain(employees, employee => employee.EmployeeId == sysAdmin.Id);
        Assert.Contains(employees, employee => employee.EmployeeId == operatorEmployee.Id);
        Assert.False(await service.IsEmployeeVisibleForManagementAsync(sysAdmin.Id));
    }
}
