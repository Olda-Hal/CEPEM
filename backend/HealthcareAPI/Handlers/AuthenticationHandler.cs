using System.Net.Http.Headers;
using System.Security.Claims;

namespace HealthcareAPI.Handlers
{
    public class AuthenticationHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthenticationHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
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
