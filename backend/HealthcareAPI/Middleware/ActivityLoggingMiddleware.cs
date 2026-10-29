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
                await activityLogService.LogActivityAsync(log);
            }
        }
    }
}
