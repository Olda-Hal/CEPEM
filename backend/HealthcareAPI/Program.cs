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
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

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

// Activity log client (typed) - same base address as the named DatabaseAPI client
builder.Services.AddHttpClient<IActivityLogService, ActivityLogService>(client =>
{
    client.BaseAddress = new Uri(databaseApiUrl.TrimEnd('/') + "/");
});

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

// Rate limiting: brute-force protection on the login endpoint
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Permission-based authorization policies for YARP routes ("perm:{code}")
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IPermissionEvaluator, PermissionEvaluator>();
foreach (var policyName in ProxyConfig.Routes.Select(r => r.AuthorizationPolicy).Where(p => p != null).Distinct()!)
{
    var code = policyName["perm:".Length..];
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(policyName!, policy => policy.Requirements.Add(new PermissionRequirement(code)));
    });
}

// YARP pass-through proxy for routes that used to be hand-written
// copy-paste proxy controller actions
var proxyBuilder = builder.Services.AddReverseProxy()
    .LoadFromMemory(ProxyConfig.Routes, ProxyConfig.GetClusters(databaseApiUrl));
ProxyConfig.AddClaimsTransform(proxyBuilder, databaseApiServiceKey);

var app = builder.Build();

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
app.UseMiddleware<ActivityLoggingMiddleware>();

app.UseRateLimiter();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapReverseProxy();

app.Run();

// Make the implicit Program class public so it can be referenced by tests
public partial class Program { }
