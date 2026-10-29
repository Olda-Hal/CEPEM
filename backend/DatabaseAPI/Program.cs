using Prometheus;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.Services;
using DatabaseAPI.Repositories;

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
        .Enrich.WithProperty("Service", "DatabaseAPI");
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
builder.Services.AddHttpContextAccessor();

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_API_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is not configured. Set ConnectionStrings:DefaultConnection or the DATABASE_API_CONNECTION_STRING environment variable.");
}

// Use a specific MySQL version to avoid auto-detection issues
var serverVersion = new MySqlServerVersion(new Version(8, 0, 33));
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// Register migration service
builder.Services.AddScoped<IMigrationService, MigrationService>();

// Register seed service
builder.Services.AddScoped<ISeedService, SeedService>();

// Register authentication service
builder.Services.AddScoped<IEmployeeAuthService, EmployeeAuthService>();

// Register employee management service
builder.Services.AddScoped<IEmployeeManagementService, EmployeeManagementService>();

// Register access control services
builder.Services.AddScoped<IActorCountryContextService, ActorCountryContextService>();
builder.Services.AddScoped<IEmployeeScopeService, EmployeeScopeService>();
builder.Services.AddScoped<IUidService, UidService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IEventOptionsService, EventOptionsService>();
builder.Services.AddScoped<IIntakeFormLinkService, IntakeFormLinkService>();
builder.Services.AddScoped<IExaminationsRepository, ExaminationsRepository>();
builder.Services.AddScoped<IExaminationsService, ExaminationsService>();

// Register photo services
builder.Services.AddScoped<PhotoEncryptionService>();
builder.Services.AddScoped<PatientPhotoService>();

// Register document services
builder.Services.AddScoped<DocumentEncryptionService>();
builder.Services.AddScoped<PatientDocumentService>();
builder.Services.AddScoped<ExaminationDocumentService>();

// Periodic cleanup of old activity logs (the table grows without bound)
builder.Services.AddHostedService<ActivityLogRetentionService>();

// CORS Configuration
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(",") 
        ?? Environment.GetEnvironmentVariable("DATABASE_CORS_ORIGINS")?.Split(",")
        ?? new[] { "http://localhost:3000" };
    
    Console.WriteLine($"[CORS] Allowed origins: {string.Join(", ", allowedOrigins)}");
    
    options.AddPolicy("AllowHealthcareAPI",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Database API V1");
        c.RoutePrefix = string.Empty;
    });
}

// Correlation ID first: the auth handler and request logging both see it.
app.UseMiddleware<DatabaseAPI.Middleware.CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

app.UseMiddleware<DatabaseAPI.Middleware.GlobalExceptionMiddleware>();
app.UseMiddleware<DatabaseAPI.Middleware.ServiceAuthMiddleware>();
app.UseCors("AllowHealthcareAPI");
app.MapControllers();
app.UseMetricServer(); // /metrics for Prometheus
app.UseHttpMetrics();

// Apply database migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
    var seedService = scope.ServiceProvider.GetRequiredService<ISeedService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Check database connection
        var canConnect = await migrationService.CanConnectAsync();
        if (!canConnect)
        {
            logger.LogWarning("Cannot connect to database. Will retry on startup...");
        }
        else
        {
            // Apply migrations
            await migrationService.ApplyMigrationsAsync();
            logger.LogInformation("Database ready - migrations applied.");
            
            // Seed initial data
            await seedService.SeedAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database initialization.");
        // Don't throw - let the application start and handle database issues gracefully
    }
}

app.Run();

// Make the implicit Program class public so it can be referenced by tests
public partial class Program { }
