using DatabaseAPI.Data;
using DatabaseAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActivityLogsController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public ActivityLogsController(DatabaseContext context)
        {
            _context = context;
        }

        // POST: api/ActivityLogs
        [HttpPost]
        public async Task<ActionResult<ActivityLog>> PostActivityLog(ActivityLog activityLog)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            _context.ActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetActivityLog), new { id = activityLog.Id }, activityLog);
        }

        // GET: api/ActivityLogs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ActivityLog>>> GetActivityLogs()
        {
            return await _context.ActivityLogs.ToListAsync();
        }

        // GET: api/ActivityLogs/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ActivityLog>> GetActivityLog(Guid id)
        {
            var activityLog = await _context.ActivityLogs.FindAsync(id);

            if (activityLog == null)
            {
                return NotFound();
            }

            return activityLog;
        }
    }
}
