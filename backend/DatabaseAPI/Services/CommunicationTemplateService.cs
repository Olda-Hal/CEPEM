using System.Text.RegularExpressions;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public static class CommunicationLanguages
{
    public static string FromCountry(string? countryCode) =>
        countryCode?.Trim().ToUpperInvariant() switch
        {
            "NL" => "nl",
            "CZ" or null or "" => "cs",
            _ => "en"
        };
}

public interface ICommunicationTemplateService
{
    /// <summary>
    /// Effective template: Center &gt; Country &gt; Global for the requested
    /// code/channel/language. Falls back to English and Czech when the
    /// requested language has no wording.
    /// </summary>
    Task<CommunicationTemplate?> ResolveAsync(
        string code,
        string channel,
        string language,
        int? hospitalId,
        int? countryScopeId,
        CancellationToken cancellationToken = default);

    string Render(string text, IReadOnlyDictionary<string, string?> values);
}

public class CommunicationTemplateService : ICommunicationTemplateService
{
    private static readonly Regex PlaceholderPattern = new(
        @"\{\{(\w+)\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly DatabaseContext _context;

    public CommunicationTemplateService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<CommunicationTemplate?> ResolveAsync(
        string code,
        string channel,
        string language,
        int? hospitalId,
        int? countryScopeId,
        CancellationToken cancellationToken = default)
    {
        foreach (var candidateLanguage in new[] { language, "en", "cs" }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var templates = await _context.CommunicationTemplates
                .AsNoTracking()
                .Where(template =>
                    template.IsActive &&
                    template.Code == code &&
                    template.Channel == channel &&
                    template.Language == candidateLanguage)
                .ToListAsync(cancellationToken);

            var resolved = templates
                .Where(template => IsInScope(template, hospitalId, countryScopeId))
                .OrderByDescending(template => ScopePriority(template.ScopeType))
                .ThenByDescending(template => template.UpdatedAtUtc)
                .FirstOrDefault();

            if (resolved != null)
            {
                return resolved;
            }
        }

        return null;
    }

    public string Render(string text, IReadOnlyDictionary<string, string?> values)
    {
        return PlaceholderPattern.Replace(text, match =>
        {
            var key = match.Groups[1].Value;
            // Unknown placeholders stay visible: an admin typo should show up
            // in the message instead of silently deleting a word.
            return values.TryGetValue(key, out var value) ? value ?? string.Empty : match.Value;
        });
    }

    private static bool IsInScope(CommunicationTemplate template, int? hospitalId, int? countryScopeId) =>
        template.ScopeType switch
        {
            CommunicationScopes.Global => template.ScopeId == 0,
            CommunicationScopes.Country => countryScopeId.HasValue && template.ScopeId == countryScopeId.Value,
            CommunicationScopes.Center => hospitalId.HasValue && template.ScopeId == hospitalId.Value,
            _ => false
        };

    private static int ScopePriority(string scopeType) => scopeType switch
    {
        CommunicationScopes.Center => 3,
        CommunicationScopes.Country => 2,
        CommunicationScopes.Global => 1,
        _ => 0
    };
}
