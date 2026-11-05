using System.Text.Json;
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

public class IntakeChangesControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly Mock<IActorCountryContextService> _actor = new();
    private readonly IntakeChangesController _controller;

    public IntakeChangesControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new DatabaseContext(options);
        _actor.Setup(service => service.GetActorEmployeeId()).Returns(5);

        _controller = new IntakeChangesController(
            _context,
            new PatientMasterDataService(_context),
            _actor.Object,
            NullLogger<IntakeChangesController>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Patient SeedPatientWithPendingChange(out IntakeChangeRequest change)
    {
        var person = new Person
        {
            FirstName = "Eva",
            LastName = "Kralova",
            UID = "1",
            Gender = "F",
            CountryCode = "CZ",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Persons.Add(person);
        _context.SaveChanges();

        var patient = new Patient
        {
            PersonId = person.Id,
            BirthDate = new DateTime(1990, 1, 1),
            InsuranceNumber = 1,
            CountryCode = "CZ"
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();

        change = new IntakeChangeRequest
        {
            PatientId = patient.Id,
            Status = IntakeChangeStatuses.Pending,
            ChangesJson = JsonSerializer.Serialize(new CreateIntakeFormEventRequest
            {
                FirstName = "Evicka",
                Email = "new@example.com",
                GdprAccepted = true
            }),
            Summary = "Jméno: Evicka",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.IntakeChangeRequests.Add(change);
        _context.SaveChanges();
        return patient;
    }

    [Fact]
    public async Task Approve_AppliesMasterDataAndMarksReviewed()
    {
        var patient = SeedPatientWithPendingChange(out var change);

        var result = await _controller.Approve(patient.Id, change.Id);

        var dto = Assert.IsType<IntakeChangeRequestDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(IntakeChangeStatuses.Approved, dto.Status);
        Assert.Equal(5, dto.ReviewedByEmployeeId);

        var person = await _context.Persons.SingleAsync(entry => entry.Id == patient.PersonId);
        Assert.Equal("Evicka", person.FirstName);
    }

    [Fact]
    public async Task Reject_KeepsPatientCardUnchanged()
    {
        var patient = SeedPatientWithPendingChange(out var change);

        var result = await _controller.Reject(patient.Id, change.Id);

        var dto = Assert.IsType<IntakeChangeRequestDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(IntakeChangeStatuses.Rejected, dto.Status);

        var person = await _context.Persons.SingleAsync(entry => entry.Id == patient.PersonId);
        Assert.Equal("Eva", person.FirstName);
    }

    [Fact]
    public async Task Approve_AlreadyReviewed_ReturnsConflict()
    {
        var patient = SeedPatientWithPendingChange(out var change);
        change.Status = IntakeChangeStatuses.Rejected;
        await _context.SaveChangesAsync();

        var result = await _controller.Approve(patient.Id, change.Id);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }
}
