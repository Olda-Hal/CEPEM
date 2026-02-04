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
                .Include(r => r.Patient)
                .ThenInclude(p => p!.Person)
                .Include(r => r.ExaminationType);

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
                .Include(r => r.Patient)
                .ThenInclude(p => p!.Person)
                .Include(r => r.ExaminationType);

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

            var patientExists = await _context.Patients.AnyAsync(p => p.Id == request.PatientId);
            if (!patientExists)
                return NotFound("Patient not found");

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
                PatientId = request.PatientId,
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
            PatientId = r.PatientId,
            PatientName = !string.IsNullOrEmpty(r.Patient?.Person?.FirstName) && !string.IsNullOrEmpty(r.Patient?.Person?.LastName) 
                ? $"{r.Patient.Person.FirstName} {r.Patient.Person.LastName}".Trim() 
                : null,
            ExaminationRoomId = r.ExaminationRoomId,
            RoomName = r.ExaminationRoom?.Name,
            ExaminationTypeId = r.ExaminationTypeId,
            ExaminationTypeName = r.ExaminationType?.Name,
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
    public int PatientId { get; set; }
    public int ExaminationRoomId { get; set; }
    public int ExaminationTypeId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? Notes { get; set; }
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
    public int PatientId { get; set; }
    public string? PatientName { get; set; }
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
