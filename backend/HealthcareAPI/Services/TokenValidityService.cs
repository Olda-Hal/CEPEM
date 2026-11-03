using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace HealthcareAPI.Services;

public sealed record TokenValidityResult(bool Valid, bool PasswordExpired);

public interface ITokenValidityService
{
    /// <summary>
    /// Returns Valid=false when the JWT's acl_version no longer matches the
    /// employee or the account has been deactivated. PasswordExpired=true
    /// means only the password-change/self/logout endpoints may be used.
    /// Fail-closed on service errors.
    /// </summary>
    Task<TokenValidityResult> ValidateAsync(
        int employeeId,
        int accessControlVersion,
        CancellationToken cancellationToken = default);
}

public class TokenValidityService : ITokenValidityService
{
    private static readonly TimeSpan PositiveCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TokenValidityResult InvalidResult = new(false, false);

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

    public async Task<TokenValidityResult> ValidateAsync(
        int employeeId,
        int accessControlVersion,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"token-valid:{employeeId}:{accessControlVersion}";
        if (_cache.TryGetValue(cacheKey, out TokenValidityResult? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            using var response = await client.GetAsync(
                $"/api/auth/token-valid?employeeId={employeeId}&accessControlVersion={accessControlVersion}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return InvalidResult;
            }

            var payload = await response.Content.ReadFromJsonAsync<TokenValidityResponse>(
                cancellationToken: cancellationToken);
            if (payload?.Valid != true)
            {
                return InvalidResult;
            }

            var result = new TokenValidityResult(true, payload.PasswordExpired);

            // Cache only positive results: a revoked token must start failing
            // as soon as the next uncached check happens.
            _cache.Set(cacheKey, result, PositiveCacheDuration);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return InvalidResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not validate token for employee {EmployeeId}", employeeId);
            return InvalidResult;
        }
    }

    private sealed class TokenValidityResponse
    {
        public bool Valid { get; set; }
        public bool PasswordExpired { get; set; }
    }
}
