using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace HealthcareAPI.Services;

public interface ITokenValidityService
{
    /// <summary>
    /// Returns false when the JWT's acl_version no longer matches the employee
    /// or the account has been deactivated. Fail-closed on service errors.
    /// </summary>
    Task<bool> IsTokenValidAsync(
        int employeeId,
        int accessControlVersion,
        CancellationToken cancellationToken = default);
}

public class TokenValidityService : ITokenValidityService
{
    private static readonly TimeSpan PositiveCacheDuration = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TokenValidityService> _logger;

    public TokenValidityService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<TokenValidityService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsTokenValidAsync(
        int employeeId,
        int accessControlVersion,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"token-valid:{employeeId}:{accessControlVersion}";
        if (_cache.TryGetValue(cacheKey, out bool cached) && cached)
        {
            return true;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            using var response = await client.GetAsync(
                $"/api/auth/token-valid?employeeId={employeeId}&accessControlVersion={accessControlVersion}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var payload = await response.Content.ReadFromJsonAsync<TokenValidityResponse>(
                cancellationToken: cancellationToken);
            if (payload?.Valid != true)
            {
                return false;
            }

            // Cache only positive results: a revoked token must start failing
            // as soon as the next uncached check happens.
            _cache.Set(cacheKey, true, PositiveCacheDuration);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not validate token for employee {EmployeeId}", employeeId);
            return false;
        }
    }

    private sealed class TokenValidityResponse
    {
        public bool Valid { get; set; }
    }
}
