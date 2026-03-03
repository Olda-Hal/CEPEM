using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorExaminationRoomsController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly ILogger<DoctorExaminationRoomsController> _logger;

    public DoctorExaminationRoomsController(DatabaseContext context, ILogger<DoctorExaminationRoomsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("doctor/{doctorId}")]
    public async Task<ActionResult<List<DoctorExaminationRoomDto>>> GetByDoctor(int doctorId)
    {
        try
        {
            var assignments = await _context.DoctorExaminationRooms
                .Where(der => der.DoctorId == doctorId)
                .Include(der => der.ExaminationRoom)
                .Select(der => new DoctorExaminationRoomDto
                {
                    Id = der.Id,
                    DoctorId = der.DoctorId,
                    ExaminationRoomId = der.ExaminationRoomId,
                    RoomName = der.ExaminationRoom!.Name,
                    HospitalId = der.ExaminationRoom.HospitalId,
                    AssignedAt = der.AssignedAt
                })
                .OrderBy(der => der.RoomName)
                .ToListAsync();

            return Ok(assignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting doctor examination rooms");
            return StatusCode(500, new { Error = "Error getting doctor examination rooms", Details = ex.Message });
        }
    }

    [HttpGet("doctor/{doctorId}/hospitals")]
    public async Task<ActionResult<List<DoctorHospitalDto>>> GetDoctorHospitals(int doctorId)
    {
        try
        {
            var hospitals = await _context.DoctorExaminationRooms
                .Where(der => der.DoctorId == doctorId)
                .Include(der => der.ExaminationRoom)
                .ThenInclude(er => er.Hospital)
                .ThenInclude(h => h.Address)
                .Select(der => der.ExaminationRoom.Hospital)
                .Distinct()
                .Select(h => new DoctorHospitalDto
                {
                    Id = h.Id,
                    Name = h.Name ?? ("Hospital " + h.Id),
                    Address = h.Address != null ? (h.Address.Street + ", " + h.Address.City) : null
                })
                .OrderBy(h => h.Name)
                .ToListAsync();

            return Ok(hospitals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting doctor hospitals");
            return StatusCode(500, new { Error = "Error getting doctor hospitals", Details = ex.Message });
        }
    }

    [HttpGet("doctor/{doctorId}/rooms")]
    public async Task<ActionResult<List<DoctorRoomDto>>> GetDoctorRoomsByHospital(int doctorId, [FromQuery] int? hospitalId)
    {
        try
        {
            IQueryable<DoctorExaminationRoom> query = _context.DoctorExaminationRooms
                .Where(der => der.DoctorId == doctorId)
                .Include(der => der.ExaminationRoom);

            if (hospitalId.HasValue)
            {
                query = query.Where(der => der.ExaminationRoom.HospitalId == hospitalId.Value);
            }

            var rooms = await query
                .Select(der => new DoctorRoomDto
                {
                    Id = der.ExaminationRoomId,
                    Name = der.ExaminationRoom.Name,
                    Description = der.ExaminationRoom.Description,
                    HospitalId = der.ExaminationRoom.HospitalId
                })
                .OrderBy(r => r.Name)
                .ToListAsync();

            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting doctor rooms");
            return StatusCode(500, new { Error = "Error getting doctor rooms", Details = ex.Message });
        }
    }

    [HttpGet("room/{roomId}/doctors")]
    public async Task<ActionResult<List<RoomDoctorDto>>> GetDoctorsByRoom(int roomId)
    {
        try
        {
            var doctors = await _context.DoctorExaminationRooms
                .Where(der => der.ExaminationRoomId == roomId)
                .Include(der => der.Doctor)
                .ThenInclude(d => d.Person)
                .Select(der => new RoomDoctorDto
                {
                    Id = der.DoctorId,
                    FullName = (der.Doctor.Person.FirstName ?? "") + " " + (der.Doctor.Person.LastName ?? "")
                })
                .Distinct()
                .OrderBy(d => d.FullName)
                .ToListAsync();

            foreach (var doctor in doctors)
            {
                doctor.FullName = doctor.FullName.Trim();
            }

            return Ok(doctors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room doctors");
            return StatusCode(500, new { Error = "Error getting room doctors", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<DoctorExaminationRoom>> AssignRoom([FromBody] AssignExaminationRoomRequest request)
    {
        try
        {
            var doctorExists = await _context.Employees.AnyAsync(e => e.Id == request.DoctorId);
            if (!doctorExists)
                return NotFound("Doctor not found");

            var roomExists = await _context.ExaminationRooms.AnyAsync(r => r.Id == request.ExaminationRoomId && r.IsActive);
            if (!roomExists)
                return NotFound("Examination room not found");

            var existing = await _context.DoctorExaminationRooms
                .FirstOrDefaultAsync(der => der.DoctorId == request.DoctorId && der.ExaminationRoomId == request.ExaminationRoomId);

            if (existing != null)
                return BadRequest("Doctor is already assigned to this room");

            var assignment = new DoctorExaminationRoom
            {
                DoctorId = request.DoctorId,
                ExaminationRoomId = request.ExaminationRoomId
            };

            _context.DoctorExaminationRooms.Add(assignment);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetByDoctor), new { doctorId = request.DoctorId }, assignment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning examination room");
            return StatusCode(500, new { Error = "Error assigning examination room", Details = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveAssignment(int id)
    {
        try
        {
            var assignment = await _context.DoctorExaminationRooms.FindAsync(id);
            if (assignment == null)
                return NotFound("Assignment not found");

            _context.DoctorExaminationRooms.Remove(assignment);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Assignment removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing assignment");
            return StatusCode(500, new { Error = "Error removing assignment", Details = ex.Message });
        }
    }
}

public class AssignExaminationRoomRequest
{
    public int DoctorId { get; set; }
    public int ExaminationRoomId { get; set; }
}

public class DoctorExaminationRoomDto
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int ExaminationRoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public int HospitalId { get; set; }
    public DateTime AssignedAt { get; set; }
}

public class DoctorHospitalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
}

public class DoctorRoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int HospitalId { get; set; }
}

public class RoomDoctorDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
}
