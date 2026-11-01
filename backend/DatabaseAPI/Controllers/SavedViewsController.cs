using System.Text.Json;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

/// <summary>
/// Per-employee saved filter views. The gateway only requires an authenticated
/// user; every operation is scoped to the employee from the actor headers.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SavedViewsController : ControllerBase
{
    private static readonly HashSet<string> AllowedSurfaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "patients",
        "reservations",
        "employees"
    };

    private readonly DatabaseContext _context;
    private readonly IActorCountryContextService _actorCountryContextService;

    public SavedViewsController(
        DatabaseContext context,
        IActorCountryContextService actorCountryContextService)
    {
        _context = context;
        _actorCountryContextService = actorCountryContextService;
    }

    public class SavedViewDto
    {
        public int Id { get; set; }
        public string Surface { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FiltersJson { get; set; } = "{}";
        public bool IsDefault { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public class SaveSavedViewRequest
    {
        public string Surface { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FiltersJson { get; set; } = "{}";
        public bool IsDefault { get; set; }
    }

    public class UpdateSavedViewRequest
    {
        public string Name { get; set; } = string.Empty;
        public string FiltersJson { get; set; } = "{}";
        public bool IsDefault { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<List<SavedViewDto>>> GetAll([FromQuery] string surface)
    {
        var employeeId = _actorCountryContextService.GetActorEmployeeId();
        if (employeeId == null)
        {
            return Unauthorized();
        }

        var normalizedSurface = NormalizeSurface(surface);
        if (normalizedSurface == null)
        {
            return BadRequest("Unknown saved-view surface");
        }

        var views = await _context.SavedViews
            .AsNoTracking()
            .Where(view => view.EmployeeId == employeeId.Value && view.Surface == normalizedSurface)
            .OrderByDescending(view => view.IsDefault)
            .ThenBy(view => view.Name)
            .ToListAsync();

        return Ok(views.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SavedViewDto>> Create([FromBody] SaveSavedViewRequest request)
    {
        var employeeId = _actorCountryContextService.GetActorEmployeeId();
        if (employeeId == null)
        {
            return Unauthorized();
        }

        var surface = NormalizeSurface(request.Surface);
        if (surface == null)
        {
            return BadRequest("Unknown saved-view surface");
        }

        var validation = Validate(request.Name, request.FiltersJson);
        if (validation != null)
        {
            return BadRequest(validation);
        }

        var name = request.Name.Trim();
        var duplicate = await _context.SavedViews.AnyAsync(view =>
            view.EmployeeId == employeeId.Value && view.Surface == surface && view.Name == name);
        if (duplicate)
        {
            return Conflict("A saved view with this name already exists");
        }

        if (request.IsDefault)
        {
            await ClearDefaultsAsync(employeeId.Value, surface);
        }

        var created = new SavedView
        {
            EmployeeId = employeeId.Value,
            Surface = surface,
            Name = name,
            FiltersJson = request.FiltersJson,
            IsDefault = request.IsDefault
        };
        _context.SavedViews.Add(created);
        await _context.SaveChangesAsync();

        return Ok(ToDto(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SavedViewDto>> Update(int id, [FromBody] UpdateSavedViewRequest request)
    {
        var employeeId = _actorCountryContextService.GetActorEmployeeId();
        if (employeeId == null)
        {
            return Unauthorized();
        }

        var view = await _context.SavedViews
            .FirstOrDefaultAsync(entry => entry.Id == id && entry.EmployeeId == employeeId.Value);
        if (view == null)
        {
            return NotFound("Saved view not found");
        }

        var validation = Validate(request.Name, request.FiltersJson);
        if (validation != null)
        {
            return BadRequest(validation);
        }

        var name = request.Name.Trim();
        var duplicate = await _context.SavedViews.AnyAsync(entry =>
            entry.EmployeeId == employeeId.Value &&
            entry.Surface == view.Surface &&
            entry.Name == name &&
            entry.Id != view.Id);
        if (duplicate)
        {
            return Conflict("A saved view with this name already exists");
        }

        if (request.IsDefault && !view.IsDefault)
        {
            await ClearDefaultsAsync(employeeId.Value, view.Surface);
        }

        view.Name = name;
        view.FiltersJson = request.FiltersJson;
        view.IsDefault = request.IsDefault;
        view.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ToDto(view));
    }

    [HttpPost("{id:int}/default")]
    public async Task<IActionResult> SetDefault(int id)
    {
        var employeeId = _actorCountryContextService.GetActorEmployeeId();
        if (employeeId == null)
        {
            return Unauthorized();
        }

        var view = await _context.SavedViews
            .FirstOrDefaultAsync(entry => entry.Id == id && entry.EmployeeId == employeeId.Value);
        if (view == null)
        {
            return NotFound("Saved view not found");
        }

        await ClearDefaultsAsync(employeeId.Value, view.Surface);
        view.IsDefault = true;
        view.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var employeeId = _actorCountryContextService.GetActorEmployeeId();
        if (employeeId == null)
        {
            return Unauthorized();
        }

        var view = await _context.SavedViews
            .FirstOrDefaultAsync(entry => entry.Id == id && entry.EmployeeId == employeeId.Value);
        if (view == null)
        {
            return NotFound("Saved view not found");
        }

        _context.SavedViews.Remove(view);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task ClearDefaultsAsync(int employeeId, string surface)
    {
        var defaults = await _context.SavedViews
            .Where(view => view.EmployeeId == employeeId && view.Surface == surface && view.IsDefault)
            .ToListAsync();
        foreach (var view in defaults)
        {
            view.IsDefault = false;
            view.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private static string? NormalizeSurface(string? surface)
    {
        if (string.IsNullOrWhiteSpace(surface))
        {
            return null;
        }
        return AllowedSurfaces.TryGetValue(surface.Trim(), out var normalized) ? normalized : null;
    }

    private static string? Validate(string? name, string? filtersJson)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required";
        }
        if (name.Trim().Length > 100)
        {
            return "Name must be at most 100 characters";
        }
        if (string.IsNullOrWhiteSpace(filtersJson) || filtersJson.Length > 4000)
        {
            return "Filters must be a JSON object up to 4000 characters";
        }

        try
        {
            using var document = JsonDocument.Parse(filtersJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return "Filters must be a JSON object";
            }
        }
        catch (JsonException)
        {
            return "Filters must be valid JSON";
        }

        return null;
    }

    private static SavedViewDto ToDto(SavedView view) => new()
    {
        Id = view.Id,
        Surface = view.Surface,
        Name = view.Name,
        FiltersJson = view.FiltersJson,
        IsDefault = view.IsDefault,
        UpdatedAtUtc = view.UpdatedAtUtc
    };
}
