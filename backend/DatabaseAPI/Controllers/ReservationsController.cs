using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsController(DatabaseContext context, ILogger<ReservationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("doctor/{doctorId}")]
    public async Task<ActionResult<List<ReservationDto>>> GetDoctorReservations(
        int doctorId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            IQueryable<Reservation> query = _context.Reservations
                .Where(r => r.DoctorId == doctorId)
                .Include(r => r.ExaminationRoom)
                .Include(r => r.Person)
                .Include(r => r.ExaminationType)
                .ThenInclude(et => et!.NameTranslation);

            if (from.HasValue)
                query = query.Where(r => r.StartDateTime >= from.Value);

            if (to.HasValue)
                query = query.Where(r => r.EndDateTime <= to.Value);

            var reservations = await query
                .OrderBy(r => r.StartDateTime)
                .Select(r => MapToDto(r))
                .ToListAsync();

            return Ok(reservations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting doctor reservations");
            return StatusCode(500, new { Error = "Error getting reservations", Details = ex.Message });
        }
    }

    [HttpGet("room/{roomId}")]
    public async Task<ActionResult<List<ReservationDto>>> GetRoomReservations(
        int roomId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            IQueryable<Reservation> query = _context.Reservations
                .Where(r => r.ExaminationRoomId == roomId)
                .Include(r => r.Doctor)
                .ThenInclude(d => d!.Person)
                .Include(r => r.Person)
                .Include(r => r.ExaminationType)
                .ThenInclude(et => et!.NameTranslation);

            if (from.HasValue)
                query = query.Where(r => r.StartDateTime >= from.Value);

            if (to.HasValue)
                query = query.Where(r => r.EndDateTime <= to.Value);

            var reservations = await query
                .OrderBy(r => r.StartDateTime)
                .Select(r => MapToDto(r))
                .ToListAsync();

            return Ok(reservations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room reservations");
            return StatusCode(500, new { Error = "Error getting reservations", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> CreateReservation([FromBody] CreateReservationRequest request)
    {
        try
        {
            if (request.StartDateTime >= request.EndDateTime)
                return BadRequest("Start time must be before end time");

            var doctorExists = await _context.Employees.AnyAsync(e => e.Id == request.DoctorId);
            if (!doctorExists)
                return NotFound("Doctor not found");

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

            var roomExists = await _context.ExaminationRooms
                .AnyAsync(r => r.Id == request.ExaminationRoomId && r.IsActive);
            if (!roomExists)
                return NotFound("Examination room not found");

            var typeExists = await _context.ExaminationTypes.AnyAsync(et => et.Id == request.ExaminationTypeId);
            if (!typeExists)
                return NotFound("Examination type not found");

            var doctorAssigned = await _context.DoctorExaminationRooms
                .AnyAsync(der => der.DoctorId == request.DoctorId && der.ExaminationRoomId == request.ExaminationRoomId);
            if (!doctorAssigned)
                return BadRequest("Doctor is not assigned to this examination room");

            var conflict = await _context.Reservations
                .AnyAsync(r => r.ExaminationRoomId == request.ExaminationRoomId &&
                    r.Status != "Cancelled" &&
                    r.StartDateTime < request.EndDateTime &&
                    r.EndDateTime > request.StartDateTime);

            if (conflict)
                return BadRequest("Time slot is already booked");

            var reservation = new Reservation
            {
                DoctorId = request.DoctorId,
                PersonId = personId.Value,
                ExaminationRoomId = request.ExaminationRoomId,
                ExaminationTypeId = request.ExaminationTypeId,
                StartDateTime = request.StartDateTime,
                EndDateTime = request.EndDateTime,
                Notes = request.Notes,
                Status = "Scheduled"
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDoctorReservations), new { doctorId = request.DoctorId }, MapToDto(reservation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reservation");
            return StatusCode(500, new { Error = "Error creating reservation", Details = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateReservation(int id, [FromBody] UpdateReservationRequest request)
    {
        try
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
                return NotFound("Reservation not found");

            if (request.StartDateTime.HasValue && request.EndDateTime.HasValue)
            {
                if (request.StartDateTime >= request.EndDateTime)
                    return BadRequest("Start time must be before end time");

                var conflict = await _context.Reservations
                    .AnyAsync(r => r.Id != id &&
                        r.ExaminationRoomId == reservation.ExaminationRoomId &&
                        r.Status != "Cancelled" &&
                        r.StartDateTime < request.EndDateTime &&
                        r.EndDateTime > request.StartDateTime);

                if (conflict)
                    return BadRequest("Time slot is already booked");

                reservation.StartDateTime = request.StartDateTime.Value;
                reservation.EndDateTime = request.EndDateTime.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.Notes))
                reservation.Notes = request.Notes;

            if (!string.IsNullOrWhiteSpace(request.Status))
                reservation.Status = request.Status;

            reservation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(MapToDto(reservation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reservation");
            return StatusCode(500, new { Error = "Error updating reservation", Details = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReservation(int id)
    {
        try
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
                return NotFound("Reservation not found");

            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Reservation deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reservation");
            return StatusCode(500, new { Error = "Error deleting reservation", Details = ex.Message });
        }
    }

    private static ReservationDto MapToDto(Reservation r)
    {
        return new ReservationDto
        {
            Id = r.Id,
            DoctorId = r.DoctorId,
            DoctorName = !string.IsNullOrEmpty(r.Doctor?.Person?.FirstName) && !string.IsNullOrEmpty(r.Doctor?.Person?.LastName) 
                ? $"{r.Doctor.Person.FirstName} {r.Doctor.Person.LastName}".Trim() 
                : null,
            PersonId = r.PersonId,
            PersonName = !string.IsNullOrEmpty(r.Person?.FirstName) && !string.IsNullOrEmpty(r.Person?.LastName)
                ? $"{r.Person.FirstName} {r.Person.LastName}".Trim()
                : null,
            ExaminationRoomId = r.ExaminationRoomId,
            RoomName = r.ExaminationRoom?.Name,
            ExaminationTypeId = r.ExaminationTypeId,
            ExaminationTypeName = r.ExaminationType?.NameTranslation?.EN,
            StartDateTime = r.StartDateTime,
            EndDateTime = r.EndDateTime,
            Notes = r.Notes,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };
    }
}

public class CreateReservationRequest
{
    public int DoctorId { get; set; }
    public int? PersonId { get; set; }
    public CreateReservationPersonRequest? NewPerson { get; set; }
    public int ExaminationRoomId { get; set; }
    public int ExaminationTypeId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? Notes { get; set; }
}

public class CreateReservationPersonRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}

public class UpdateReservationRequest
{
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string? Notes { get; set; }
    public string? Status { get; set; }
}

public class ReservationDto
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public int PersonId { get; set; }
    public string? PersonName { get; set; }
    public int ExaminationRoomId { get; set; }
    public string? RoomName { get; set; }
    public int ExaminationTypeId { get; set; }
    public string? ExaminationTypeName { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Scheduled";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
