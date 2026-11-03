using Cepem.Shared;
using HealthcareAPI.Models;
using HealthcareAPI.Services;
using Microsoft.AspNetCore.Http;
using Prometheus;
using System;
using System.Threading.Tasks;

namespace HealthcareAPI.Middleware
{
    public class ActivityLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        // Per-client request counter — feeds the "traffic by source IP"
        // dashboard panel (closed hospital network ≙ bounded cardinality).
        private static readonly Counter RequestsByClient = Metrics.CreateCounter(
            "cepem_http_requests_by_client_total",
            "HTTP requests by client IP, method and status class",
            new CounterConfiguration { LabelNames = new[] { "remote_ip", "method", "status_class" } });

        // Low-cardinality geo aggregate for the Grafana world map. The country
        // comes from nginx (X-Country-Code, GeoIP2) or Cloudflare (CF-IPCountry);
        // direct clients without either header are counted as "unknown".
        private static readonly Counter RequestsByCountry = Metrics.CreateCounter(
            "cepem_http_requests_by_country_total",
            "HTTP requests by country, method and status class",
            new CounterConfiguration { LabelNames = new[] { "country", "method", "status_class" } });

        public ActivityLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IActivityLogService activityLogService)
        {
            var rawAction = context.Request.Path.ToString();
            var service = "HealthcareAPI";

            // Skip logging for activity logs endpoints to prevent infinite loops and spam
            if (rawAction.Contains("/api/activitylogs", StringComparison.OrdinalIgnoreCase) ||
                rawAction.Contains("/health", StringComparison.OrdinalIgnoreCase) ||
                rawAction.Contains("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var country = ResolveCountry(context);

            // Preview sessions act as the target employee; record who is really
            // behind the request so the audit trail is attributable.
            var impersonatorId = context.User.FindFirst("impersonator_id")?.Value;
            var impersonatorName = context.User.FindFirst("impersonator_name")?.Value;
            var details = PiiRedactor.RedactIpAddresses(
                $"Request from {context.Connection.RemoteIpAddress}");
            if (!string.IsNullOrWhiteSpace(impersonatorId))
            {
                var maskedImpersonator = impersonatorName?.Contains('@') == true
                    ? PiiRedactor.MaskEmail(impersonatorName)
                    : PiiRedactor.MaskName(impersonatorName);
                details += $" (impersonated by {maskedImpersonator} #{impersonatorId})";
            }

            var log = new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                Service = service,
                // Never persist raw intake-link tokens or full client IPs.
                Action = PiiRedactor.RedactPath(rawAction),
                Details = details,
                EmployeeId = int.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var employeeId) ? employeeId : null,
                EmployeeEmail = PiiRedactor.MaskEmail(
                    context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value),
                Method = context.Request.Method
            };

            try
            {
                await _next(context);
            }
            finally
            {
                log.StatusCode = context.Response.StatusCode;
                RequestsByClient
                    .WithLabels(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        context.Request.Method,
                        $"{context.Response.StatusCode / 100}xx")
                    .Inc();
                RequestsByCountry
                    .WithLabels(country,
                        context.Request.Method,
                        $"{context.Response.StatusCode / 100}xx")
                    .Inc();
                await activityLogService.LogActivityAsync(log);
            }
        }

        /// <summary>
        /// ISO-3166-1 alpha-2 country supplied by the reverse proxy's GeoIP
        /// module (or Cloudflare). Anything else is aggregated as "unknown".
        /// </summary>
        private static string ResolveCountry(HttpContext context)
        {
            var raw = context.Request.Headers["X-Country-Code"].FirstOrDefault()
                ?? context.Request.Headers["CF-IPCountry"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(raw))
            {
                return "unknown";
            }

            var code = raw.Trim().ToUpperInvariant();
            if (code.Length != 2 || code[0] < 'A' || code[0] > 'Z' || code[1] < 'A' || code[1] > 'Z')
            {
                return "unknown";
            }

            return code;
        }
    }
}
