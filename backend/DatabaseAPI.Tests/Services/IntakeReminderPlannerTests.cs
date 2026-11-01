using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Reminder selection: only confirmed (Reserved) slots inside the lead window,
/// not reminded yet and without a submitted form.
/// </summary>
public class IntakeReminderPlannerTests : IDisposable
{
    private readonly DatabaseContext _context;

    public IntakeReminderPlannerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Person SeedPerson()
    {
        var person = new Person
        {
            FirstName = "Eva",
            LastName = "Kralova",
            UID = Guid.NewGuid().ToString("N")[..10],
            Gender = "F",
            CountryCode = "CZ",
            Active = true
        };
        _context.Persons.Add(person);
        _context.SaveChanges();
        return person;
    }

    private ReservationSlot SeedSlot(
        DateTime start,
        SlotStatus status = SlotStatus.Reserved,
        DateTime? remindedAt = null,
        int? personId = null)
    {
        var slot = new ReservationSlot
        {
            HospitalId = 1,
            PersonId = personId,
            StartDateTime = start,
            EndDateTime = start.AddHours(1),
            Status = status,
            CountryCode = "CZ",
            IntakeReminderSentAtUtc = remindedAt
        };
        _context.ReservationSlots.Add(slot);
        _context.SaveChanges();
        return slot;
    }

    private void SeedSubmittedVisit(ReservationSlot slot, Person person)
    {
        var patient = new Patient
        {
            PersonId = person.Id,
            BirthDate = new DateTime(1990, 1, 1),
            CountryCode = "CZ"
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();

        _context.Visits.Add(new Visit
        {
            PatientId = patient.Id,
            ReservationSlotId = slot.Id,
            VisitDate = slot.StartDateTime.Date,
            SourceEventId = 1,
            CountryCode = "CZ"
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task FindDue_ReturnsConfirmedSlotInsideLeadWindow()
    {
        var person = SeedPerson();
        var slot = SeedSlot(DateTime.UtcNow.AddDays(2), personId: person.Id);

        var due = await IntakeReminderPlanner.FindDueSlotsAsync(_context, DateTime.UtcNow, leadDays: 3);

        Assert.Equal(slot.Id, Assert.Single(due).Id);
    }

    [Fact]
    public async Task FindDue_IgnoresSlotsOutsideTheWindowOrInThePast()
    {
        var person = SeedPerson();
        SeedSlot(DateTime.UtcNow.AddDays(10), personId: person.Id);
        SeedSlot(DateTime.UtcNow.AddHours(-1), personId: person.Id);

        var due = await IntakeReminderPlanner.FindDueSlotsAsync(_context, DateTime.UtcNow, leadDays: 3);

        Assert.Empty(due);
    }

    [Fact]
    public async Task FindDue_IgnoresAlreadyRemindedSlots()
    {
        var person = SeedPerson();
        SeedSlot(DateTime.UtcNow.AddDays(2), personId: person.Id, remindedAt: DateTime.UtcNow.AddHours(-1));

        var due = await IntakeReminderPlanner.FindDueSlotsAsync(_context, DateTime.UtcNow, leadDays: 3);

        Assert.Empty(due);
    }

    [Fact]
    public async Task FindDue_IgnoresSlotsWithSubmittedForm()
    {
        var person = SeedPerson();
        var slot = SeedSlot(DateTime.UtcNow.AddDays(2), personId: person.Id);
        SeedSubmittedVisit(slot, person);

        var due = await IntakeReminderPlanner.FindDueSlotsAsync(_context, DateTime.UtcNow, leadDays: 3);

        Assert.Empty(due);
    }

    [Fact]
    public async Task FindDue_IgnoresBlockedSlots()
    {
        var person = SeedPerson();
        SeedSlot(DateTime.UtcNow.AddDays(2), status: SlotStatus.Blocked, personId: person.Id);

        var due = await IntakeReminderPlanner.FindDueSlotsAsync(_context, DateTime.UtcNow, leadDays: 3);

        Assert.Empty(due);
    }

    [Fact]
    public async Task FindDue_IgnoresSlotsWithoutPerson()
    {
        SeedSlot(DateTime.UtcNow.AddDays(2), personId: null);

        var due = await IntakeReminderPlanner.FindDueSlotsAsync(_context, DateTime.UtcNow, leadDays: 3);

        Assert.Empty(due);
    }
}
