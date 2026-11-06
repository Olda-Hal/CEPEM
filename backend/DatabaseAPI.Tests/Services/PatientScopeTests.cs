using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using DatabaseAPI.Utils;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Country/Center admins must not see patients outside their administrative
/// scope; full admins and ordinary staff keep the clinic-wide view.
/// </summary>
public class PatientScopeTests : IDisposable
{
    private readonly DatabaseContext _context;

    public PatientScopeTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        Seed();
    }

    public void Dispose() => _context.Dispose();

    private void Seed()
    {
        var czechPerson = new Person
        {
            FirstName = "Czech",
            LastName = "Patient",
            UID = "1",
            Gender = "F",
            CountryCode = "CZ",
            Active = true
        };
        var germanPerson = new Person
        {
            FirstName = "German",
            LastName = "Patient",
            UID = "2",
            Gender = "F",
            CountryCode = "DE",
            Active = true
        };
        _context.Persons.AddRange(czechPerson, germanPerson);

        var czechCenter = new Hospital { Name = "Praha", Active = true, CountryScopeId = 203 };
        var germanCenter = new Hospital { Name = "Berlin", Active = true, CountryScopeId = 276 };
        _context.Hospitals.AddRange(czechCenter, germanCenter);

        _context.Patients.AddRange(
            new Patient
            {
                Person = czechPerson,
                BirthDate = new DateTime(1980, 1, 1),
                CountryCode = "CZ",
                HomeHospital = czechCenter
            },
            new Patient
            {
                Person = germanPerson,
                BirthDate = new DateTime(1981, 1, 1),
                CountryCode = "DE",
                HomeHospital = germanCenter
            });
        _context.SaveChanges();
    }

    private static ActorScope Scope(string[] roles, int[] hospitalIds, int[] countryScopeIds) =>
        new(1, roles, hospitalIds, countryScopeIds);

    [Fact]
    public async Task CountryAdmin_SeesOnlyOwnCountry()
    {
        var scope = Scope(new[] { "Country Admin" }, Array.Empty<int>(), new[] { 276 });

        var rows = await PatientScope.Apply(_context.Patients, scope).ToListAsync();

        var patient = Assert.Single(rows);
        Assert.Equal("DE", patient.CountryCode);
    }

    [Fact]
    public async Task CenterAdmin_SeesOnlyOwnCenter()
    {
        var centerId = _context.Hospitals.Single(h => h.Name == "Praha").Id;
        var scope = Scope(new[] { "Center Admin" }, new[] { centerId }, new[] { 203 });

        var rows = await PatientScope.Apply(_context.Patients, scope).ToListAsync();

        var patient = Assert.Single(rows);
        Assert.Equal("CZ", patient.CountryCode);
    }

    [Fact]
    public async Task FullAdminAndRegularStaff_SeeEveryone()
    {
        foreach (var scope in new[]
                 {
                     Scope(new[] { "Admin" }, Array.Empty<int>(), Array.Empty<int>()),
                     Scope(new[] { "Examiner" }, Array.Empty<int>(), Array.Empty<int>())
                 })
        {
            Assert.Equal(2, await PatientScope.Apply(_context.Patients, scope).CountAsync());
        }
    }
}
