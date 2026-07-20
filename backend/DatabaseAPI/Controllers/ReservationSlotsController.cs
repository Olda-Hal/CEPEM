using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/reservations/slots")]
public class ReservationSlotsController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly ILogger<ReservationSlotsController> _logger;
    private readonly IActorCountryContextService _actorCountryContextService;

    public ReservationSlotsController(DatabaseContext context, ILogger<ReservationSlotsController> logger, IActorCountryContextService actorCountryContextService)
    {
        _context = context;
        _logger = logger;
        _actorCountryContextService = actorCountryContextService;
    }

    [HttpGet("hospital/{hospitalId}")]
    public async Task<ActionResult<List<ReservationSlotDto>>> GetByHospital(
        int hospitalId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status)
    {
        try
        {
            IQueryable<ReservationSlot> query = _context.ReservationSlots
                .Where(slot => slot.HospitalId == hospitalId)
                .Include(slot => slot.Hospital)
                .Include(slot => slot.Doctor)
                .ThenInclude(doctor => doctor!.Person)
                .Include(slot => slot.Person)
                .Include(slot => slot.ExaminationType)
                .ThenInclude(type => type!.NameTranslation);

            if (from.HasValue)
                query = query.Where(slot => slot.StartDateTime >= from.Value);

            if (to.HasValue)
                query = query.Where(slot => slot.EndDateTime <= to.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(slot => slot.Status == status);

            var slots = await query
                .OrderBy(slot => slot.StartDateTime)
                .ToListAsync();

            return Ok(slots.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reservation slots");
            return StatusCode(500, new { Error = "Error getting reservation slots", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<List<ReservationSlotDto>>> CreateSlots([FromBody] CreateReservationSlotsRequest request)
    {
        try
        {
            var actorCountryCode = await _actorCountryContextService.GetActorCountryCodeAsync();
            if (request.HospitalId <= 0)
                return BadRequest("HospitalId is required");

            if (request.Slots == null || request.Slots.Count == 0)
                return BadRequest("At least one slot must be provided");

            var hospitalExists = await _context.Hospitals.AnyAsync(h => h.Id == request.HospitalId && h.Active == true);
            if (!hospitalExists)
                return NotFound("Hospital not found");

            var createdSlots = new List<ReservationSlot>();
            var pendingSlots = new List<(DateTime StartDateTime, DateTime EndDateTime)>();

            foreach (var slotRequest in request.Slots)
            {
                if (slotRequest.StartDateTime >= slotRequest.EndDateTime)
                    return BadRequest("Start time must be before end time");

                var conflict = await _context.ReservationSlots.AnyAsync(slot =>
                    slot.HospitalId == request.HospitalId &&
                    slot.Status != "UNAVAILABLE" &&
                    slot.StartDateTime < slotRequest.EndDateTime &&
                    slot.EndDateTime > slotRequest.StartDateTime);

                var pendingConflict = pendingSlots.Any(slot =>
                    slot.StartDateTime < slotRequest.EndDateTime &&
                    slot.EndDateTime > slotRequest.StartDateTime);

                if (conflict || pendingConflict)
                    return BadRequest("Time slot overlaps with an existing slot");

                var slot = new ReservationSlot
                {
                    HospitalId = request.HospitalId,
                    StartDateTime = slotRequest.StartDateTime,
                    EndDateTime = slotRequest.EndDateTime,
                    PublicNote = slotRequest.PublicNote,
                    InternalNote = slotRequest.InternalNote,
                    CountryCode = actorCountryCode,
                    Status = string.IsNullOrWhiteSpace(slotRequest.Status) ? "AVAILABLE" : slotRequest.Status.Trim().ToUpperInvariant()
                };

                _context.ReservationSlots.Add(slot);
                createdSlots.Add(slot);
                pendingSlots.Add((slotRequest.StartDateTime, slotRequest.EndDateTime));
            }

            await _context.SaveChangesAsync();

            return Ok(createdSlots.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reservation slots");
            return StatusCode(500, new { Error = "Error creating reservation slots", Details = ex.Message });
        }
    }

    [HttpPost("hospital/{hospitalId}/copy-day")]
    public async Task<ActionResult<List<ReservationSlotDto>>> CopyDaySlots(int hospitalId, [FromBody] CopyReservationSlotsDayRequest request)
    {
        try
        {
            var actorCountryCode = await _actorCountryContextService.GetActorCountryCodeAsync();
            var sourceDayStart = request.SourceDate.Date;
            var sourceDayEnd = sourceDayStart.AddDays(1);
            var targetDayStart = request.TargetDate.Date;
            var targetDayEnd = targetDayStart.AddDays(1);

            if (sourceDayStart == targetDayStart)
                return BadRequest("Source day and target day must be different");

            var hospitalExists = await _context.Hospitals.AnyAsync(h => h.Id == hospitalId && h.Active == true);
            if (!hospitalExists)
                return NotFound("Hospital not found");

            var sourceSlots = await _context.ReservationSlots
                .Where(slot =>
                    slot.HospitalId == hospitalId &&
                    slot.StartDateTime >= sourceDayStart &&
                    slot.StartDateTime < sourceDayEnd)
                .OrderBy(slot => slot.StartDateTime)
                .ToListAsync();

            if (sourceSlots.Count == 0)
                return NotFound("No reservation slots found for source day");

            var targetDayHasSlots = await _context.ReservationSlots.AnyAsync(slot =>
                slot.HospitalId == hospitalId &&
                slot.StartDateTime >= targetDayStart &&
                slot.StartDateTime < targetDayEnd);

            if (targetDayHasSlots)
                return BadRequest("Target day already contains reservation slots");

            var dayOffset = targetDayStart - sourceDayStart;
            var copiedSlots = new List<ReservationSlot>();

            foreach (var sourceSlot in sourceSlots)
            {
                var copiedSlot = new ReservationSlot
                {
                    HospitalId = hospitalId,
                    StartDateTime = sourceSlot.StartDateTime.Add(dayOffset),
                    EndDateTime = sourceSlot.EndDateTime.Add(dayOffset),
                    PublicNote = sourceSlot.PublicNote,
                    InternalNote = sourceSlot.InternalNote,
                    CountryCode = actorCountryCode,
                    Status = ResolveCopiedStatus(sourceSlot.Status, request.PreserveStatus)
                };

                copiedSlots.Add(copiedSlot);
            }

            _context.ReservationSlots.AddRange(copiedSlots);
            await _context.SaveChangesAsync();

            var copiedSlotIds = copiedSlots.Select(slot => slot.Id).ToList();
            var createdSlots = await _context.ReservationSlots
                .Where(slot => copiedSlotIds.Contains(slot.Id))
                .Include(slot => slot.Hospital)
                .Include(slot => slot.Doctor)
                .ThenInclude(doctor => doctor!.Person)
                .Include(slot => slot.Person)
                .Include(slot => slot.ExaminationType)
                .ThenInclude(type => type!.NameTranslation)
                .OrderBy(slot => slot.StartDateTime)
                .ToListAsync();

            return Ok(createdSlots.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying reservation slots");
            return StatusCode(500, new { Error = "Error copying reservation slots", Details = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ReservationSlotDto>> UpdateSlot(int id, [FromBody] UpdateReservationSlotRequest request)
    {
        try
        {
            var slot = await _context.ReservationSlots
                .Include(s => s.Hospital)
                .Include(s => s.Doctor)
                .ThenInclude(doctor => doctor!.Person)
                .Include(s => s.Person)
                .Include(s => s.ExaminationType)
                .ThenInclude(type => type!.NameTranslation)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (slot == null)
                return NotFound("Reservation slot not found");

            if (request.StartDateTime.HasValue)
                slot.StartDateTime = request.StartDateTime.Value;

            if (request.EndDateTime.HasValue)
                slot.EndDateTime = request.EndDateTime.Value;

            if (request.PublicNote != null)
                slot.PublicNote = string.IsNullOrWhiteSpace(request.PublicNote) ? null : request.PublicNote.Trim();

            if (request.InternalNote != null)
                slot.InternalNote = string.IsNullOrWhiteSpace(request.InternalNote) ? null : request.InternalNote.Trim();

            if (!string.IsNullOrWhiteSpace(request.Status))
                slot.Status = request.Status.Trim().ToUpperInvariant();

            slot.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(MapToDto(slot));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reservation slot");
            return StatusCode(500, new { Error = "Error updating reservation slot", Details = ex.Message });
        }
    }

    [HttpPost("{id}/release")]
    public async Task<ActionResult<ReservationSlotDto>> ReleaseSlot(int id)
    {
        try
        {
            var slot = await _context.ReservationSlots
                .Include(s => s.Hospital)
                .Include(s => s.Doctor)
                .ThenInclude(doctor => doctor!.Person)
                .Include(s => s.Person)
                .Include(s => s.ExaminationType)
                .ThenInclude(type => type!.NameTranslation)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (slot == null)
                return NotFound("Reservation slot not found");

            if (!string.Equals(slot.Status, "BLOCKED", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(slot.Status, "RESERVED", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only blocked or reserved slots can be released");
            }

            slot.DoctorId = null;
            slot.PersonId = null;
            slot.ExaminationTypeId = null;
            slot.Status = "AVAILABLE";
            slot.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(MapToDto(slot));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing reservation slot");
            return StatusCode(500, new { Error = "Error releasing reservation slot", Details = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSlot(int id)
    {
        try
        {
            var slot = await _context.ReservationSlots.FirstOrDefaultAsync(s => s.Id == id);

            if (slot == null)
                return NotFound("Reservation slot not found");

            _context.ReservationSlots.Remove(slot);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reservation slot");
            return StatusCode(500, new { Error = "Error deleting reservation slot", Details = ex.Message });
        }
    }

    [HttpPost("{id}/block")]
    public async Task<ActionResult<ReservationSlotDto>> BlockSlot(int id, [FromBody] BlockReservationSlotRequest request)
    {
        try
        {
            var slot = await _context.ReservationSlots
                .Include(s => s.Hospital)
                .Include(s => s.Doctor)
                .ThenInclude(doctor => doctor!.Person)
                .Include(s => s.Person)
                .Include(s => s.ExaminationType)
                .ThenInclude(type => type!.NameTranslation)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (slot == null)
                return NotFound("Reservation slot not found");

            if (!string.Equals(slot.Status, "AVAILABLE", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only available slots can be blocked");

            var personId = request.PersonId;

            if (request.NewPerson != null)
            {
                if (string.IsNullOrWhiteSpace(request.NewPerson.FirstName) || string.IsNullOrWhiteSpace(request.NewPerson.LastName))
                    return BadRequest("New person first name and last name are required");

                var uid = $"res-{Guid.NewGuid():N}";
                while (await _context.Persons.AnyAsync(p => p.UID == uid))
                {
                    uid = $"res-{Guid.NewGuid():N}";
                }

                var person = new Person
                {
                    FirstName = request.NewPerson.FirstName.Trim(),
                    LastName = request.NewPerson.LastName.Trim(),
                    Gender = "Unknown",
                    CountryCode = await _actorCountryContextService.GetActorCountryCodeAsync(),
                    UID = uid,
                    Active = true
                };

                _context.Persons.Add(person);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(request.NewPerson.PhoneNumber) || !string.IsNullOrWhiteSpace(request.NewPerson.Email))
                {
                    var contact = new Contact();
                    _context.Contacts.Add(contact);
                    await _context.SaveChangesAsync();

                    _context.ContactToObjects.Add(new ContactToObject
                    {
                        ContactId = contact.Id,
                        ObjectId = person.Id,
                        ObjectType = ContactObjectType.Person,
                        PersonId = person.Id
                    });

                    if (!string.IsNullOrWhiteSpace(request.NewPerson.PhoneNumber))
                    {
                        _context.ContactPhoneNumbers.Add(new ContactPhoneNumber
                        {
                            ContactId = contact.Id,
                            PhoneNumber = request.NewPerson.PhoneNumber.Trim()
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(request.NewPerson.Email))
                    {
                        _context.ContactEmails.Add(new ContactEmail
                        {
                            ContactId = contact.Id,
                            Email = request.NewPerson.Email.Trim()
                        });
                    }

                    await _context.SaveChangesAsync();
                }

                personId = person.Id;
            }

            if (!personId.HasValue)
                return BadRequest("personId is required when newPerson is not provided");

            var personExists = await _context.Persons.AnyAsync(p => p.Id == personId.Value);
            if (!personExists)
                return NotFound("Person not found");

            var typeExists = await _context.ExaminationTypes.AnyAsync(et => et.Id == request.ExaminationTypeId);
            if (!typeExists)
                return NotFound("Examination type not found");

            slot.PersonId = personId.Value;
            slot.ExaminationTypeId = request.ExaminationTypeId;
            slot.Status = "BLOCKED";
            slot.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(MapToDto(slot));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking reservation slot");
            return StatusCode(500, new { Error = "Error blocking reservation slot", Details = ex.Message });
        }
    }

    [HttpPost("{id}/confirm")]
    public async Task<ActionResult<ReservationSlotDto>> ConfirmSlot(int id, [FromBody] ConfirmReservationSlotRequest request)
    {
        try
        {
            var slot = await _context.ReservationSlots
                .Include(s => s.Hospital)
                .Include(s => s.Doctor)
                .ThenInclude(doctor => doctor!.Person)
                .Include(s => s.Person)
                .Include(s => s.ExaminationType)
                .ThenInclude(type => type!.NameTranslation)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (slot == null)
                return NotFound("Reservation slot not found");

            if (!string.Equals(slot.Status, "BLOCKED", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only blocked slots can be confirmed");

            var doctorExists = await _context.Employees.AnyAsync(e => e.Id == request.DoctorId);
            if (!doctorExists)
                return NotFound("Doctor not found");

            slot.DoctorId = request.DoctorId;
            slot.Status = "RESERVED";
            slot.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(MapToDto(slot));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming reservation slot");
            return StatusCode(500, new { Error = "Error confirming reservation slot", Details = ex.Message });
        }
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult<ReservationSlotDto>> RejectSlot(int id)
    {
        try
        {
            var slot = await _context.ReservationSlots
                .Include(s => s.Hospital)
                .Include(s => s.Doctor)
                .ThenInclude(doctor => doctor!.Person)
                .Include(s => s.Person)
                .Include(s => s.ExaminationType)
                .ThenInclude(type => type!.NameTranslation)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (slot == null)
                return NotFound("Reservation slot not found");

            if (!string.Equals(slot.Status, "BLOCKED", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only blocked slots can be rejected");

            slot.DoctorId = null;
            slot.PersonId = null;
            slot.ExaminationTypeId = null;
            slot.Status = "AVAILABLE";
            slot.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(MapToDto(slot));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting reservation slot");
            return StatusCode(500, new { Error = "Error rejecting reservation slot", Details = ex.Message });
        }
    }

    private static ReservationSlotDto MapToDto(ReservationSlot slot)
    {
        return new ReservationSlotDto
        {
            Id = slot.Id,
            HospitalId = slot.HospitalId,
            HospitalName = slot.Hospital?.Name,
            DoctorId = slot.DoctorId,
            DoctorName = !string.IsNullOrEmpty(slot.Doctor?.Person?.FirstName) && !string.IsNullOrEmpty(slot.Doctor?.Person?.LastName)
                ? $"{slot.Doctor.Person.FirstName} {slot.Doctor.Person.LastName}".Trim()
                : null,
            PersonId = slot.PersonId,
            PersonName = !string.IsNullOrEmpty(slot.Person?.FirstName) && !string.IsNullOrEmpty(slot.Person?.LastName)
                ? $"{slot.Person.FirstName} {slot.Person.LastName}".Trim()
                : null,
            ExaminationTypeId = slot.ExaminationTypeId,
            ExaminationTypeName = slot.ExaminationType?.NameTranslation?.EN,
            StartDateTime = slot.StartDateTime,
            EndDateTime = slot.EndDateTime,
            PublicNote = slot.PublicNote,
            InternalNote = slot.InternalNote,
            Status = slot.Status,
            CreatedAt = slot.CreatedAt,
            UpdatedAt = slot.UpdatedAt
        };
    }

    private static string ResolveCopiedStatus(string sourceStatus, bool preserveStatus)
    {
        if (preserveStatus)
        {
            if (string.Equals(sourceStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceStatus, "UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
            {
                return sourceStatus.ToUpperInvariant();
            }
        }

        if (string.Equals(sourceStatus, "UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
            return "UNAVAILABLE";

        return "AVAILABLE";
    }
}

public class CreateReservationSlotsRequest
{
    public int HospitalId { get; set; }
    public List<CreateReservationSlotItemRequest> Slots { get; set; } = new();
}

public class CreateReservationSlotItemRequest
{
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? PublicNote { get; set; }
    public string? InternalNote { get; set; }
    public string? Status { get; set; }
}

public class UpdateReservationSlotRequest
{
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string? PublicNote { get; set; }
    public string? InternalNote { get; set; }
    public string? Status { get; set; }
}

public class BlockReservationSlotRequest
{
    public int? PersonId { get; set; }
    public BlockReservationSlotPersonRequest? NewPerson { get; set; }
    public int ExaminationTypeId { get; set; }
}

public class BlockReservationSlotPersonRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}

public class ConfirmReservationSlotRequest
{
    public int DoctorId { get; set; }
}

public class CopyReservationSlotsDayRequest
{
    public DateTime SourceDate { get; set; }
    public DateTime TargetDate { get; set; }
    public bool PreserveStatus { get; set; }
}

public class ReservationSlotDto
{
    public int Id { get; set; }
    public int HospitalId { get; set; }
    public string? HospitalName { get; set; }
    public int? DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public int? PersonId { get; set; }
    public string? PersonName { get; set; }
    public int? ExaminationTypeId { get; set; }
    public string? ExaminationTypeName { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? PublicNote { get; set; }
    public string? InternalNote { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}