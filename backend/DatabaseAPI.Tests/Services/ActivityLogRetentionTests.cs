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

    private void SeedLog(int daysOld)
    {
        _context.ActivityLogs.Add(new ActivityLog
        {
            Service = "HealthcareAPI",
            Action = "GET /api/patients",
            Timestamp = DateTime.UtcNow.AddDays(-daysOld)
        });
        _context.SaveChanges();
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
}
