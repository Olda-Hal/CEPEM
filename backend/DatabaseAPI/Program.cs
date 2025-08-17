using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
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

// Register authentication service
builder.Services.AddScoped<IEmployeeAuthService, EmployeeAuthService>();

// Register employee management service
builder.Services.AddScoped<IEmployeeManagementService, EmployeeManagementService>();

// JWT Authentication Configuration
var jwtSettings = builder.Configuration.GetSection("JWT");
var secretKey = jwtSettings["SecretKey"] ?? "test-secret-key-for-jwt-testing-minimum-256-bits-long";
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "healthcare-api",
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"] ?? "healthcare-app",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

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
