using HealthcareAPI.Models;
using HealthcareAPI.Services;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace HealthcareAPI.Middleware
{
    public class ActivityLoggingMiddleware
    {
        private readonly RequestDelegate _next;

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
                Details = $"Request from {context.Connection.RemoteIpAddress}"
            };

            await activityLogService.LogActivityAsync(log);

            await _next(context);
        }
    }
}
