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
            var action = context.Request.Path.ToString();
            var service = "HealthcareAPI";

            // Skip logging for activity logs endpoints to prevent infinite loops and spam
            if (action.Contains("/api/activitylogs", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("/health", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var country = ResolveCountry(context);

            var log = new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                Service = service,
                Action = action,
                Details = $"Request from {context.Connection.RemoteIpAddress}",
                EmployeeId = int.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var employeeId) ? employeeId : null,
                EmployeeEmail = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
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
