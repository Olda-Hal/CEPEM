using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Patient creation defaults: home center comes from the creating employee's
/// assignment; an employee without an assigned center may not create patients.
/// </summary>
public class PatientServiceTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly Mock<IActorCountryContextService> _actorMock = new();
    private readonly Mock<IUidService> _uidMock = new();

    public PatientServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _uidMock.Setup(u => u.GetNextAvailableUidAsync()).ReturnsAsync("00000001");
    }

    public void Dispose() => _context.Dispose();

    private PatientService CreateService() => new(_context, _uidMock.Object, _actorMock.Object);

    private Employee SeedEmployee(bool withCenterAssignment)
    {
        var person = new Person
        {
            FirstName = "Tvůrce",
            LastName = "Test",
            UID = "1",
            Gender = "M",
            CountryCode = "CZ",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Persons.Add(person);
        var employee = new Employee { PersonId = person.Id, PasswordHash = "x", Salt = "x" };
        _context.Employees.Add(employee);
        _context.SaveChanges();

        if (withCenterAssignment)
        {
            var hospital = new Hospital { Name = "Test centrum", Active = true, CountryScopeId = 203 };
            _context.Hospitals.Add(hospital);
            _context.SaveChanges();
            _context.HospitalEmployees.Add(new HospitalEmployee { HospitalId = hospital.Id, EmployeeId = employee.Id });
            _context.SaveChanges();
        }

        return employee;
    }

    private static CreatePatientRequest ValidRequest() => new()
    {
        FirstName = "Pacient",
        LastName = "Nový",
        Gender = "M",
        BirthDate = new DateTime(2000, 1, 1)
    };

    [Fact]
    public async Task CreatePatient_WithoutCenterAssignment_Throws()
    {
        var employee = SeedEmployee(withCenterAssignment: false);
        _actorMock.Setup(a => a.GetActorEmployeeId()).Returns(employee.Id);

        await Assert.ThrowsAsync<ScopeViolationException>(
            () => CreateService().CreatePatientAsync(ValidRequest(), "CZ"));
    }

    [Fact]
    public async Task CreatePatient_NoActorEmployee_Throws()
    {
        _actorMock.Setup(a => a.GetActorEmployeeId()).Returns((int?)null);

        await Assert.ThrowsAsync<ScopeViolationException>(
            () => CreateService().CreatePatientAsync(ValidRequest(), "CZ"));
    }

    [Fact]
    public async Task CreatePatient_WithCenterAssignment_AssignsHomeHospital()
    {
        var employee = SeedEmployee(withCenterAssignment: true);
        _actorMock.Setup(a => a.GetActorEmployeeId()).Returns(employee.Id);

        var patient = await CreateService().CreatePatientAsync(ValidRequest(), "CZ");

        Assert.NotNull(patient.HomeHospitalId);
        Assert.Equal(
            await _context.HospitalEmployees.Where(he => he.EmployeeId == employee.Id).Select(he => he.HospitalId).FirstAsync(),
            patient.HomeHospitalId);
        Assert.Equal("CZ", patient.CountryCode);
    }
}
