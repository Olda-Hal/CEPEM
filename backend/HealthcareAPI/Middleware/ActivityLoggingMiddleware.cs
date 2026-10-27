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
                await activityLogService.LogActivityAsync(log);
            }
        }
    }
}
