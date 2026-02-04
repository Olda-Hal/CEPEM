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
