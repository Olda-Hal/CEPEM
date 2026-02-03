using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActivityLogsController : ControllerBase
{
    private readonly DatabaseContext _context;

    public ActivityLogsController(DatabaseContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult> LogActivity([FromBody] ActivityLog activityLog)
    {
        if (activityLog == null)
        {
            return BadRequest("Activity log cannot be null");
        }

        activityLog.Timestamp = DateTime.UtcNow;
        
        _context.ActivityLogs.Add(activityLog);
        await _context.SaveChangesAsync();

        return Ok();
    }
}
