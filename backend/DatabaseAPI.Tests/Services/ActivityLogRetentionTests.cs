using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Activity-log retention: rows past the retention cutoff are removed,
/// anything newer survives untouched.
/// </summary>
public class ActivityLogRetentionTests : IDisposable
{
    private readonly DatabaseContext _context;

    public ActivityLogRetentionTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private ActivityLog SeedLog(
        int daysOld,
        string method = "GET",
        string action = "GET /api/patients",
        int? statusCode = 200)
    {
        var log = new ActivityLog
        {
            Service = "HealthcareAPI",
            Action = action,
            Method = method,
            StatusCode = statusCode,
            Timestamp = DateTime.UtcNow.AddDays(-daysOld)
        };
        _context.ActivityLogs.Add(log);
        _context.SaveChanges();
        return log;
    }

    [Fact]
    public async Task DeleteOlderThan_RemovesOnlyRowsPastTheCutoff()
    {
        SeedLog(daysOld: 400);
        SeedLog(daysOld: 366);
        SeedLog(daysOld: 36);
        SeedLog(daysOld: 1);

        var deleted = await ActivityLogRetention.DeleteOlderThanAsync(_context, retentionDays: 365);

        Assert.Equal(2, deleted);
        var remaining = await _context.ActivityLogs
            .OrderBy(log => log.Timestamp)
            .Select(log => log.Timestamp)
            .ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.All(remaining, timestamp =>
            Assert.True(timestamp >= DateTime.UtcNow.AddDays(-365).AddMinutes(-5)));
    }

    [Fact]
    public async Task DeleteOlderThan_WithNoOldRows_IsANoOp()
    {
        SeedLog(daysOld: 1);
        SeedLog(daysOld: 10);

        var deleted = await ActivityLogRetention.DeleteOlderThanAsync(_context, retentionDays: 365);

        Assert.Equal(0, deleted);
        Assert.Equal(2, await _context.ActivityLogs.CountAsync());
    }

    [Fact]
    public async Task DeleteOlderThan_WithZeroRetentionDays_EmptiesTheTable()
    {
        SeedLog(daysOld: 400);
        SeedLog(daysOld: 1);

        var deleted = await ActivityLogRetention.DeleteOlderThanAsync(_context, retentionDays: 0);

        Assert.Equal(2, deleted);
        Assert.Equal(0, await _context.ActivityLogs.CountAsync());
    }

    [Fact]
    public async Task DeleteOlderThan_ExactCutoffIsKept()
    {
        // rows exactly at the cutoff are kept (strictly "< cutoff")
        SeedLog(daysOld: 365);
        var log = await _context.ActivityLogs.SingleAsync();
        log.Timestamp = DateTime.UtcNow.AddDays(-365).AddMinutes(1);
        await _context.SaveChangesAsync();

        var deleted = await ActivityLogRetention.DeleteOlderThanAsync(_context, retentionDays: 365);

        Assert.Equal(0, deleted);
    }

    private void SeedEmailLog(int daysOld)
    {
        _context.EmailLogs.Add(new EmailLog
        {
            ToAddress = "eva@example.com",
            Subject = "test",
            Kind = EmailLogKinds.PasswordReset,
            Status = EmailLogStatuses.Sent,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-daysOld)
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task EmailLogRetention_RemovesOnlyRowsPastTheCutoff()
    {
        SeedEmailLog(daysOld: 400);
        SeedEmailLog(daysOld: 366);
        SeedEmailLog(daysOld: 36);

        var deleted = await EmailLogRetention.DeleteOlderThanAsync(_context, retentionDays: 365);

        Assert.Equal(2, deleted);
        Assert.Equal(1, await _context.EmailLogs.CountAsync());
    }

    [Fact]
    public async Task DeleteExpired_KeepsImportantRowsLongerThanRoutineOnes()
    {
        var routineExpired = SeedLog(40, action: "GET /api/patients");
        var routineFresh = SeedLog(10, action: "GET /api/patients");
        var importantExpired = SeedLog(400, method: "POST", action: "POST /api/patients");
        var importantFresh = SeedLog(40, method: "POST", action: "POST /api/patients");
        var failedRead = SeedLog(40, statusCode: 500, action: "GET /api/patients");

        var deleted = await ActivityLogRetention.DeleteExpiredAsync(
            _context, routineDays: 30, importantDays: 365, batchSize: 100);

        Assert.Equal(2, deleted);
        Assert.False(await _context.ActivityLogs.AnyAsync(log => log.Id == routineExpired.Id));
        Assert.False(await _context.ActivityLogs.AnyAsync(log => log.Id == importantExpired.Id));
        Assert.True(await _context.ActivityLogs.AnyAsync(log => log.Id == routineFresh.Id));
        Assert.True(await _context.ActivityLogs.AnyAsync(log => log.Id == importantFresh.Id));
        Assert.True(await _context.ActivityLogs.AnyAsync(log => log.Id == failedRead.Id));
    }

    [Fact]
    public async Task EnforceMaxRows_DeletesOldestRoutineFirst_ThenCapsEverything()
    {
        var oldestRoutine = SeedLog(100, action: "GET /api/patients");
        var oldImportant = SeedLog(90, method: "POST", action: "POST /api/patients");
        for (var i = 0; i < 6; i++)
        {
            // Newer important rows: 6 of them, so the hard cap still has to
            // remove important rows after the one routine row is gone.
            SeedLog(80 - i, method: "POST", action: "POST /api/patients");
        }

        var deleted = await ActivityLogRetention.EnforceMaxRowsAsync(
            _context, maxRows: 5, batchSize: 2);

        Assert.Equal(3, deleted);
        Assert.Equal(5, await _context.ActivityLogs.CountAsync());
        Assert.False(await _context.ActivityLogs.AnyAsync(log => log.Id == oldestRoutine.Id));
        Assert.False(await _context.ActivityLogs.AnyAsync(log => log.Id == oldImportant.Id));
    }

    [Fact]
    public async Task EmailLog_EnforceMaxRows_KeepsNewestRows()
    {
        for (var i = 0; i < 6; i++)
        {
            SeedEmailLog(i);
        }

        var deleted = await EmailLogRetention.EnforceMaxRowsAsync(_context, maxRows: 3, batchSize: 2);

        Assert.Equal(3, deleted);
        var remaining = await _context.EmailLogs.OrderBy(log => log.CreatedAtUtc).ToListAsync();
        Assert.Equal(3, remaining.Count);
        Assert.All(remaining, log => Assert.True(log.CreatedAtUtc >= DateTime.UtcNow.AddDays(-2).AddMinutes(-5)));
    }
}
