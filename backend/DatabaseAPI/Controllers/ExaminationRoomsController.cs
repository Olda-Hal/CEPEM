using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExaminationRoomsController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly ILogger<ExaminationRoomsController> _logger;

    public ExaminationRoomsController(DatabaseContext context, ILogger<ExaminationRoomsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("{hospitalId}")]
    public async Task<ActionResult<List<ExaminationRoom>>> GetByHospital(int hospitalId)
    {
        try
        {
            var rooms = await _context.ExaminationRooms
                .Where(r => r.HospitalId == hospitalId && r.IsActive)
                .OrderBy(r => r.Name)
                .ToListAsync();

            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting examination rooms");
            return StatusCode(500, new { Error = "Error getting examination rooms", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ExaminationRoom>> CreateRoom([FromBody] CreateExaminationRoomRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Room name is required");

            var room = new ExaminationRoom
            {
                Name = request.Name,
                Description = request.Description,
                HospitalId = request.HospitalId,
                IsActive = true
            };

            _context.ExaminationRooms.Add(room);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetByHospital), new { hospitalId = room.HospitalId }, room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating examination room");
            return StatusCode(500, new { Error = "Error creating examination room", Details = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRoom(int id, [FromBody] UpdateExaminationRoomRequest request)
    {
        try
        {
            var room = await _context.ExaminationRooms.FindAsync(id);
            if (room == null)
                return NotFound("Examination room not found");

            room.Name = request.Name ?? room.Name;
            room.Description = request.Description ?? room.Description;
            room.IsActive = request.IsActive ?? room.IsActive;
            room.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating examination room");
            return StatusCode(500, new { Error = "Error updating examination room", Details = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        try
        {
            var room = await _context.ExaminationRooms.FindAsync(id);
            if (room == null)
                return NotFound("Examination room not found");

            room.IsActive = false;
            room.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Room deactivated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting examination room");
            return StatusCode(500, new { Error = "Error deleting examination room", Details = ex.Message });
        }
    }
}

public class CreateExaminationRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int HospitalId { get; set; }
}

public class UpdateExaminationRoomRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}
