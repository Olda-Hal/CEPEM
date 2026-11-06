namespace Cepem.Shared;

/// <summary>
/// CSV cell sanitizer shared by every export.
///
/// Besides ordinary quoting it neutralises spreadsheet formula injection:
/// Excel/LibreOffice execute cells beginning with =, +, -, @, TAB or CR, even
/// when the attacker-controlled value came from a patient/employee field.
/// Such cells are prefixed with an apostrophe.
/// </summary>
public static class CsvSanitizer
{
    private static readonly char[] FormulaPrefixes = { '=', '+', '-', '@', '\t', '\r' };

    public static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && FormulaPrefixes.Contains(text[0]))
        {
            text = "'" + text;
        }

        return text.Contains(';') || text.Contains('"') ||
               text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
