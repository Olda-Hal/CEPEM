using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.Services;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MigrationsController : ControllerBase
{
    private readonly IMigrationService _migrationService;
    private readonly ILogger<MigrationsController> _logger;

    public MigrationsController(IMigrationService migrationService, ILogger<MigrationsController> logger)
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    /// <summary>
    /// Apply pending database migrations
    /// </summary>
    [HttpPost("apply")]
    public async Task<IActionResult> ApplyMigrations()
    {
        try
        {
            await _migrationService.ApplyMigrationsAsync();
            return Ok(new { Message = "Migrations applied successfully", Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply migrations");
            return StatusCode(500, new { Error = "Failed to apply migrations", Details = ex.Message });
        }
    }

    /// <summary>
    /// Get pending migrations
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingMigrations()
    {
        try
        {
            var pendingMigrations = await _migrationService.GetPendingMigrationsAsync();
            return Ok(new { 
                PendingMigrations = pendingMigrations,
                Count = pendingMigrations.Count(),
                Timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending migrations");
            return StatusCode(500, new { Error = "Failed to get pending migrations", Details = ex.Message });
        }
    }

    /// <summary>
    /// Get applied migrations
    /// </summary>
    [HttpGet("applied")]
    public async Task<IActionResult> GetAppliedMigrations()
    {
        try
        {
            var appliedMigrations = await _migrationService.GetAppliedMigrationsAsync();
            return Ok(new { 
                AppliedMigrations = appliedMigrations,
                Count = appliedMigrations.Count(),
                Timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get applied migrations");
            return StatusCode(500, new { Error = "Failed to get applied migrations", Details = ex.Message });
        }
    }

    /// <summary>
    /// Check database connection status
    /// </summary>
    [HttpGet("connection-status")]
    public async Task<IActionResult> GetConnectionStatus()
    {
        try
        {
            var canConnect = await _migrationService.CanConnectAsync();
            return Ok(new { 
                CanConnect = canConnect,
                Status = canConnect ? "Connected" : "Disconnected",
                Timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check database connection");
            return StatusCode(500, new { Error = "Failed to check database connection", Details = ex.Message });
        }
    }

    /// <summary>
    /// Create database from current Entity Framework models
    /// </summary>
    [HttpPost("ensure-created")]
    public async Task<IActionResult> EnsureDatabaseCreated()
    {
        try
        {
            await _migrationService.EnsureDatabaseCreatedAsync();
            return Ok(new { 
                Message = "Database structure created from Entity Framework models",
                Timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure database creation");
            return StatusCode(500, new { Error = "Failed to ensure database creation", Details = ex.Message });
        }
    }
}
