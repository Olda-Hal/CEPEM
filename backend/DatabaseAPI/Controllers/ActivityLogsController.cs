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
    /// method, statusCode, from, to (ISO datetimes)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ActivityLogListResponse>> GetLogs(
        [FromQuery] int page = 0,
        [FromQuery] int limit = 50,
        [FromQuery] int? employeeId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? method = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        if (limit < 1 || limit > 200)
        {
            limit = 50;
        }

        var query = BuildFilteredQuery(employeeId, search, method, statusCode, from, to);

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

    /// <summary>
    /// Download all matching entries as CSV (same filters as the list view,
    /// no paging). Row cap protects the service from runaway exports.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] int? employeeId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? method = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        const int maxRows = 50_000;

        var logs = await BuildFilteredQuery(employeeId, search, method, statusCode, from, to)
            .OrderByDescending(log => log.Timestamp)
            .Take(maxRows)
            .ToListAsync();

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Timestamp;Service;Employee;Method;Action;Status;Details");
        foreach (var log in logs)
        {
            builder.AppendLine(string.Join(';',
                log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                CsvEscape(log.Service),
                CsvEscape(log.EmployeeEmail ?? string.Empty),
                CsvEscape(log.Method ?? string.Empty),
                CsvEscape(log.Action),
                log.StatusCode?.ToString() ?? string.Empty,
                CsvEscape(log.Details ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ')));
        }

        // UTF-8 with BOM so Excel parses non-ASCII (Czech) correctly
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString()))
            .ToArray();

        return File(bytes, "text/csv", $"activity-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private IQueryable<ActivityLog> BuildFilteredQuery(
        int? employeeId,
        string? search,
        string? method,
        int? statusCode,
        DateTime? from,
        DateTime? to)
    {
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
        if (!string.IsNullOrWhiteSpace(method))
        {
            var normalizedMethod = method.Trim().ToUpper();
            query = query.Where(log => log.Method != null && log.Method.ToUpper() == normalizedMethod);
        }
        if (statusCode.HasValue)
        {
            query = query.Where(log => log.StatusCode == statusCode.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(log => log.Timestamp >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(log => log.Timestamp <= to.Value);
        }

        return query;
    }

    private static string CsvEscape(string value)
    {
        return value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
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
