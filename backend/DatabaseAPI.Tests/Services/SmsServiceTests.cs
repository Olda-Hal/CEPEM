using System.Net;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// The default provider is log-only: no external call, but every attempt is
/// recorded in the SMS log with enough context for the admin browser.
/// </summary>
public class SmsServiceTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _responseBody;

        public RecordingHandler(HttpStatusCode status = HttpStatusCode.OK, string responseBody = "ok")
        {
            _status = status;
            _responseBody = responseBody;
        }

        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_responseBody)
            };
        }
    }

    private static (ServiceProvider Provider, SmsService Service, RecordingHandler? Handler) Build(
        SmsOptions options, HttpMessageHandler? handler = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<DatabaseContext>(builder =>
            builder.UseInMemoryDatabase(databaseName));
        var provider = services.BuildServiceProvider();

        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(entry => entry.CreateClient("Sms"))
            .Returns(() => handler == null ? new HttpClient() : new HttpClient(handler));

        var service = new SmsService(
            Options.Create(options),
            provider.GetRequiredService<IServiceScopeFactory>(),
            factory.Object,
            NullLogger<SmsService>.Instance);

        return (provider, service, handler as RecordingHandler);
    }

    [Fact]
    public async Task LogProvider_WritesLoggedOnlyRow()
    {
        var (provider, service, _) = Build(new SmsOptions { Provider = "log", Enabled = true });

        await service.SendAsync(
            "+420777123456",
            "Pripominka",
            CommunicationTemplateCodes.AppointmentReminderSms,
            new SmsLogContext(42, "Eva Kralova", 7));

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var log = await context.SmsLogs.SingleAsync();

        Assert.Equal("+420777123456", log.ToNumber);
        Assert.Equal(CommunicationTemplateCodes.AppointmentReminderSms, log.TemplateCode);
        Assert.Equal(EmailLogStatuses.LoggedOnly, log.Status);
        Assert.Equal(42, log.PersonId);
        Assert.Equal("Eva Kralova", log.RecipientName);
        Assert.Equal(7, log.ReservationSlotId);
    }

    [Fact]
    public async Task DisabledProvider_LogsAndDoesNotThrow()
    {
        var (provider, service, _) = Build(new SmsOptions { Enabled = false });

        await service.SendAsync("+420777123456", "Text", "code");

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var log = await context.SmsLogs.SingleAsync();
        Assert.Equal(EmailLogStatuses.LoggedOnly, log.Status);
        Assert.Equal("SMS is disabled", log.Error);
    }

    [Fact]
    public async Task HttpProvider_PostsJsonAndLogsSent()
    {
        var handler = new RecordingHandler();
        var (provider, service, _) = Build(
            new SmsOptions
            {
                Provider = "http",
                Endpoint = "https://sms.example/send",
                Sender = "CEPEM",
                ApiKey = "secret-token"
            },
            handler);

        await service.SendAsync("+420777123456", "Pripominka", "appointment_reminder");

        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("https://sms.example/send", handler.Request?.RequestUri?.ToString());
        using var payload = System.Text.Json.JsonDocument.Parse(handler.RequestBody!);
        var root = payload.RootElement;
        Assert.Equal("+420777123456", root.GetProperty("to").GetString());
        Assert.Equal("Pripominka", root.GetProperty("text").GetString());
        Assert.Equal("CEPEM", root.GetProperty("sender").GetString());
        Assert.Equal(
            "Bearer",
            handler.Request?.Headers.Authorization?.Scheme);
        Assert.Equal(
            "secret-token",
            handler.Request?.Headers.Authorization?.Parameter);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var log = await context.SmsLogs.SingleAsync();
        Assert.Equal(EmailLogStatuses.Sent, log.Status);
        Assert.Null(log.Error);
    }

    [Fact]
    public async Task HttpProvider_NonSuccessLogsFailedAndThrows()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadGateway, "gateway is down");
        var (provider, service, _) = Build(
            new SmsOptions { Provider = "http", Endpoint = "https://sms.example/send" },
            handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync("+420777123456", "Text", "code"));

        Assert.Contains("502", exception.Message);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var log = await context.SmsLogs.SingleAsync();
        Assert.Equal(EmailLogStatuses.Failed, log.Status);
        Assert.Contains("502", log.Error);
    }

    [Fact]
    public async Task EmptyRecipient_Throws()
    {
        var (_, service, _) = Build(new SmsOptions());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendAsync(" ", "Text", "code"));
    }
}
