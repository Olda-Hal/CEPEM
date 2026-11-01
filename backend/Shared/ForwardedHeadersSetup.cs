using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cepem.Shared;

/// <summary>
/// Shared reverse-proxy header configuration. Both APIs run behind nginx
/// (directly or through the Docker network), so they must read the caller's
/// address/proto from X-Forwarded-*; otherwise every request looks like it
/// came from the proxy and rate limiting/audit IPs are wrong.
/// </summary>
public static class ForwardedHeadersSetup
{
    // Loopback + Docker/private ranges. A client-supplied X-Forwarded-For is
    // appended by nginx, so ForwardLimit=1 picks the hop added by our proxy.
    private static readonly string[] DefaultNetworks =
    {
        "127.0.0.1/32",
        "::1/128",
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16"
    };

    public static IServiceCollection AddCepemForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;

            // Do not rely on the framework's loopback-only list: the proxy is
            // the nginx container (private Docker range).
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            var networks = (configuration["ForwardedHeaders:KnownNetworks"]
                            ?? string.Join(',', DefaultNetworks))
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var network in networks)
            {
                var (prefix, prefixLength) = ParseCidr(network);
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
            }

            var proxies = (configuration["ForwardedHeaders:KnownProxies"] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });

        return services;
    }

    internal static (IPAddress Prefix, int PrefixLength) ParseCidr(string value)
    {
        var parts = value.Split('/', 2);
        var address = IPAddress.Parse(parts[0].Trim());
        if (parts.Length == 2)
        {
            return (address, int.Parse(parts[1].Trim()));
        }

        return (address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
    }
}
