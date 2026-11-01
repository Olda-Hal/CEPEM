using Cepem.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Both APIs read the client IP from X-Forwarded-* behind nginx; the trusted
/// proxy/networks configuration must come from configuration and default to
/// the loopback + private Docker ranges.
/// </summary>
public class ForwardedHeadersSetupTests
{
    private static ForwardedHeadersOptions BuildOptions(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddCepemForwardedHeaders(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }

    [Fact]
    public void UsesConfiguredLimitNetworksAndProxies()
    {
        var options = BuildOptions(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:ForwardLimit"] = "2",
            ["ForwardedHeaders:KnownNetworks"] = "10.0.0.0/8, 192.168.0.0/16, ::1/128",
            ["ForwardedHeaders:KnownProxies"] = "127.0.0.1, 172.18.0.1"
        });

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(2, options.ForwardLimit);
        Assert.Equal(3, options.KnownNetworks.Count);
        Assert.Contains(options.KnownNetworks, network => network.PrefixLength == 8);
        Assert.Contains(options.KnownNetworks, network => network.PrefixLength == 16);
        Assert.Contains(options.KnownNetworks, network => network.PrefixLength == 128);
        Assert.Equal(2, options.KnownProxies.Count);
    }

    [Fact]
    public void DefaultsToLoopbackAndPrivateDockerRanges()
    {
        var options = BuildOptions(new Dictionary<string, string?>());

        Assert.Equal(1, options.ForwardLimit);
        Assert.Contains(options.KnownNetworks, network => network.Prefix.Equals(System.Net.IPAddress.Loopback));
        Assert.Contains(options.KnownNetworks, network => network.PrefixLength == 8);
        Assert.Contains(options.KnownNetworks, network => network.PrefixLength == 12);
        Assert.Contains(options.KnownNetworks, network => network.PrefixLength == 16);
        Assert.Empty(options.KnownProxies);
    }
}
