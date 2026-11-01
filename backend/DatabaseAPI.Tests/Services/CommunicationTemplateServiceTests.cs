using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Scope resolution is Center &gt; Country &gt; Global, with language fallback
/// to English and then Czech. Placeholders are replaced; unknown ones stay
/// visible so an admin typo is not silently deleted.
/// </summary>
public class CommunicationTemplateServiceTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly CommunicationTemplateService _service;

    public CommunicationTemplateServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _service = new CommunicationTemplateService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private CommunicationTemplate Seed(
        string scopeType,
        int scopeId,
        string language = "cs",
        string body = "Scope body",
        bool active = true,
        string code = CommunicationTemplateCodes.IntakeReminder,
        string channel = CommunicationChannels.Email,
        DateTime? updatedAtUtc = null)
    {
        var template = new CommunicationTemplate
        {
            Code = code,
            Channel = channel,
            Language = language,
            ScopeType = scopeType,
            ScopeId = scopeId,
            Subject = "Subject",
            Body = body,
            IsActive = active,
            UpdatedAtUtc = updatedAtUtc ?? DateTime.UtcNow
        };
        _context.CommunicationTemplates.Add(template);
        _context.SaveChanges();
        return template;
    }

    [Fact]
    public async Task Resolve_PrefersCenterThenCountryThenGlobal()
    {
        var global = Seed(CommunicationScopes.Global, 0, body: "global");
        var country = Seed(CommunicationScopes.Country, 203, body: "country");
        var center = Seed(CommunicationScopes.Center, 7, body: "center");

        var centerTemplate = await _service.ResolveAsync(
            CommunicationTemplateCodes.IntakeReminder, CommunicationChannels.Email, "cs", 7, 203);
        Assert.Equal(center.Id, centerTemplate?.Id);

        _context.CommunicationTemplates.Remove(center);
        await _context.SaveChangesAsync();

        var countryTemplate = await _service.ResolveAsync(
            CommunicationTemplateCodes.IntakeReminder, CommunicationChannels.Email, "cs", 7, 203);
        Assert.Equal(country.Id, countryTemplate?.Id);

        _context.CommunicationTemplates.Remove(country);
        await _context.SaveChangesAsync();

        var globalTemplate = await _service.ResolveAsync(
            CommunicationTemplateCodes.IntakeReminder, CommunicationChannels.Email, "cs", 7, 203);
        Assert.Equal(global.Id, globalTemplate?.Id);
    }

    [Fact]
    public async Task Resolve_IgnoresInactiveAndFallsBackToEnglish()
    {
        Seed(CommunicationScopes.Global, 0, language: "cs", active: false);
        var english = Seed(CommunicationScopes.Global, 0, language: "en", body: "english");

        var template = await _service.ResolveAsync(
            CommunicationTemplateCodes.IntakeReminder, CommunicationChannels.Email, "nl", 7, 528);

        Assert.Equal(english.Id, template?.Id);
    }

    [Fact]
    public async Task Resolve_ReturnsNullWhenNothingMatches()
    {
        var template = await _service.ResolveAsync(
            "missing_code", CommunicationChannels.Sms, "cs", 7, 203);

        Assert.Null(template);
    }

    [Fact]
    public async Task Resolve_DoesNotMixChannels()
    {
        Seed(CommunicationScopes.Global, 0, channel: CommunicationChannels.Sms, body: "sms");

        var email = await _service.ResolveAsync(
            CommunicationTemplateCodes.IntakeReminder, CommunicationChannels.Email, "cs", 7, 203);
        var sms = await _service.ResolveAsync(
            CommunicationTemplateCodes.IntakeReminder, CommunicationChannels.Sms, "cs", 7, 203);

        Assert.Null(email);
        Assert.NotNull(sms);
    }

    [Fact]
    public async Task Resolve_CountryOverrideForAnotherCountry_FallsBackToGlobal()
    {
        var global = Seed(CommunicationScopes.Global, 0, body: "global");
        Seed(CommunicationScopes.Country, 528, body: "NL country");

        var template = await _service.ResolveAsync(
            CommunicationTemplateCodes.IntakeReminder, CommunicationChannels.Email, "cs", 7, 203);

        Assert.Equal(global.Id, template?.Id);
    }

    [Fact]
    public async Task Resolve_FallsBackToCzechWhenRequestedAndEnglishAreMissing()
    {
        var czech = Seed(CommunicationScopes.Global, 0, language: "cs", body: "czech");

        var template = await _service.ResolveAsync(
            CommunicationTemplateCodes.IntakeReminder, CommunicationChannels.Email, "de", 7, 276);

        Assert.Equal(czech.Id, template?.Id);
    }

    [Fact]
    public async Task Resolve_PrefersMostRecentlyUpdatedTemplateWithinSameScope()
    {
        var older = Seed(CommunicationScopes.Global, 0, body: "older", updatedAtUtc: DateTime.UtcNow.AddDays(-2));
        var newer = Seed(CommunicationScopes.Global, 0, body: "newer", updatedAtUtc: DateTime.UtcNow);

        var template = await _service.ResolveAsync(
            CommunicationTemplateCodes.IntakeReminder, CommunicationChannels.Email, "cs", 7, 203);

        Assert.Equal(newer.Id, template?.Id);
        Assert.NotEqual(older.Id, template?.Id);
    }

    [Fact]
    public void Render_ReplacesKnownValuesAndKeepsUnknownPlaceholders()
    {
        var rendered = _service.Render(
            "Hello {{name}}, visit {{date}}. {{unknown}}",
            new Dictionary<string, string?>
            {
                ["name"] = "Eva",
                ["date"] = null
            });

        Assert.Equal("Hello Eva, visit . {{unknown}}", rendered);
    }

    [Fact]
    public void Render_AllowsWhitespaceInsidePlaceholders()
    {
        var rendered = _service.Render(
            "Hello {{ name }}, visit {{  date  }}.",
            new Dictionary<string, string?>
            {
                ["name"] = "Eva",
                ["date"] = "12. 11. 2026"
            });

        Assert.Equal("Hello Eva, visit 12. 11. 2026.", rendered);
    }
}
