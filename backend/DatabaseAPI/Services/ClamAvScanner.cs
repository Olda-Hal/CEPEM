using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace DatabaseAPI.Services;

public sealed class ClamAvOptions
{
    public const string SectionName = "ClamAv";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "clamav";
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// When clamd is unreachable: false rejects the upload (fail closed),
    /// true accepts it with a warning. Fail closed is the safe default.
    /// </summary>
    public bool FailOpen { get; set; }
}

public sealed record AntivirusScanResult(bool Clean, string? ThreatName, string? Error)
{
    public static AntivirusScanResult CleanResult { get; } = new(true, null, null);
    public static AntivirusScanResult Infected(string threat) => new(false, threat, null);
    public static AntivirusScanResult Unavailable(string error) => new(false, null, error);
}

public interface IAntivirusScanner
{
    Task<AntivirusScanResult> ScanAsync(byte[] content, CancellationToken cancellationToken = default);
}

/// <summary>No-op scanner used by tests that construct controllers directly.</summary>
public sealed class NullAntivirusScanner : IAntivirusScanner
{
    public Task<AntivirusScanResult> ScanAsync(byte[] content, CancellationToken cancellationToken = default) =>
        Task.FromResult(AntivirusScanResult.CleanResult);
}

/// <summary>
/// Minimal clamd INSTREAM client. Keeping the protocol in-process avoids
/// shipping a second HTTP service just to scan a handful of uploads.
/// </summary>
public sealed class ClamAvScanner : IAntivirusScanner
{
    private readonly ClamAvOptions _options;
    private readonly ILogger<ClamAvScanner> _logger;

    public ClamAvScanner(IOptions<ClamAvOptions> options, ILogger<ClamAvScanner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AntivirusScanResult> ScanAsync(
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return AntivirusScanResult.CleanResult;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_options.Host, _options.Port, timeout.Token);
            await using var stream = client.GetStream();

            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);

            const int chunkSize = 64 * 1024;
            for (var offset = 0; offset < content.Length; offset += chunkSize)
            {
                var length = Math.Min(chunkSize, content.Length - offset);
                var lengthPrefix = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, length);

                await stream.WriteAsync(lengthPrefix, timeout.Token);
                await stream.WriteAsync(content.AsMemory(offset, length), timeout.Token);
            }

            // Zero-length chunk terminates the stream.
            await stream.WriteAsync(new byte[4], timeout.Token);
            await stream.FlushAsync(timeout.Token);

            var response = await ReadResponseAsync(stream, timeout.Token);
            if (response.Contains("FOUND", StringComparison.Ordinal))
            {
                var threat = response.Split(':', 2).LastOrDefault()?.Replace("FOUND", string.Empty).Trim();
                _logger.LogWarning("ClamAV rejected an upload: {Threat}", threat ?? response);
                return AntivirusScanResult.Infected(threat ?? "malware");
            }

            if (response.Contains("OK", StringComparison.Ordinal))
            {
                return AntivirusScanResult.CleanResult;
            }

            _logger.LogWarning("ClamAV returned an unexpected response: {Response}", response);
            return AntivirusScanResult.Unavailable("Antivirus scan returned an unexpected response.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("ClamAV scan timed out after {Seconds}s", _options.TimeoutSeconds);
            return _options.FailOpen
                ? AntivirusScanResult.CleanResult
                : AntivirusScanResult.Unavailable("Antivirus scan timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClamAV scan failed");
            return _options.FailOpen
                ? AntivirusScanResult.CleanResult
                : AntivirusScanResult.Unavailable("Antivirus scan is unavailable.");
        }
    }

    private static async Task<string> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var response = new MemoryStream();
        var buffer = new byte[256];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            var terminator = Array.IndexOf(buffer, (byte)0, 0, read);
            if (terminator >= 0)
            {
                response.Write(buffer, 0, terminator);
                break;
            }

            response.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(response.ToArray()).Trim();
    }
}
