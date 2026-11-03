using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>The writer persists one metadata row per send attempt.</summary>
public class EmailLogWriterTests : IDisposable
{
    private readonly ServiceProvider _provider;

    public EmailLogWriterTests()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<DatabaseContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        _provider = services.BuildServiceProvider();
    }

    public void Dispose() => _provider.Dispose();

    private EmailLogWriter CreateWriter() =>
        new(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailLogWriter>.Instance);

    [Fact]
    public async Task WriteAsync_StoresRecipientKindStatusAndContext()
    {
        var writer = CreateWriter();

        await writer.WriteAsync(
            "eva@example.com",
            "CEPEM – test",
            EmailLogStatuses.Failed,
            "smtp timeout",
            new EmailLogContext(EmailLogKinds.IntakeReminder, 12, "Eva Test", 34));

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var log = await context.EmailLogs.SingleAsync();

        Assert.Equal("e***@example.com", log.ToAddress);
        Assert.Equal("CEPEM – test", log.Subject);
        Assert.Equal(EmailLogKinds.IntakeReminder, log.Kind);
        Assert.Equal(EmailLogStatuses.Failed, log.Status);
        Assert.Equal("smtp timeout", log.Error);
        Assert.Equal(12, log.PersonId);
        Assert.Equal("E*** T***", log.RecipientName);
        Assert.Equal(34, log.ReservationSlotId);
        Assert.True(log.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task WriteAsync_TruncatesOverlongError()
    {
        var writer = CreateWriter();

        await writer.WriteAsync(
            "eva@example.com",
            "subject",
            EmailLogStatuses.Failed,
            new string('x', 5000),
            new EmailLogContext(EmailLogKinds.PasswordReset));

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var log = await context.EmailLogs.SingleAsync();

        Assert.Equal(1000, log.Error!.Length);
    }
}
