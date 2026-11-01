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
    private static (ServiceProvider Provider, SmsService Service) Build(SmsOptions options)
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<DatabaseContext>(builder =>
            builder.UseInMemoryDatabase(databaseName));
        var provider = services.BuildServiceProvider();

        var service = new SmsService(
            Options.Create(options),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new Mock<IHttpClientFactory>().Object,
            NullLogger<SmsService>.Instance);

        return (provider, service);
    }

    [Fact]
    public async Task LogProvider_WritesLoggedOnlyRow()
    {
        var (provider, service) = Build(new SmsOptions { Provider = "log", Enabled = true });

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
        var (provider, service) = Build(new SmsOptions { Enabled = false });

        await service.SendAsync("+420777123456", "Text", "code");

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var log = await context.SmsLogs.SingleAsync();
        Assert.Equal(EmailLogStatuses.LoggedOnly, log.Status);
        Assert.Equal("SMS is disabled", log.Error);
    }

    [Fact]
    public async Task EmptyRecipient_Throws()
    {
        var (_, service) = Build(new SmsOptions());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendAsync(" ", "Text", "code"));
    }
}
