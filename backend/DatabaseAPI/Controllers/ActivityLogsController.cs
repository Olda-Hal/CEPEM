using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public class ActivityLogListResponse
    {
        public List<ActivityLog> Logs { get; set; } = new();
        public int TotalCount { get; set; }
        public bool HasMore { get; set; }
    }

    /// <summary>
    /// Query params: page, limit (default 50), employeeId, search (action/email substring),
    /// from, to (ISO datetimes)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ActivityLogListResponse>> GetLogs(
        [FromQuery] int page = 0,
        [FromQuery] int limit = 50,
        [FromQuery] int? employeeId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        if (limit < 1 || limit > 200)
        {
            limit = 50;
        }

        var query = _context.ActivityLogs.AsNoTracking().AsQueryable();

        if (employeeId.HasValue)
        {
            query = query.Where(log => log.EmployeeId == employeeId.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = search.Trim().ToLower();
            query = query.Where(log =>
                log.Action.ToLower().Contains(pattern) ||
                (log.EmployeeEmail != null && log.EmployeeEmail.ToLower().Contains(pattern)) ||
                (log.Details != null && log.Details.ToLower().Contains(pattern)));
        }
        if (from.HasValue)
        {
            query = query.Where(log => log.Timestamp >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(log => log.Timestamp <= to.Value);
        }

        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(log => log.Timestamp)
            .Skip(page * limit)
            .Take(limit + 1)
            .ToListAsync();

        var hasMore = logs.Count > limit;
        if (hasMore)
        {
            logs.RemoveAt(limit);
        }

        return Ok(new ActivityLogListResponse
        {
            Logs = logs,
            TotalCount = totalCount,
            HasMore = hasMore
        });
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
