using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DatabaseAPI.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var builder = new DbContextOptionsBuilder<DatabaseContext>();
        
        // Use a design-time connection string that doesn't require MySQL to be running
        // This allows EF commands to work for generating migrations
        var connectionString = "Server=localhost;Port=3306;Database=cepem_healthcare;User=cepem_user;Password=cepem_password;";
        
        // Use a specific MySQL version to avoid auto-detection which requires a connection
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 33));
        builder.UseMySql(connectionString, serverVersion);
        
        return new DatabaseContext(builder.Options);
    }
}
