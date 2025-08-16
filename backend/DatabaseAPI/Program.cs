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
    ?? "Server=mysql;Database=cepem_healthcare;User=root;Password=root123;";

builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Register migration service
builder.Services.AddScoped<IMigrationService, MigrationService>();

// Register authentication service
builder.Services.AddScoped<IEmployeeAuthService, EmployeeAuthService>();

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://frontend:3000")
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

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
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
