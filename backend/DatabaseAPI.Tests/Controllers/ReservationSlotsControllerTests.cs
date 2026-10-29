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

/// <summary>
/// Risky-path coverage for reservation slots: overlap rejection (full,
/// partial and adjacent ranges), validate state transitions
/// (block/confirm/reject/release) and copy-day rules.
/// </summary>
public class ReservationSlotsControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly ReservationSlotsController _controller;

    public ReservationSlotsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new DatabaseContext(options);

        var actorCountryMock = new Mock<IActorCountryContextService>();
        actorCountryMock.Setup(service => service.GetActorCountryCodeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("CZ");

        _controller = new ReservationSlotsController(
            _context,
            NullLogger<ReservationSlotsController>.Instance,
            actorCountryMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private int SeedHospital(bool active = true)
    {
        var hospital = new Hospital { Name = "Test Hospital", Active = active };
        _context.Hospitals.Add(hospital);
        _context.SaveChanges();
        return hospital.Id;
    }

    private ReservationSlot SeedSlot(
        int hospitalId,
        DateTime start,
        DateTime end,
        SlotStatus status = SlotStatus.Available,
        int? personId = null,
        int? examinationTypeId = null)
    {
        var slot = new ReservationSlot
        {
            HospitalId = hospitalId,
            StartDateTime = start,
            EndDateTime = end,
            Status = status,
            PersonId = personId,
            ExaminationTypeId = examinationTypeId,
            CountryCode = "CZ"
        };
        _context.ReservationSlots.Add(slot);
        _context.SaveChanges();
        return slot;
    }

    private int SeedExaminationType(string name = "Mamograf")
    {
        var type = new ExaminationType { NameTranslation = new Translation { EN = name } };
        _context.ExaminationTypes.Add(type);
        _context.SaveChanges();
        return type.Id;
    }

    private readonly DateTime _base = new(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc);

    // ---- CreateSlots: overlap matrix ----

    [Fact]
    public async Task CreateSlots_WithEmptyRequest_ReturnsBadRequest()
    {
        var hospitalId = SeedHospital();

        var result = await _controller.CreateSlots(new CreateReservationSlotsRequest
        {
            HospitalId = hospitalId,
            Slots = new List<CreateReservationSlotItemRequest>()
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateSlots_WithUnknownHospital_ReturnsNotFound()
    {
        var result = await _controller.CreateSlots(new CreateReservationSlotsRequest
        {
            HospitalId = 99999,
            Slots = new List<CreateReservationSlotItemRequest>
            {
                new() { StartDateTime = _base, EndDateTime = _base.AddHours(1) }
            }
        });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateSlots_WithInvertedTimes_ReturnsBadRequest()
    {
        var hospitalId = SeedHospital();

        var result = await _controller.CreateSlots(new CreateReservationSlotsRequest
        {
            HospitalId = hospitalId,
            Slots = new List<CreateReservationSlotItemRequest>
            {
                new() { StartDateTime = _base.AddHours(1), EndDateTime = _base }
            }
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateSlots_WithValidSlots_CreatesAll()
    {
        var hospitalId = SeedHospital();

        var result = await _controller.CreateSlots(new CreateReservationSlotsRequest
        {
            HospitalId = hospitalId,
            Slots = new List<CreateReservationSlotItemRequest>
            {
                new() { StartDateTime = _base, EndDateTime = _base.AddHours(1), PublicNote = "morning" },
                new() { StartDateTime = _base.AddHours(1), EndDateTime = _base.AddHours(2) }
            }
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var created = Assert.IsType<List<ReservationSlotDto>>(ok.Value);
        Assert.Equal(2, created.Count);
        Assert.Equal(2, await _context.ReservationSlots.CountAsync());
        Assert.Equal("morning", created[0].PublicNote);
    }

    [Theory]
    [InlineData(0, 60)]   // exact same range
    [InlineData(30, 90)]  // overlaps end
    [InlineData(-30, 30)] // overlaps start
    [InlineData(-15, 75)] // contains existing slot
    [InlineData(15, 45)]  // contained within existing slot
    public async Task CreateSlots_OverlappingExistingSlot_ReturnsBadRequest(int startOffsetMinutes, int endOffsetMinutes)
    {
        var hospitalId = SeedHospital();
        SeedSlot(hospitalId, _base, _base.AddHours(1));

        var result = await _controller.CreateSlots(new CreateReservationSlotsRequest
        {
            HospitalId = hospitalId,
            Slots = new List<CreateReservationSlotItemRequest>
            {
                new()
                {
                    StartDateTime = _base.AddMinutes(startOffsetMinutes),
                    EndDateTime = _base.AddMinutes(endOffsetMinutes)
                }
            }
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("overlaps", badRequest.Value?.ToString());
    }

    [Theory]
    [InlineData(-60, 0)] // ends exactly when existing starts
    [InlineData(60, 120)] // starts exactly when existing ends
    public async Task CreateSlots_AdjacentToExistingSlot_IsAllowed(int startOffsetMinutes, int endOffsetMinutes)
    {
        var hospitalId = SeedHospital();
        SeedSlot(hospitalId, _base, _base.AddHours(1));

        var result = await _controller.CreateSlots(new CreateReservationSlotsRequest
        {
            HospitalId = hospitalId,
            Slots = new List<CreateReservationSlotItemRequest>
            {
                new()
                {
                    StartDateTime = _base.AddMinutes(startOffsetMinutes),
                    EndDateTime = _base.AddMinutes(endOffsetMinutes)
                }
            }
        });

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateSlots_OverlapInSameBatch_ReturnsBadRequest_DoesNotPersistPartially()
    {
        var hospitalId = SeedHospital();

        var result = await _controller.CreateSlots(new CreateReservationSlotsRequest
        {
            HospitalId = hospitalId,
            Slots = new List<CreateReservationSlotItemRequest>
            {
                new() { StartDateTime = _base, EndDateTime = _base.AddHours(2) },
                new() { StartDateTime = _base.AddHours(1), EndDateTime = _base.AddHours(3) } // overlaps previous one in the batch
            }
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, await _context.ReservationSlots.CountAsync());
    }

    [Fact]
    public async Task CreateSlots_OverlappingOtherHospital_IsAllowed()
    {
        var hospitalId = SeedHospital();
        var otherHospitalId = SeedHospital();
        SeedSlot(otherHospitalId, _base, _base.AddHours(1));

        var result = await _controller.CreateSlots(new CreateReservationSlotsRequest
        {
            HospitalId = hospitalId,
            Slots = new List<CreateReservationSlotItemRequest>
            {
                new() { StartDateTime = _base, EndDateTime = _base.AddHours(1) }
            }
        });

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateSlots_OverlappingUnavailableSlot_IsAllowed()
    {
        var hospitalId = SeedHospital();
        SeedSlot(hospitalId, _base, _base.AddHours(1), status: SlotStatus.Unavailable);

        var result = await _controller.CreateSlots(new CreateReservationSlotsRequest
        {
            HospitalId = hospitalId,
            Slots = new List<CreateReservationSlotItemRequest>
            {
                new() { StartDateTime = _base, EndDateTime = _base.AddHours(1) }
            }
        });

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // ---- GetByHospital ----

    [Fact]
    public async Task GetByHospital_FiltersByDateRangeAndStatus()
    {
        var hospitalId = SeedHospital();
        SeedSlot(hospitalId, _base, _base.AddHours(1));
        SeedSlot(hospitalId, _base.AddDays(2), _base.AddDays(2).AddHours(1), status: SlotStatus.Blocked);
        SeedSlot(hospitalId, _base.AddDays(10), _base.AddDays(10).AddHours(1));

        var result = await _controller.GetByHospital(
            hospitalId,
            from: _base.AddDays(1),
            to: _base.AddDays(3),
            status: "BLOCKED");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var slots = Assert.IsType<List<ReservationSlotDto>>(ok.Value);
        Assert.Single(slots);
        Assert.Equal("Blocked", slots[0].Status);
    }

    [Fact]
    public async Task GetByHospital_ReturnsSlotsOrderedByStartTime()
    {
        var hospitalId = SeedHospital();
        SeedSlot(hospitalId, _base.AddHours(4), _base.AddHours(5));
        SeedSlot(hospitalId, _base, _base.AddHours(1));
        SeedSlot(hospitalId, _base.AddHours(2), _base.AddHours(3));

        var result = await _controller.GetByHospital(hospitalId, null, null, null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var slots = Assert.IsType<List<ReservationSlotDto>>(ok.Value);
        Assert.Equal(3, slots.Count);
        Assert.True(slots[0].StartDateTime < slots[1].StartDateTime);
        Assert.True(slots[1].StartDateTime < slots[2].StartDateTime);
    }

    // ---- Block / Confirm / Reject / Release lifecycle ----

    [Fact]
    public async Task BlockSlot_OnAvailableSlot_BlocksWithPersonAndType()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1));
        var examinationTypeId = SeedExaminationType();
        var person = new Person { FirstName = "Eva", LastName = "Kralova", UID = "00000010", Gender = "F", CountryCode = "CZ" };
        _context.Persons.Add(person);
        _context.SaveChanges();

        var result = await _controller.BlockSlot(slot.Id, new BlockReservationSlotRequest
        {
            PersonId = person.Id,
            ExaminationTypeId = examinationTypeId
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ReservationSlotDto>(ok.Value);
        Assert.Equal("Blocked", dto.Status);
        Assert.Equal("Eva Kralova", dto.PersonName);
        Assert.Equal("Mamograf", dto.ExaminationTypeName);
    }

    [Fact]
    public async Task BlockSlot_OnBlockedSlot_ReturnsBadRequest()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1), status: SlotStatus.Blocked);

        var result = await _controller.BlockSlot(slot.Id, new BlockReservationSlotRequest
        {
            PersonId = 1,
            ExaminationTypeId = 1
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task BlockSlot_WithNewPerson_CreatesPersonAndContact()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1));
        var examinationTypeId = SeedExaminationType();

        var result = await _controller.BlockSlot(slot.Id, new BlockReservationSlotRequest
        {
            NewPerson = new BlockReservationSlotPersonRequest
            {
                FirstName = "Anna",
                LastName = "Dvorakova",
                Email = "anna@example.com",
                PhoneNumber = "777000111"
            },
            ExaminationTypeId = examinationTypeId
        });

        Assert.IsType<OkObjectResult>(result.Result);
        var person = await _context.Persons.SingleAsync(p => p.FirstName == "Anna");
        Assert.StartsWith("res-", person.UID);
        var email = await _context.ContactToObjects
            .Where(cto => cto.ObjectId == person.Id)
            .Join(_context.ContactEmails, cto => cto.ContactId, ce => ce.ContactId, (_, ce) => ce.Email)
            .SingleAsync();
        Assert.Equal("anna@example.com", email);
    }

    [Fact]
    public async Task BlockSlot_WithNeitherPersonIdNorNewPerson_ReturnsBadRequest()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1));
        SeedExaminationType();

        var result = await _controller.BlockSlot(slot.Id, new BlockReservationSlotRequest
        {
            ExaminationTypeId = 1
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ConfirmSlot_OnBlockedSlot_MarksReservedWithDoctor()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1), status: SlotStatus.Blocked);
        var person = new Person { FirstName = "MUDr", LastName = "Doktor", UID = "00000011", Gender = "M", CountryCode = "CZ", Active = true };
        _context.Persons.Add(person);
        await _context.SaveChangesAsync();
        var doctor = new Employee { PersonId = person.Id, PasswordHash = "x", Salt = "s" };
        _context.Employees.Add(doctor);
        await _context.SaveChangesAsync();

        var result = await _controller.ConfirmSlot(slot.Id, new ConfirmReservationSlotRequest
        {
            DoctorId = doctor.Id
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ReservationSlotDto>(ok.Value);
        Assert.Equal("Reserved", dto.Status);
        Assert.Equal(doctor.Id, dto.DoctorId);
    }

    [Fact]
    public async Task ConfirmSlot_OnAvailableSlot_ReturnsBadRequest()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1));

        var result = await _controller.ConfirmSlot(slot.Id, new ConfirmReservationSlotRequest { DoctorId = 1 });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RejectSlot_OnBlockedSlot_ReleasesItToAvailable()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1), status: SlotStatus.Blocked, personId: 1, examinationTypeId: 1);

        var result = await _controller.RejectSlot(slot.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ReservationSlotDto>(ok.Value);
        Assert.Equal("Available", dto.Status);
        Assert.Null(dto.PersonId);
        Assert.Null(dto.ExaminationTypeId);
    }

    [Fact]
    public async Task ReleaseSlot_OnAvailableSlot_ReturnsBadRequest()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1));

        var result = await _controller.ReleaseSlot(slot.Id);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReleaseSlot_OnReservedSlot_ClearsReservation()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1), status: SlotStatus.Reserved, personId: 1, examinationTypeId: 1);

        var result = await _controller.ReleaseSlot(slot.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ReservationSlotDto>(ok.Value);
        Assert.Equal("Available", dto.Status);
        Assert.Null(dto.PersonId);
        Assert.Null(dto.DoctorId);
        Assert.Null(dto.ExaminationTypeId);
    }

    // ---- CopyDay ----

    [Fact]
    public async Task CopyDay_ToSameDay_ReturnsBadRequest()
    {
        var hospitalId = SeedHospital();

        var result = await _controller.CopyDaySlots(hospitalId, new CopyReservationSlotsDayRequest
        {
            SourceDate = _base,
            TargetDate = _base
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CopyDay_FromEmptyDay_ReturnsNotFound()
    {
        var hospitalId = SeedHospital();

        var result = await _controller.CopyDaySlots(hospitalId, new CopyReservationSlotsDayRequest
        {
            SourceDate = _base,
            TargetDate = _base.AddDays(1)
        });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CopyDay_WithExistingTargetSlots_ReturnsBadRequest()
    {
        var hospitalId = SeedHospital();
        SeedSlot(hospitalId, _base.AddHours(1), _base.AddHours(2));
        SeedSlot(hospitalId, _base.AddDays(1).AddHours(3), _base.AddDays(1).AddHours(4));

        var result = await _controller.CopyDaySlots(hospitalId, new CopyReservationSlotsDayRequest
        {
            SourceDate = _base,
            TargetDate = _base.AddDays(1)
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CopyDay_CopiesSlotsWithCorrectOffset_ResetsBlockedToAvailable()
    {
        var hospitalId = SeedHospital();
        var slot1 = SeedSlot(hospitalId, _base.AddHours(1), _base.AddHours(2), status: SlotStatus.Blocked, personId: 1, examinationTypeId: 1);
        var slot2 = SeedSlot(hospitalId, _base.AddHours(3), _base.AddHours(4));

        var result = await _controller.CopyDaySlots(hospitalId, new CopyReservationSlotsDayRequest
        {
            SourceDate = _base,
            TargetDate = _base.AddDays(7)
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var copied = Assert.IsType<List<ReservationSlotDto>>(ok.Value);
        Assert.Equal(2, copied.Count);
        Assert.True(copied[0].StartDateTime > _base.AddDays(7));
        Assert.True(copied[0].StartDateTime < _base.AddDays(8));
        Assert.Equal(slot1.PublicNote ?? copied[0].PublicNote, copied[0].PublicNote);
        // Blocked/Reserved slots are always copied as Available (fresh day, no people copied)
        Assert.All(copied, dto => Assert.Equal("Available", dto.Status));
        Assert.All(copied, dto => Assert.Null(dto.PersonId));
    }

    [Fact]
    public async Task CopyDay_WithPreserveStatus_KeepsAvailableAndUnavailable()
    {
        var hospitalId = SeedHospital();
        SeedSlot(hospitalId, _base.AddHours(1), _base.AddHours(2), status: SlotStatus.Unavailable);

        var result = await _controller.CopyDaySlots(hospitalId, new CopyReservationSlotsDayRequest
        {
            SourceDate = _base,
            TargetDate = _base.AddDays(7),
            PreserveStatus = true
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var copied = Assert.IsType<List<ReservationSlotDto>>(ok.Value);
        Assert.Equal("Unavailable", copied[0].Status);
    }

    // ---- Update / Delete ----

    [Fact]
    public async Task UpdateSlot_UpdatesTimesAndNotes()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1));

        var result = await _controller.UpdateSlot(slot.Id, new UpdateReservationSlotRequest
        {
            StartDateTime = _base.AddHours(2),
            EndDateTime = _base.AddHours(3),
            PublicNote = "changed",
            Status = "UNAVAILABLE"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ReservationSlotDto>(ok.Value);
        Assert.Equal(_base.AddHours(2), dto.StartDateTime);
        Assert.Equal("changed", dto.PublicNote);
        Assert.Equal("Unavailable", dto.Status);
    }

    [Fact]
    public async Task UpdateSlot_ForMissingSlot_ReturnsNotFound()
    {
        var result = await _controller.UpdateSlot(99999, new UpdateReservationSlotRequest());

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteSlot_RemovesSlot()
    {
        var hospitalId = SeedHospital();
        var slot = SeedSlot(hospitalId, _base, _base.AddHours(1));

        var result = await _controller.DeleteSlot(slot.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.False(await _context.ReservationSlots.AnyAsync(s => s.Id == slot.Id));
    }

    [Fact]
    public async Task DeleteSlot_ForMissingSlot_ReturnsNotFound()
    {
        var result = await _controller.DeleteSlot(99999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ---- ParseSlotStatus fallback ----

    [Fact]
    public async Task CreateSlots_WithUnknownStatus_DefaultsToAvailable()
    {
        var hospitalId = SeedHospital();

        var result = await _controller.CreateSlots(new CreateReservationSlotsRequest
        {
            HospitalId = hospitalId,
            Slots = new List<CreateReservationSlotItemRequest>
            {
                new() { StartDateTime = _base, EndDateTime = _base.AddHours(1), Status = "GARBAGE" }
            }
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var created = Assert.IsType<List<ReservationSlotDto>>(ok.Value);
        Assert.Equal("Available", created[0].Status);
    }
}
