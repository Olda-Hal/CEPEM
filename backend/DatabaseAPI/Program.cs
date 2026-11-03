using Prometheus;
using Microsoft.EntityFrameworkCore;
using Cepem.Shared;
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddCepemForwardedHeaders(builder.Configuration);

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

// E-mail + password reset (SMTP* / App__FrontendBaseUrl via configuration)
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddSingleton<IEmailLogWriter, EmailLogWriter>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();

// Register employee management service
builder.Services.AddScoped<IEmployeeManagementService, EmployeeManagementService>();

// MEIK .tdb measurements → Vyhodnocování case (slices + histograms)
builder.Services.AddScoped<ITdbAnalysisService, TdbAnalysisService>();

// Register access control services
builder.Services.AddScoped<IActorCountryContextService, ActorCountryContextService>();
builder.Services.AddScoped<IEmployeeScopeService, EmployeeScopeService>();
builder.Services.AddScoped<IUidService, UidService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<PatientMasterDataService>();
builder.Services.AddScoped<IPatientMergeService, PatientMergeService>();
builder.Services.AddScoped<IVisitService, VisitService>();
builder.Services.AddScoped<IEventOptionsService, EventOptionsService>();
builder.Services.AddScoped<IIntakeFormLinkService, IntakeFormLinkService>();
builder.Services.AddScoped<IPersonMatchingService, PersonMatchingService>();
builder.Services.AddScoped<IGdprService, GdprService>();
builder.Services.AddSingleton<IDocumentTextExtractionService, DocumentTextExtractionService>();
builder.Services.AddScoped<ICommunicationTemplateService, CommunicationTemplateService>();
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection("Sms"));
builder.Services.Configure<SmsReminderOptions>(builder.Configuration.GetSection(SmsReminderOptions.SectionName));
builder.Services.AddHttpClient("Sms");
builder.Services.AddSingleton<ISmsService, SmsService>();
builder.Services.AddHostedService<SmsReminderService>();

// Pre-visit intake form reminders (Reserved slots → e-mail with a form link)
builder.Services.Configure<IntakeReminderOptions>(
    builder.Configuration.GetSection(IntakeReminderOptions.SectionName));
builder.Services.AddHostedService<IntakeReminderService>();
builder.Services.AddScoped<IExaminationsRepository, ExaminationsRepository>();
builder.Services.AddScoped<IExaminationsService, ExaminationsService>();

// Register photo services
builder.Services.AddScoped<PhotoEncryptionService>();
builder.Services.AddScoped<PatientPhotoService>();

// Antivirus scanning for every uploaded file (clamd INSTREAM).
builder.Services.Configure<ClamAvOptions>(builder.Configuration.GetSection(ClamAvOptions.SectionName));
builder.Services.AddSingleton<IAntivirusScanner, ClamAvScanner>();

// Register document services
builder.Services.AddScoped<DocumentEncryptionService>();
builder.Services.AddScoped<PatientDocumentService>();
builder.Services.AddScoped<ExaminationDocumentService>();

// Periodic cleanup of old activity logs (the table grows without bound)
builder.Services.AddHostedService<ActivityLogRetentionService>();

// One-shot masking of historical PII in e-mail/activity logs.
builder.Services.AddHostedService<PiiLogBackfillService>();

// Purge soft-deleted patient/examination documents after the retention window.
builder.Services.AddHostedService<DocumentRetentionService>();

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

// Must be first: rate limiting, audit and metrics all read the caller IP.
app.UseForwardedHeaders();

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

// Apply database migrations and seed data. Fail fast: serving requests on a
// half-migrated database is worse than letting the container restart.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
    var seedService = scope.ServiceProvider.GetRequiredService<ISeedService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (!await migrationService.CanConnectAsync())
        {
            throw new InvalidOperationException(
                "Database is not reachable during startup; refusing to serve requests.");
        }

        await migrationService.ApplyMigrationsAsync();
        logger.LogInformation("Database ready - migrations applied.");

        await seedService.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database initialization failed; stopping the application.");
        throw;
    }
}

app.Run();

// Make the implicit Program class public so it can be referenced by tests
public partial class Program { }
