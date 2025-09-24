using System.Net.Http.Headers;

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
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
