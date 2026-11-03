using System.Net;
using System.Text;
using HealthcareAPI.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HealthcareAPI.Tests.Services;

public class TokenValidityServiceTests
{
    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public int Calls { get; private set; }

        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name)
        {
            Calls++;
            return new HttpClient(_handler, disposeHandler: false)
            {
                BaseAddress = new Uri("http://database-api/")
            };
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public int Requests { get; private set; }

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static TokenValidityService CreateService(
        StubHandler handler, out StubHttpClientFactory factory)
    {
        factory = new StubHttpClientFactory(handler);
        return new TokenValidityService(
            factory,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<TokenValidityService>.Instance);
    }

    [Fact]
    public async Task ValidToken_IsAcceptedAndCached()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"valid\":true}");
        var service = CreateService(handler, out _);

        Assert.True(await service.IsTokenValidAsync(7, 2));
        Assert.True(await service.IsTokenValidAsync(7, 2));

        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task RevokedToken_IsRejected()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"valid\":false}");
        var service = CreateService(handler, out _);

        Assert.False(await service.IsTokenValidAsync(7, 2));
    }

    [Fact]
    public async Task ServiceFailure_FailsClosed()
    {
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, "down");
        var service = CreateService(handler, out _);

        Assert.False(await service.IsTokenValidAsync(7, 2));
    }
}
