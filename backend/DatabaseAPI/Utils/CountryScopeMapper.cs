namespace DatabaseAPI.Utils;

public static class CountryScopeMapper
{
    private static readonly IReadOnlyDictionary<string, int> ScopeByCountryCode =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CZ"] = 203,
            ["DE"] = 276,
            ["SK"] = 703,
            ["AT"] = 40,
            ["PL"] = 616,
            ["NL"] = 528
        };

    public static int ToScopeId(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return 203;

        return ScopeByCountryCode.TryGetValue(countryCode.Trim().ToUpperInvariant(), out var scopeId)
            ? scopeId
            : 999;
    }

    public static string NormalizeCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return "CZ";

        var normalized = countryCode.Trim().ToUpperInvariant();
        return normalized.Length >= 2 ? normalized[..2] : "CZ";
    }
}
