using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Authorization scope matrices for Center/Country Admin: listing visibility,
/// role assignability and country/center assignment restrictions.
/// </summary>
public class EmployeeScopeServiceTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly Mock<IActorCountryContextService> _actorMock = new();

    public EmployeeScopeServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
    }

    public void Dispose() => _context.Dispose();

    private EmployeeScopeService CreateService() => new(_context, _actorMock.Object);

    private Role SeedRole(string name)
    {
        var translation = new Translation { EN = name, CS = name };
        _context.Translations.Add(translation);
        _context.SaveChanges();
        var role = new Role { NameTranslationId = translation.Id };
        _context.Roles.Add(role);
        _context.SaveChanges();
        return role;
    }

    private Employee SeedEmployee(string uid, string countryCode, params Role[] roles)
    {
        var person = new Person
        {
            FirstName = uid,
            LastName = "Test",
            UID = uid,
            Gender = "M",
            CountryCode = countryCode,
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Persons.Add(person);
        _context.SaveChanges();

        foreach (var role in roles)
        {
            _context.UserRoles.Add(new UserRole { UserId = person.Id, RoleId = role.Id });
        }

        var employee = new Employee
        {
            PersonId = person.Id,
            PasswordHash = "x",
            Salt = "x"
        };
        _context.Employees.Add(employee);
        _context.SaveChanges();
        return employee;
    }

    private Hospital SeedHospital(string name, int scopeId)
    {
        var hospital = new Hospital { Name = name, Active = true, CountryScopeId = scopeId };
        _context.Hospitals.Add(hospital);
        _context.SaveChanges();
        return hospital;
    }

    [Fact]
    public async Task SysAdmin_SeesEveryone_NoFiltering()
    {
        var sysAdmin = SeedEmployee("sys", "CZ", SeedRole("SysAdmin"));
        _actorMock.Setup(s => s.GetActorEmployeeId()).Returns(sysAdmin.Id);

        var visible = await CreateService().GetVisibleEmployeeIdsAsync();

        Assert.Null(visible);
    }

    [Fact]
    public async Task EmployeeWithoutAdminRole_SeesEveryone_CurrentBehaviorKept()
    {
        var doctor = SeedEmployee("doc", "CZ", SeedRole("Doctor"));
        _actorMock.Setup(s => s.GetActorEmployeeId()).Returns(doctor.Id);

        var visible = await CreateService().GetVisibleEmployeeIdsAsync();

        Assert.Null(visible);
    }

    [Fact]
    public async Task CountryAdmin_SeesOnlyOwnCountry_AndCreatedEmployees()
    {
        var countryAdminRole = SeedRole("Country Admin");
        var countryAdmin = SeedEmployee("cadmin", "DE", countryAdminRole);
        var german = SeedEmployee("de1", "DE");
        var czech = SeedEmployee("cz1", "CZ");
        var createdByAdmin = SeedEmployee("cz2", "CZ");
        createdByAdmin.CreatedByEmployeeId = countryAdmin.Id;
        _context.SaveChanges();

        _actorMock.Setup(s => s.GetActorEmployeeId()).Returns(countryAdmin.Id);

        var visible = await CreateService().GetVisibleEmployeeIdsAsync();

        Assert.NotNull(visible);
        Assert.Contains(german.Id, visible!);
        Assert.Contains(createdByAdmin.Id, visible!);
        Assert.Contains(countryAdmin.Id, visible!);
        Assert.DoesNotContain(czech.Id, visible!);
    }

    [Fact]
    public async Task CenterAdmin_SeesOnlyEmployeesOfOwnCenters_AndSelf()
    {
        var centerAdminRole = SeedRole("Center Admin");
        var hospitalA = SeedHospital("A", 203);
        var hospitalB = SeedHospital("B", 203);

        var centerAdmin = SeedEmployee("cadmin", "CZ", centerAdminRole);
        var colleague = SeedEmployee("coll", "CZ");
        var outsider = SeedEmployee("outs", "DE");

        _context.HospitalEmployees.Add(new HospitalEmployee { HospitalId = hospitalA.Id, EmployeeId = centerAdmin.Id });
        _context.HospitalEmployees.Add(new HospitalEmployee { HospitalId = hospitalA.Id, EmployeeId = colleague.Id });
        _context.HospitalEmployees.Add(new HospitalEmployee { HospitalId = hospitalB.Id, EmployeeId = outsider.Id });
        _context.SaveChanges();

        _actorMock.Setup(s => s.GetActorEmployeeId()).Returns(centerAdmin.Id);

        var visible = await CreateService().GetVisibleEmployeeIdsAsync();

        Assert.NotNull(visible);
        Assert.Contains(colleague.Id, visible!);
        Assert.Contains(centerAdmin.Id, visible!);
        Assert.DoesNotContain(outsider.Id, visible!);
    }

    [Fact]
    public async Task CountryAdmin_AdministeredCountries_CoverAssignedCenterScopes()
    {
        var countryAdminRole = SeedRole("Country Admin");
        var countryAdmin = SeedEmployee("cadmin", "CZ", countryAdminRole);
        var germanCenter = SeedHospital("Berlin", 276);
        _context.HospitalEmployees.Add(new HospitalEmployee { HospitalId = germanCenter.Id, EmployeeId = countryAdmin.Id });
        var germanEmployee = SeedEmployee("de1", "DE");
        _context.SaveChanges();

        _actorMock.Setup(s => s.GetActorEmployeeId()).Returns(countryAdmin.Id);

        var visible = await CreateService().GetVisibleEmployeeIdsAsync();

        Assert.NotNull(visible);
        Assert.Contains(germanEmployee.Id, visible!);
    }

    [Fact]
    public async Task CenterAdmin_CannotAssignPrivilegedRoles_ButExaminerAndDoctorAreAllowed()
    {
        var centerAdminRole = SeedRole("Center Admin");
        var sysAdminRole = SeedRole("SysAdmin");
        var countryAdminRole = SeedRole("Country Admin");
        var examinerRole = SeedRole("Examiner");
        var doctorRole = SeedRole("Doctor");

        var centerAdmin = SeedEmployee("cadmin", "CZ", centerAdminRole);
        _actorMock.Setup(s => s.GetActorEmployeeId()).Returns(centerAdmin.Id);

        var service = CreateService();

        Assert.Null(await service.ValidateAssignableRolesAsync(new[] { examinerRole.Id, doctorRole.Id }));
        Assert.NotNull(await service.ValidateAssignableRolesAsync(new[] { sysAdminRole.Id }));
        Assert.NotNull(await service.ValidateAssignableRolesAsync(new[] { countryAdminRole.Id }));
        Assert.NotNull(await service.ValidateAssignableRolesAsync(new[] { centerAdminRole.Id }));
    }

    [Fact]
    public async Task CountryAdmin_CanAssignCenterAdminAndBelow_ButNotSysAdmin()
    {
        var sysAdminRole = SeedRole("SysAdmin");
        var countryAdminRole = SeedRole("Country Admin");
        var centerAdminRole = SeedRole("Center Admin");
        var examinerRole = SeedRole("Examiner");

        var countryAdmin = SeedEmployee("cadmin", "CZ", countryAdminRole);
        _actorMock.Setup(s => s.GetActorEmployeeId()).Returns(countryAdmin.Id);

        var service = CreateService();

        Assert.Null(await service.ValidateAssignableRolesAsync(new[] { centerAdminRole.Id, examinerRole.Id }));
        Assert.NotNull(await service.ValidateAssignableRolesAsync(new[] { sysAdminRole.Id }));
    }

    [Fact]
    public async Task CenterAdmin_CountryAssignment_OnlyOwnCountry()
    {
        var centerAdminRole = SeedRole("Center Admin");
        var centerAdmin = SeedEmployee("cadmin", "CZ", centerAdminRole);
        _actorMock.Setup(s => s.GetActorEmployeeId()).Returns(centerAdmin.Id);

        var service = CreateService();

        Assert.Null(await service.ValidateCountryAssignmentAsync("CZ"));
        Assert.NotNull(await service.ValidateCountryAssignmentAsync("DE"));
    }

    [Fact]
    public async Task CenterAdmin_HospitalAssignment_OnlyOwnCenters()
    {
        var centerAdminRole = SeedRole("Center Admin");
        var centerAdmin = SeedEmployee("cadmin", "CZ", centerAdminRole);
        var own = SeedHospital("own", 203);
        var other = SeedHospital("other", 203);
        _context.HospitalEmployees.Add(new HospitalEmployee { HospitalId = own.Id, EmployeeId = centerAdmin.Id });
        _context.SaveChanges();

        _actorMock.Setup(s => s.GetActorEmployeeId()).Returns(centerAdmin.Id);

        var service = CreateService();

        Assert.Null(await service.ValidateHospitalAssignmentAsync(new[] { own.Id }));
        Assert.NotNull(await service.ValidateHospitalAssignmentAsync(new[] { other.Id }));
    }

    [Fact]
    public async Task CountryAdmin_HospitalAssignment_OnlyCentersInAdministeredCountries()
    {
        var countryAdminRole = SeedRole("Country Admin");
        var countryAdmin = SeedEmployee("cadmin", "CZ", countryAdminRole);
        var czech = SeedHospital("praha", 203);
        var german = SeedHospital("berlin", 276);
        _context.SaveChanges();

        _actorMock.Setup(s => s.GetActorEmployeeId()).Returns(countryAdmin.Id);

        var service = CreateService();

        Assert.Null(await service.ValidateHospitalAssignmentAsync(new[] { czech.Id }));
        Assert.NotNull(await service.ValidateHospitalAssignmentAsync(new[] { german.Id }));
    }
}
