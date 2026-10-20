using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HealthcareAPI.Tests.Integration;

public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    public TestWebApplicationFactory()
    {
        // Program.cs validates the JWT settings while it runs (fail fast),
        // before WebApplicationFactory's ConfigureAppConfiguration callbacks
        // are applied - so supply them via environment variables instead.
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "test-secret-key-for-jwt-testing-minimum-256-bits-long");
        Environment.SetEnvironmentVariable("JWT_ISSUER", "test-issuer");
        Environment.SetEnvironmentVariable("JWT_AUDIENCE", "test-audience");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
