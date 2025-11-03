using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=mysql;Port=3306;Database=cepem_healthcare;User=cepem_user;Password=cepem_password;";

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

// Register photo services
builder.Services.AddScoped<PhotoEncryptionService>();
builder.Services.AddScoped<PatientPhotoService>();

// CORS Configuration - Only allow HealthcareAPI to call DatabaseAPI
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowHealthcareAPI",
        policy =>
        {
            policy.WithOrigins("http://healthcare-api:5000", "http://localhost:5000")
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

app.UseCors("AllowHealthcareAPI");
app.MapControllers();

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
