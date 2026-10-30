using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using HealthcareAPI.Models;
using HealthcareAPI.Services;
using HealthcareAPI.Tests.Authorization;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace HealthcareAPI.Tests.Services;

public class AuthServiceTests
{
    private const string Secret = "test-secret-key-for-preview-tests-minimum-256-bits-long";

    private static AuthService CreateService(StubHttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:SecretKey"] = Secret,
                ["JWT:Issuer"] = "test-issuer",
                ["JWT:Audience"] = "test-audience"
            })
            .Build();

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(() => new HttpClient(handler) { BaseAddress = new Uri("http://database-api") });

        return new AuthService(configuration, factory.Object);
    }

    private static EmployeeAuthInfo Target(bool active = true) => new()
    {
        EmployeeId = 42,
        PersonId = 7,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Active = active,
        UID = "U42",
        CountryCode = "CZ",
        AccessControlVersion = 3,
        Roles = new List<string> { "Doctor" }
    };

    [Fact]
    public async Task ImpersonateAsync_ActiveTarget_IssuesPreviewTokenWithImpersonatorClaim()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, JsonSerializer.Serialize(Target()));
        var service = CreateService(handler);

        var result = await service.ImpersonateAsync(42, 1, "Admin User");

        Assert.NotNull(result);
        Assert.Equal(42, result!.EmployeeId);
        Assert.Equal("Jane Doe", result.FullName);
        Assert.Equal("Admin User", result.ImpersonatorName);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        // JwtSecurityTokenHandler may map claim types to short JWT names on
        // write, so accept both representations.
        Assert.Equal(
            "42",
            jwt.Claims.Single(c => c.Type is ClaimTypes.NameIdentifier or "nameid").Value);
        Assert.Equal("1", jwt.Claims.Single(c => c.Type == "impersonator_id").Value);
        Assert.Equal("Admin User", jwt.Claims.Single(c => c.Type == "impersonator_name").Value);
        Assert.Contains(jwt.Claims, c => (c.Type is ClaimTypes.Role or "role") && c.Value == "Doctor");
        Assert.Equal("CZ", jwt.Claims.Single(c => c.Type == "country_code").Value);
        Assert.InRange(jwt.ValidTo, DateTime.UtcNow.AddMinutes(50), DateTime.UtcNow.AddMinutes(70));
    }

    [Fact]
    public async Task ImpersonateAsync_InactiveTarget_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, JsonSerializer.Serialize(Target(active: false)));
        var service = CreateService(handler);

        var result = await service.ImpersonateAsync(42, 1, "Admin User");

        Assert.Null(result);
    }

    [Fact]
    public async Task ImpersonateAsync_OwnAccount_ReturnsNullWithoutCallingDatabaseApi()
    {
        var handler = new StubHttpMessageHandler();
        var service = CreateService(handler);

        var result = await service.ImpersonateAsync(1, 1, "Admin User");

        Assert.Null(result);
        Assert.Empty(handler.Requests);
    }
}
