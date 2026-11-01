using DatabaseAPI.APIModels;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Controllers;

/// <summary>Clinic analytics: metrics, date range and actor scope.</summary>
public class AnalyticsControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly Mock<IEmployeeScopeService> _scopeMock = new();
    private readonly AnalyticsController _controller;

    private readonly DateTime _day = new(2026, 10, 20);

    public AnalyticsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);

        SetActor(new ActorScope(1, ["SysAdmin"], [], []));
        _controller = new AnalyticsController(
            _context, _scopeMock.Object, NullLogger<AnalyticsController>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SetActor(ActorScope scope)
    {
        _scopeMock
            .Setup(service => service.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(scope);
    }

    private Hospital SeedHospital(string name = "Centrum A", int countryScopeId = 203)
    {
        var hospital = new Hospital { Name = name, Active = true, CountryScopeId = countryScopeId };
        _context.Hospitals.Add(hospital);
        _context.SaveChanges();
        return hospital;
    }

    private Patient SeedPatient(int? homeHospitalId = null, string countryCode = "CZ")
    {
        var person = new Person
        {
            FirstName = "Eva", LastName = "Test", UID = Guid.NewGuid().ToString("N")[..10],
            Gender = "F", CountryCode = countryCode, Active = true
        };
        _context.Persons.Add(person);
        _context.SaveChanges();
        var patient = new Patient
        {
            PersonId = person.Id,
            BirthDate = new DateTime(1990, 1, 1),
            CountryCode = countryCode,
            HomeHospitalId = homeHospitalId
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();
        return patient;
    }

    private Employee SeedDoctor(string firstName = "Jan", string lastName = "Lekar")
    {
        var person = new Person
        {
            FirstName = firstName, LastName = lastName, UID = Guid.NewGuid().ToString("N")[..10],
            Gender = "M", CountryCode = "CZ", Active = true
        };
        _context.Persons.Add(person);
        _context.SaveChanges();
        var employee = new Employee { PersonId = person.Id, PasswordHash = "x", Salt = "s" };
        _context.Employees.Add(employee);
        _context.SaveChanges();
        return employee;
    }

    private ReservationSlot SeedSlot(
        int hospitalId,
        DateTime start,
        SlotStatus status,
        int? doctorId = null)
    {
        var slot = new ReservationSlot
        {
            HospitalId = hospitalId,
            StartDateTime = start,
            EndDateTime = start.AddHours(1),
            Status = status,
            DoctorId = doctorId,
            CountryCode = "CZ"
        };
        _context.ReservationSlots.Add(slot);
        _context.SaveChanges();
        return slot;
    }

    private Visit SeedVisit(Patient patient, DateTime date, int? slotId, bool withIntake)
    {
        var visit = new Visit
        {
            PatientId = patient.Id,
            VisitDate = date,
            ReservationSlotId = slotId,
            SourceEventId = withIntake ? 1 : null,
            CountryCode = "CZ"
        };
        _context.Visits.Add(visit);
        _context.SaveChanges();
        return visit;
    }

    private EvaluationCase SeedCase(Patient patient, DateTime created, int reviews)
    {
        var evaluation = new EvaluationCase
        {
            PatientId = patient.Id,
            Title = "MEIK",
            ImageKey = "tdb",
            CreatedAtUtc = created
        };
        for (var index = 0; index < reviews; index++)
        {
            evaluation.Reviews.Add(new EvaluationReview
            {
                EmployeeId = null,
                EvaluatedAtUtc = created.AddHours(2 + index * 2),
                Outcome = "OK"
            });
        }
        _context.EvaluationCases.Add(evaluation);
        _context.SaveChanges();
        return evaluation;
    }

    private async Task<AnalyticsSummaryDto> GetSummaryAsync(
        DateTime? from = null, DateTime? to = null, int? hospitalId = null)
    {
        var result = await _controller.GetSummary(
            from ?? _day.AddDays(-1), to ?? _day.AddDays(2), hospitalId);
        return Assert.IsType<AnalyticsSummaryDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Summary_ComputesCoreMetrics()
    {
        var hospital = SeedHospital();
        var doctor = SeedDoctor();
        var patient = SeedPatient(hospital.Id);

        SeedSlot(hospital.Id, _day.AddHours(8), SlotStatus.Available);
        SeedSlot(hospital.Id, _day.AddHours(9), SlotStatus.Blocked);
        var reservedPast = SeedSlot(hospital.Id, _day.AddHours(10), SlotStatus.Reserved, doctor.Id);
        SeedSlot(hospital.Id, _day.AddHours(11), SlotStatus.Unavailable);
        SeedSlot(hospital.Id, _day.AddHours(12), SlotStatus.Reserved, doctor.Id);

        SeedVisit(patient, _day.Date, reservedPast.Id, withIntake: true);
        SeedVisit(patient, _day.Date, null, withIntake: false);

        SeedCase(patient, _day, reviews: 0);
        SeedCase(patient, _day, reviews: 1);
        SeedCase(patient, _day, reviews: 2);

        var summary = await GetSummaryAsync();

        Assert.Equal(5, summary.Slots.Total);
        Assert.Equal(1, summary.Slots.Available);
        Assert.Equal(1, summary.Slots.Blocked);
        Assert.Equal(2, summary.Slots.Reserved);
        Assert.Equal(1, summary.Slots.Unavailable);
        Assert.Equal(3, summary.Slots.Booked);
        Assert.Equal(75, summary.Slots.UtilizationPercent);

        Assert.Equal(2, summary.Visits.Total);
        Assert.Equal(1, summary.Visits.WithIntake);
        Assert.Equal(50, summary.Visits.IntakeCompletionPercent);

        Assert.Equal(3, summary.Evaluations.Total);
        Assert.Equal(1, summary.Evaluations.Unreviewed);
        Assert.Equal(1, summary.Evaluations.PartiallyReviewed);
        Assert.Equal(1, summary.Evaluations.Completed);
        Assert.Equal(2, summary.Evaluations.AverageHoursToFirstReview);

        // One past reserved slot has a visit, the other does not.
        Assert.Equal(1, summary.NoShowProxy);

        Assert.NotEmpty(summary.Daily);
        Assert.Contains(summary.Daily, day => day.SlotsBooked == 3);
        var hospitalRow = Assert.Single(summary.ByHospital);
        Assert.Equal(hospital.Id, hospitalRow.HospitalId);
        Assert.Equal(3, hospitalRow.Booked);
        Assert.Equal(2, hospitalRow.Visits);
        Assert.Equal(3, hospitalRow.Evaluations);
        var doctorRow = Assert.Single(summary.ByDoctor);
        Assert.Equal(doctor.Id, doctorRow.DoctorId);
        Assert.Equal(2, doctorRow.BookedSlots);
    }

    [Fact]
    public async Task Summary_RespectsCenterScope()
    {
        var hospitalA = SeedHospital("A");
        var hospitalB = SeedHospital("B");
        SeedSlot(hospitalA.Id, _day.AddHours(8), SlotStatus.Blocked);
        SeedSlot(hospitalB.Id, _day.AddHours(9), SlotStatus.Blocked);

        SetActor(new ActorScope(2, ["Center Admin"], new[] { hospitalA.Id }, new[] { 203 }));

        var summary = await GetSummaryAsync();

        Assert.Equal(1, summary.Slots.Total);
        Assert.Equal(hospitalA.Id, Assert.Single(summary.ByHospital).HospitalId);
    }

    [Fact]
    public async Task Summary_WithForeignHospitalFilter_ReturnsForbidden()
    {
        var hospitalA = SeedHospital("A");
        var hospitalB = SeedHospital("B");
        SetActor(new ActorScope(2, ["Center Admin"], new[] { hospitalA.Id }, new[] { 203 }));

        var result = await _controller.GetSummary(_day, _day.AddDays(1), hospitalB.Id);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
    }

    [Fact]
    public async Task Summary_ForNonAdmin_ReturnsForbidden()
    {
        SetActor(new ActorScope(3, ["Examiner"], [], []));

        var result = await _controller.GetSummary(_day, _day.AddDays(1));

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
    }

    [Fact]
    public async Task Summary_RejectsInvertedOrOversizedRange()
    {
        Assert.IsType<BadRequestObjectResult>(
            (await _controller.GetSummary(_day.AddDays(1), _day)).Result);
        Assert.IsType<BadRequestObjectResult>(
            (await _controller.GetSummary(_day.AddDays(-400), _day)).Result);
    }
}
