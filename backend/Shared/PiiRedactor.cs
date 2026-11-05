using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Cepem.Shared;

/// <summary>
/// Masks personal data before it is written to audit/communication logs or
/// returned through their admin pages and CSV exports.
/// </summary>
public static partial class PiiRedactor
{
    public const string Mask = "***";

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b\d{1,3}(?:\.\d{1,3}){3}\b", RegexOptions.CultureInvariant)]
    private static partial Regex IPv4Pattern();

    [GeneratedRegex(
        @"^(/api/events/intake-form-links/)([^/]+)(/submit)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IntakeTokenPathPattern();

    /// <summary>j***@example.com — keeps the domain useful for support.</summary>
    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@');
        if (at <= 0)
        {
            return Mask;
        }

        var local = trimmed[..at];
        var domain = trimmed[(at + 1)..];
        var visible = local.Length <= 1 ? string.Empty : local[..1];
        return $"{visible}{Mask}@{domain}";
    }

    /// <summary>Jan Novák → J*** N***.</summary>
    public static string? MaskName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(part => $"{part[0]}{Mask}"));
    }

    /// <summary>10.1.2.3 → 10.1.*.* ; IPv6 keeps only the first 32 bits.</summary>
    public static string? MaskIpAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return address;
        }

        if (!IPAddress.TryParse(address.Trim(), out var ip))
        {
            return Mask;
        }

        var bytes = ip.GetAddressBytes();
        return ip.AddressFamily switch
        {
            AddressFamily.InterNetwork => $"{bytes[0]}.{bytes[1]}.*.*",
            AddressFamily.InterNetworkV6 =>
                $"{bytes[0]:x2}{bytes[1]:x2}:{bytes[2]:x2}{bytes[3]:x2}:****",
            _ => Mask
        };
    }

    /// <summary>Redacts any IPv4 address embedded in free text (e.g. "Request from 10.0.0.1").</summary>
    public static string? RedactIpAddresses(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return IPv4Pattern().Replace(text, match => MaskIpAddress(match.Value) ?? Mask);
    }

    /// <summary>Redacts e-mail addresses embedded in free text or SMTP errors.</summary>
    public static string? MaskEmails(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return EmailPattern().Replace(text, match => MaskEmail(match.Value) ?? Mask);
    }

    /// <summary>
    /// Intake-link tokens are bearer secrets. Keep only a short prefix so the
    /// audit trail still shows the route without storing a usable token.
    /// </summary>
    public static string RedactPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path ?? string.Empty;
        }

        var match = IntakeTokenPathPattern().Match(path);
        if (!match.Success)
        {
            return path;
        }

        var token = match.Groups[2].Value;
        var visible = token.Length <= 4 ? string.Empty : token[..4];
        return $"{match.Groups[1].Value}{visible}{Mask}{match.Groups[3].Value}";
    }
}
