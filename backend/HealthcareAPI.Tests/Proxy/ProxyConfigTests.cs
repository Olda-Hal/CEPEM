using HealthcareAPI.Proxy;
using Xunit;

namespace HealthcareAPI.Tests.Proxy;

/// <summary>
/// Route authorization names must match policies registered in Program.cs.
/// "authenticated" is a policy name, not a permission code.
/// </summary>
public class ProxyConfigTests
{
    [Fact]
    public void SavedViewsRoutes_UseAuthenticatedPolicy()
    {
        var savedViews = ProxyConfig.Routes
            .Where(route => route.Match.Path.StartsWith("/api/savedviews", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(savedViews);
        Assert.All(savedViews, route => Assert.Equal("authenticated", route.AuthorizationPolicy));
    }

    [Fact]
    public void PermissionRoutes_UsePermPrefixAndNeverAuthenticatedCode()
    {
        var permissionRoutes = ProxyConfig.Routes
            .Where(route => route.AuthorizationPolicy?.StartsWith("perm:", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(permissionRoutes);
        Assert.DoesNotContain(permissionRoutes, route => route.AuthorizationPolicy == "perm:authenticated");
        Assert.All(permissionRoutes, route => Assert.True(route.AuthorizationPolicy!.Length > "perm:".Length));
    }

    [Fact]
    public void EveryRouteWithoutPolicyOrAuthenticatedHasPermissionPolicy()
    {
        var invalid = ProxyConfig.Routes
            .Where(route =>
                route.AuthorizationPolicy != null &&
                route.AuthorizationPolicy != "authenticated" &&
                !route.AuthorizationPolicy.StartsWith("perm:", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(invalid);
    }
}
