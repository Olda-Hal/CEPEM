using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

/// <summary>
/// Admin browser for editable e-mail/SMS templates. Scope resolution is
/// Center &gt; Country &gt; Global; ScopeId is 0 for Global, a country scope id
/// for Country and a hospital id for Center.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CommunicationTemplatesController : ControllerBase
{
    private static readonly HashSet<string> Channels = new(StringComparer.OrdinalIgnoreCase)
    {
        CommunicationChannels.Email,
        CommunicationChannels.Sms
    };

    private static readonly HashSet<string> ScopeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        CommunicationScopes.Global,
        CommunicationScopes.Country,
        CommunicationScopes.Center
    };

    private readonly DatabaseContext _context;

    public CommunicationTemplatesController(DatabaseContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CommunicationTemplateDto>>> GetAll(
        [FromQuery] string? channel = null,
        [FromQuery] string? code = null,
        [FromQuery] string? language = null,
        [FromQuery] string? scopeType = null,
        [FromQuery] int? scopeId = null,
        [FromQuery] string? search = null)
    {
        var query = _context.CommunicationTemplates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(channel))
        {
            var normalized = channel.Trim();
            query = query.Where(template => template.Channel == normalized);
        }
        if (!string.IsNullOrWhiteSpace(code))
        {
            var normalized = code.Trim();
            query = query.Where(template => template.Code == normalized);
        }
        if (!string.IsNullOrWhiteSpace(language))
        {
            var normalized = language.Trim().ToLowerInvariant();
            query = query.Where(template => template.Language == normalized);
        }
        if (!string.IsNullOrWhiteSpace(scopeType))
        {
            var normalized = scopeType.Trim();
            query = query.Where(template => template.ScopeType == normalized);
        }
        if (scopeId.HasValue)
        {
            query = query.Where(template => template.ScopeId == scopeId.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(template =>
                template.Code.Contains(term) ||
                (template.Subject != null && template.Subject.Contains(term)) ||
                template.Body.Contains(term));
        }

        var templates = await query
            .OrderBy(template => template.Code)
            .ThenBy(template => template.Channel)
            .ThenBy(template => template.Language)
            .ThenBy(template => template.ScopeType)
            .ToListAsync();

        var hospitalIds = templates
            .Where(template => template.ScopeType == CommunicationScopes.Center)
            .Select(template => template.ScopeId)
            .Distinct()
            .ToList();
        var hospitalNames = hospitalIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Hospitals
                .Where(hospital => hospitalIds.Contains(hospital.Id))
                .ToDictionaryAsync(hospital => hospital.Id, hospital => hospital.Name ?? $"#{hospital.Id}");

        return Ok(templates.Select(template => ToDto(template, hospitalNames)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CommunicationTemplateDto>> Create(
        [FromBody] SaveCommunicationTemplateRequest request)
    {
        var validation = Validate(request);
        if (validation != null)
        {
            return BadRequest(validation);
        }

        var template = Map(request);
        var duplicate = await _context.CommunicationTemplates.AnyAsync(existing =>
            existing.Code == template.Code &&
            existing.Channel == template.Channel &&
            existing.Language == template.Language &&
            existing.ScopeType == template.ScopeType &&
            existing.ScopeId == template.ScopeId);
        if (duplicate)
        {
            return Conflict("This template variant already exists");
        }

        var hospitalNames = await LoadHospitalNamesAsync(template.ScopeType, template.ScopeId);
        if (template.ScopeType == CommunicationScopes.Center && hospitalNames.Count == 0)
        {
            return BadRequest("Unknown center");
        }

        _context.CommunicationTemplates.Add(template);
        await _context.SaveChangesAsync();
        return Ok(ToDto(template, hospitalNames));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CommunicationTemplateDto>> Update(
        int id, [FromBody] SaveCommunicationTemplateRequest request)
    {
        var template = await _context.CommunicationTemplates.FirstOrDefaultAsync(entry => entry.Id == id);
        if (template == null)
        {
            return NotFound("Template not found");
        }

        var validation = Validate(request);
        if (validation != null)
        {
            return BadRequest(validation);
        }

        var updated = Map(request);
        var duplicate = await _context.CommunicationTemplates.AnyAsync(existing =>
            existing.Id != id &&
            existing.Code == updated.Code &&
            existing.Channel == updated.Channel &&
            existing.Language == updated.Language &&
            existing.ScopeType == updated.ScopeType &&
            existing.ScopeId == updated.ScopeId);
        if (duplicate)
        {
            return Conflict("This template variant already exists");
        }

        var hospitalNames = await LoadHospitalNamesAsync(updated.ScopeType, updated.ScopeId);
        if (updated.ScopeType == CommunicationScopes.Center && hospitalNames.Count == 0)
        {
            return BadRequest("Unknown center");
        }

        template.Code = updated.Code;
        template.Channel = updated.Channel;
        template.Language = updated.Language;
        template.ScopeType = updated.ScopeType;
        template.ScopeId = updated.ScopeId;
        template.Subject = updated.Subject;
        template.Body = updated.Body;
        template.IsActive = updated.IsActive;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ToDto(template, hospitalNames));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await _context.CommunicationTemplates.FirstOrDefaultAsync(entry => entry.Id == id);
        if (template == null)
        {
            return NotFound("Template not found");
        }

        _context.CommunicationTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("sms-log")]
    public async Task<ActionResult<List<SmsLogDto>>> GetSmsLog([FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        var rows = await _context.SmsLogs
            .AsNoTracking()
            .OrderByDescending(log => log.CreatedAtUtc)
            .Take(limit)
            .Select(log => new SmsLogDto
            {
                Id = log.Id,
                CreatedAtUtc = log.CreatedAtUtc,
                ToNumber = log.ToNumber,
                TemplateCode = log.TemplateCode,
                Status = log.Status,
                Error = log.Error,
                RecipientName = log.RecipientName,
                ReservationSlotId = log.ReservationSlotId
            })
            .ToListAsync();

        return Ok(rows);
    }

    private async Task<Dictionary<int, string>> LoadHospitalNamesAsync(string scopeType, int scopeId)
    {
        if (scopeType != CommunicationScopes.Center)
        {
            return new Dictionary<int, string>();
        }

        var hospital = await _context.Hospitals
            .Where(entry => entry.Id == scopeId)
            .Select(entry => new { entry.Id, entry.Name })
            .FirstOrDefaultAsync();
        return hospital == null
            ? new Dictionary<int, string>()
            : new Dictionary<int, string> { [hospital.Id] = hospital.Name ?? $"#{hospital.Id}" };
    }

    private static string? Validate(SaveCommunicationTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 50)
        {
            return "Code is required (max 50 characters)";
        }
        if (!Channels.Contains(request.Channel?.Trim() ?? string.Empty))
        {
            return "Channel must be Email or Sms";
        }
        if (!ScopeTypes.Contains(request.ScopeType?.Trim() ?? string.Empty))
        {
            return "ScopeType must be Global, Country or Center";
        }
        if (string.IsNullOrWhiteSpace(request.Language) || request.Language.Trim().Length is < 2 or > 5)
        {
            return "Language must be 2-5 characters";
        }
        if (request.Body is null || string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 20000)
        {
            return "Body is required (max 20000 characters)";
        }
        if (string.Equals(request.Channel, CommunicationChannels.Email, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length > 300))
        {
            return "Subject is required for e-mail templates (max 300 characters)";
        }
        if (string.Equals(request.ScopeType, CommunicationScopes.Global, StringComparison.OrdinalIgnoreCase) &&
            request.ScopeId != 0)
        {
            return "Global templates must use ScopeId 0";
        }
        if (!string.Equals(request.ScopeType, CommunicationScopes.Global, StringComparison.OrdinalIgnoreCase) &&
            request.ScopeId <= 0)
        {
            return "Country/Center templates require a positive ScopeId";
        }
        return null;
    }

    private static CommunicationTemplate Map(SaveCommunicationTemplateRequest request) => new()
    {
        Code = request.Code.Trim(),
        Channel = string.Equals(request.Channel, CommunicationChannels.Sms, StringComparison.OrdinalIgnoreCase)
            ? CommunicationChannels.Sms
            : CommunicationChannels.Email,
        Language = request.Language.Trim().ToLowerInvariant(),
        ScopeType = ScopeTypes.First(scope =>
            string.Equals(scope, request.ScopeType.Trim(), StringComparison.OrdinalIgnoreCase)),
        ScopeId = request.ScopeId,
        Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
        Body = request.Body,
        IsActive = request.IsActive
    };

    private static CommunicationTemplateDto ToDto(
        CommunicationTemplate template, IReadOnlyDictionary<int, string> hospitalNames)
    {
        var scopeName = template.ScopeType switch
        {
            CommunicationScopes.Center => hospitalNames.TryGetValue(template.ScopeId, out var hospital)
                ? hospital
                : $"#{template.ScopeId}",
            CommunicationScopes.Country => CountryScopeMapper.ToCountryCode(template.ScopeId) ?? $"#{template.ScopeId}",
            _ => "Global"
        };

        return new CommunicationTemplateDto
        {
            Id = template.Id,
            Code = template.Code,
            Channel = template.Channel,
            Language = template.Language,
            ScopeType = template.ScopeType,
            ScopeId = template.ScopeId,
            ScopeName = scopeName,
            Subject = template.Subject,
            Body = template.Body,
            IsActive = template.IsActive,
            UpdatedAtUtc = template.UpdatedAtUtc
        };
    }
}
