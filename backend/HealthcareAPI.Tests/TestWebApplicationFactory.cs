using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HealthcareAPI.Tests.Integration;

public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration with JWT secret
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:SecretKey"] = "test-secret-key-for-jwt-testing-minimum-256-bits-long",
                ["JWT:Issuer"] = "test-issuer",
                ["JWT:Audience"] = "test-audience"
            });
        });

        builder.UseEnvironment("Testing");
    }
}
