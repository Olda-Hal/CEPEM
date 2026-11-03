using Prometheus;
using Cepem.Shared;
using HealthcareAPI.Authorization;
using HealthcareAPI.Middleware;
using HealthcareAPI.Proxy;
using HealthcareAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy;

using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

// Structured JSON logs in production, readable rendered text in Development.
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "HealthcareAPI");
    if (context.HostingEnvironment.IsDevelopment())
    {
        configuration.WriteTo.Console();
    }
    else
    {
        configuration.WriteTo.Console(new JsonFormatter());
    }

    // Optional error tracking. Leaving SENTRY_DSN empty disables it.
    var sentryDsn = context.Configuration["Sentry:Dsn"]
        ?? Environment.GetEnvironmentVariable("SENTRY_DSN");
    if (!string.IsNullOrWhiteSpace(sentryDsn))
    {
        configuration.WriteTo.Sentry(options =>
        {
            options.Dsn = sentryDsn;
            options.Environment = context.HostingEnvironment.EnvironmentName;
            options.MinimumBreadcrumbLevel = Serilog.Events.LogEventLevel.Information;
            options.MinimumEventLevel = Serilog.Events.LogEventLevel.Error;
        });
    }
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddCepemForwardedHeaders(builder.Configuration);

// Register repositories and services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// Register the authentication handler
builder.Services.AddTransient<HealthcareAPI.Handlers.AuthenticationHandler>();

// Add HttpClient for DatabaseAPI communication
var databaseApiUrl = builder.Configuration["DatabaseApiUrl"]
    ?? Environment.GetEnvironmentVariable("DATABASE_API_URL")
    ?? "http://database-api:5001";

// Shared service key for authenticating calls to DatabaseAPI
var databaseApiServiceKey = builder.Configuration["DatabaseApiServiceKey"]
    ?? Environment.GetEnvironmentVariable("DATABASE_API_SERVICE_KEY");
if (string.IsNullOrWhiteSpace(databaseApiServiceKey))
{
    throw new InvalidOperationException(
        "DatabaseAPI service key is not configured. Set the DatabaseApiServiceKey configuration value or the DATABASE_API_SERVICE_KEY environment variable.");
}

builder.Services.AddHttpClient("DatabaseAPI", client =>
{
    client.BaseAddress = new Uri(databaseApiUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler<HealthcareAPI.Handlers.AuthenticationHandler>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITokenValidityService, TokenValidityService>();

// Activity log client (typed) - same base address as the named DatabaseAPI client
builder.Services.AddHttpClient<IActivityLogService, ActivityLogService>(client =>
{
    client.BaseAddress = new Uri(databaseApiUrl.TrimEnd('/') + "/");
})
// DatabaseAPI requires X-Service-Key on every non-health request; without
// this handler the audit POST would be rejected with 401.
.AddHttpMessageHandler<HealthcareAPI.Handlers.AuthenticationHandler>();

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JWT");
var secretKey = jwtSettings["SecretKey"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
if (string.IsNullOrWhiteSpace(secretKey))
{
    throw new InvalidOperationException(
        "JWT secret key is not configured. Set the JWT__SecretKey configuration value or the JWT_SECRET_KEY environment variable.");
}
var issuer = jwtSettings["Issuer"]
    ?? Environment.GetEnvironmentVariable("JWT_ISSUER");
var audience = jwtSettings["Audience"]
    ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };

        // A JWT is only valid while its acl_version still matches the DB and
        // the employee remains active. This is what makes password changes,
        // deactivation, role changes and logout revoke existing tokens.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;

                // Refresh tokens are signed by the same key but must never be
                // usable as bearer access tokens (missing claim = legacy token).
                if (principal?.FindFirst("token_type")?.Value == "refresh")
                {
                    context.Fail("Refresh token cannot be used as an access token.");
                    return;
                }

                var employeeIdValue = principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var versionValue = principal?.FindFirst("acl_version")?.Value;

                if (!int.TryParse(employeeIdValue, out var employeeId) ||
                    !int.TryParse(versionValue, out var accessControlVersion))
                {
                    context.Fail("Token is missing employee/acl_version claims.");
                    return;
                }

                var validator = context.HttpContext.RequestServices
                    .GetRequiredService<ITokenValidityService>();
                var validity = await validator.ValidateAsync(
                    employeeId, accessControlVersion, context.HttpContext.RequestAborted);

                if (!validity.Valid)
                {
                    context.Fail("Token has been revoked.");
                    return;
                }

                if (validity.PasswordExpired)
                {
                    // Only the forced password-change flow, /me and logout may
                    // be used until the password is changed.
                    var path = (context.Request.Path.Value ?? string.Empty).TrimEnd('/');
                    var allowed =
                        path.Equals("/api/employees/me", StringComparison.OrdinalIgnoreCase) ||
                        path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase) ||
                        path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase);

                    if (!allowed)
                    {
                        context.Fail("Password change required.");
                    }
                }
            }
        };
    });

// Add CORS
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(",") 
        ?? Environment.GetEnvironmentVariable("HEALTHCARE_CORS_ORIGINS")?.Split(",")
        ?? new[] { "http://localhost:3000" };
    
    Console.WriteLine($"[CORS] Allowed origins: {string.Join(", ", allowedOrigins)}");
    
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Rate limiting. Login keeps its tight brute-force policy; writes are
// throttled harder than reads so a compromised UI session can't flood the
// write path while still keeping dashboards usable.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login brute-force budget; configurable so E2E/dev can raise it without
    // weakening the production default (10 attempts per minute per IP).
    var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 10);

    // everything without an explicit policy gets a generous read budget
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 600,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitLimit,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("api-write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Authorization policies for YARP routes: "authenticated" for per-user data
// without a specific permission, and "perm:{code}" for permission-gated APIs.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IPermissionEvaluator, PermissionEvaluator>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());

    foreach (var policyName in ProxyConfig.Routes
                 .Select(route => route.AuthorizationPolicy)
                 .Where(policy => policy != null && policy.StartsWith("perm:", StringComparison.Ordinal))
                 .Distinct()!)
    {
        var code = policyName["perm:".Length..];
        options.AddPolicy(policyName, policy =>
            policy.Requirements.Add(new PermissionRequirement(code)));
    }
});

// YARP pass-through proxy for routes that used to be hand-written
// copy-paste proxy controller actions
var proxyBuilder = builder.Services.AddReverseProxy()
    .LoadFromMemory(ProxyConfig.Routes, ProxyConfig.GetClusters(databaseApiUrl));
ProxyConfig.AddClaimsTransform(proxyBuilder, databaseApiServiceKey);

var app = builder.Build();

// Must be first: rate limiting, audit and metrics all read the caller IP.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Correlation ID before request logging so every entry carries it;
// downstream DatabaseAPI calls propagate the same header.
app.UseMiddleware<HealthcareAPI.Middleware.CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
    };
});

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseRateLimiter();
app.UseCors("AllowFrontend");
app.UseAuthentication();
// Must run after authentication so the audit row carries the caller's
// employee id/e-mail (and before authorization so rejected calls are logged).
app.UseMiddleware<ActivityLoggingMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapReverseProxy();
app.UseMetricServer(); // /metrics for Prometheus
app.UseHttpMetrics();

app.Run();

// Make the implicit Program class public so it can be referenced by tests
public partial class Program { }
