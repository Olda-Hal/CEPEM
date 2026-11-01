using System.Net.Http.Json;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Services;

public class SmsOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>log (default, no external calls) or http (generic JSON webhook).</summary>
    public string Provider { get; set; } = "log";

    /// <summary>HTTP provider endpoint accepting { to, text, sender }.</summary>
    public string? Endpoint { get; set; }

    public string? ApiKey { get; set; }
    public string Sender { get; set; } = "CEPEM";
    public int TimeoutSeconds { get; set; } = 20;
}

public sealed record SmsLogContext(int? PersonId = null, string? RecipientName = null, int? ReservationSlotId = null);

public interface ISmsService
{
    bool IsConfigured { get; }

    Task SendAsync(
        string toNumber,
        string text,
        string templateCode,
        SmsLogContext? context = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provider-agnostic SMS sender. The default "log" provider only writes to
/// the application log and SMS log table; "http" posts a small JSON payload
/// to a configured gateway. Delivery is intentionally vendor-neutral.
/// </summary>
public class SmsService : ISmsService
{
    private readonly SmsOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmsService> _logger;

    public SmsService(
        Microsoft.Extensions.Options.IOptions<SmsOptions> options,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<SmsService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsConfigured =>
        _options.Enabled &&
        (!string.Equals(_options.Provider, "http", StringComparison.OrdinalIgnoreCase) ||
         !string.IsNullOrWhiteSpace(_options.Endpoint));

    public async Task SendAsync(
        string toNumber,
        string text,
        string templateCode,
        SmsLogContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toNumber))
        {
            throw new ArgumentException("SMS recipient is required", nameof(toNumber));
        }

        var useHttp = string.Equals(_options.Provider, "http", StringComparison.OrdinalIgnoreCase);
        if (!_options.Enabled)
        {
            await WriteLogAsync(toNumber, templateCode, EmailLogStatuses.LoggedOnly, "SMS is disabled", context, cancellationToken);
            return;
        }
        if (!useHttp || string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            _logger.LogWarning("SMS ({TemplateCode}) to {Number}: {Text}", templateCode, toNumber, text);
            await WriteLogAsync(toNumber, templateCode, EmailLogStatuses.LoggedOnly, "SMS provider is log-only", context, cancellationToken);
            return;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("Sms");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 120));

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    to = toNumber,
                    text,
                    sender = _options.Sender
                })
            };
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"SMS gateway returned {(int)response.StatusCode}: {Truncate(body, 500)}");
            }

            await WriteLogAsync(toNumber, templateCode, EmailLogStatuses.Sent, null, context, cancellationToken);
        }
        catch (Exception ex)
        {
            await WriteLogAsync(toNumber, templateCode, EmailLogStatuses.Failed, ex.Message, context, cancellationToken);
            throw;
        }
    }

    private async Task WriteLogAsync(
        string toNumber,
        string templateCode,
        string status,
        string? error,
        SmsLogContext? context,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            db.SmsLogs.Add(new SmsLog
            {
                CreatedAtUtc = DateTime.UtcNow,
                ToNumber = Truncate(toNumber, 40),
                TemplateCode = Truncate(templateCode, 50),
                Status = status,
                Error = Truncate(error, 1000),
                PersonId = context?.PersonId,
                RecipientName = Truncate(context?.RecipientName, 200),
                ReservationSlotId = context?.ReservationSlotId
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not write SMS log for {Number}", toNumber);
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
