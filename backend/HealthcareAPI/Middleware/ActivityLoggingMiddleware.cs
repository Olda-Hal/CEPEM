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
            var service = "HealthcareAPI"; // Or get it from configuration

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
