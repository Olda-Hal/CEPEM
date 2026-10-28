using System.Net.Http.Headers;
using System.Security.Claims;

namespace HealthcareAPI.Handlers
{
    public class AuthenticationHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _serviceKey;

        public AuthenticationHandler(IHttpContextAccessor httpContextAccessor, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _serviceKey = configuration["DatabaseApiServiceKey"]
                ?? Environment.GetEnvironmentVariable("DATABASE_API_SERVICE_KEY")
                ?? string.Empty;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_serviceKey))
            {
                request.Headers.Remove("X-Service-Key");
                request.Headers.Add("X-Service-Key", _serviceKey);
            }

            // Forward the request's correlation ID so DatabaseAPI logs join
            // to the same trace.
            var correlationId = _httpContextAccessor.HttpContext?.Request.Headers[Middleware.CorrelationIdMiddleware.HeaderName].FirstOrDefault();
            if (!string.IsNullOrEmpty(correlationId))
            {
                request.Headers.Remove(Middleware.CorrelationIdMiddleware.HeaderName);
                request.Headers.Add(Middleware.CorrelationIdMiddleware.HeaderName, correlationId);
            }

            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext?.Request?.Headers != null)
            {
                var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
                }

                var actorEmployeeId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrWhiteSpace(actorEmployeeId))
                {
                    request.Headers.Remove("X-Actor-Employee-Id");
                    request.Headers.Add("X-Actor-Employee-Id", actorEmployeeId);
                }

                var actorCountryCode = httpContext.User.FindFirst("country_code")?.Value;
                if (!string.IsNullOrWhiteSpace(actorCountryCode))
                {
                    request.Headers.Remove("X-Actor-Country-Code");
                    request.Headers.Add("X-Actor-Country-Code", actorCountryCode);
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
