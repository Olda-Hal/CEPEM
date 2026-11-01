using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Controllers;

/// <summary>
/// Admin audit trail of outgoing e-mails. Only metadata is stored; message
/// bodies (and therefore reset/intake tokens) never appear here.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EmailLogsController : ControllerBase
{
    private readonly DatabaseContext _context;

    public EmailLogsController(DatabaseContext context)
    {
        _context = context;
    }

    public class EmailLogListResponse
    {
        public List<EmailLog> Logs { get; set; } = new();
        public int TotalCount { get; set; }
        public bool HasMore { get; set; }
    }

    /// <summary>
    /// Query params: page, limit (default 50), kind, status,
    /// search (recipient/subject/name substring), from, to (ISO datetimes)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<EmailLogListResponse>> GetLogs(
        [FromQuery] int page = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string? kind = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        if (limit < 1 || limit > 200)
        {
            limit = 50;
        }

        var query = BuildFilteredQuery(kind, status, search, from, to);
        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(log => log.CreatedAtUtc)
            .ThenByDescending(log => log.Id)
            .Skip(page * limit)
            .Take(limit + 1)
            .ToListAsync();

        var hasMore = logs.Count > limit;
        if (hasMore)
        {
            logs.RemoveAt(limit);
        }

        return Ok(new EmailLogListResponse
        {
            Logs = logs,
            TotalCount = totalCount,
            HasMore = hasMore
        });
    }

    /// <summary>Download all matching rows as CSV (same filters, no paging).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? kind = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        const int maxRows = 50_000;

        var logs = await BuildFilteredQuery(kind, status, search, from, to)
            .OrderByDescending(log => log.CreatedAtUtc)
            .ThenByDescending(log => log.Id)
            .Take(maxRows)
            .ToListAsync();

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("CreatedAt;To;Recipient;Subject;Kind;Status;Slot;Error");
        foreach (var log in logs)
        {
            builder.AppendLine(string.Join(';',
                log.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                CsvEscape(log.ToAddress),
                CsvEscape(log.RecipientName ?? string.Empty),
                CsvEscape(log.Subject),
                CsvEscape(log.Kind),
                CsvEscape(log.Status),
                log.ReservationSlotId?.ToString() ?? string.Empty,
                CsvEscape(log.Error ?? string.Empty)));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(builder.ToString());
        return File(bytes, "text/csv; charset=utf-8", $"email-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private IQueryable<EmailLog> BuildFilteredQuery(
        string? kind,
        string? status,
        string? search,
        DateTime? from,
        DateTime? to)
    {
        var query = _context.EmailLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(kind))
        {
            query = query.Where(log => log.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(log => log.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(log =>
                log.ToAddress.Contains(term) ||
                log.Subject.Contains(term) ||
                (log.RecipientName != null && log.RecipientName.Contains(term)));
        }

        if (from.HasValue)
        {
            query = query.Where(log => log.CreatedAtUtc >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(log => log.CreatedAtUtc <= to.Value);
        }

        return query;
    }

    private static string CsvEscape(string value) =>
        value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
