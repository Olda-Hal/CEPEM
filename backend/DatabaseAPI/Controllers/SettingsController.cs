using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.APIModels;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ILogger<SettingsController> logger)
    {
        _logger = logger;
    }

    [HttpGet("quick-preview")]
    public async Task<ActionResult<QuickPreviewSettingsDto>> GetQuickPreviewSettings()
    {
        try
        {
            // Pro nyní vrátíme výchozí nastavení
            // V budoucnu se nastavení uloží do databáze pro každého uživatele
            var settings = new QuickPreviewSettingsDto();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting quick preview settings");
            return StatusCode(500, "An error occurred while getting quick preview settings");
        }
    }

    [HttpPut("quick-preview")]
    public async Task<ActionResult<QuickPreviewSettingsDto>> UpdateQuickPreviewSettings([FromBody] UpdateQuickPreviewSettingsRequest request)
    {
        try
        {
            // Pro nyní jen vrátíme nastavení zpět
            // V budoucnu se nastavení uloží do databáze pro každého uživatele
            var settings = new QuickPreviewSettingsDto
            {
                ShowCovidVaccination = request.ShowCovidVaccination,
                ShowFluVaccination = request.ShowFluVaccination,
                ShowDiabetes = request.ShowDiabetes,
                ShowHypertension = request.ShowHypertension,
                ShowHeartDisease = request.ShowHeartDisease,
                ShowAllergies = request.ShowAllergies,
                ShowRecentEvents = request.ShowRecentEvents,
                ShowUpcomingAppointments = request.ShowUpcomingAppointments,
                ShowLastVisit = request.ShowLastVisit
            };
            
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while updating quick preview settings");
            return StatusCode(500, "An error occurred while updating quick preview settings");
        }
    }
}
