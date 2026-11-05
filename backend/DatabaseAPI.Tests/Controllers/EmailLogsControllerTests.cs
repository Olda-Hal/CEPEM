using System.Text;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabaseAPI.Tests.Controllers;

/// <summary>Admin e-mail log: filters, newest-first paging and CSV export.</summary>
public class EmailLogsControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly EmailLogsController _controller;

    public EmailLogsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _controller = new EmailLogsController(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private EmailLog Seed(
        string to,
        string subject,
        string kind,
        string status,
        DateTime createdAt,
        string? error = null,
        string? recipientName = null)
    {
        var log = new EmailLog
        {
            ToAddress = to,
            Subject = subject,
            Kind = kind,
            Status = status,
            CreatedAtUtc = createdAt,
            Error = error,
            RecipientName = recipientName
        };
        _context.EmailLogs.Add(log);
        _context.SaveChanges();
        return log;
    }

    [Fact]
    public async Task GetLogs_FiltersByKindAndStatus()
    {
        Seed("a@example.com", "reset", EmailLogKinds.PasswordReset, EmailLogStatuses.Sent, DateTime.UtcNow);
        Seed("b@example.com", "reminder", EmailLogKinds.IntakeReminder, EmailLogStatuses.Failed, DateTime.UtcNow);

        var result = await _controller.GetLogs(kind: EmailLogKinds.IntakeReminder);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<EmailLogsController.EmailLogListResponse>(ok.Value);
        Assert.Equal("***@example.com", Assert.Single(response.Logs).ToAddress);

        var failed = await _controller.GetLogs(status: EmailLogStatuses.Failed);
        var failedResponse = Assert.IsType<EmailLogsController.EmailLogListResponse>(
            Assert.IsType<OkObjectResult>(failed.Result).Value);
        Assert.Single(failedResponse.Logs);
    }

    [Fact]
    public async Task GetLogs_SearchesRecipientSubjectAndName()
    {
        Seed("eva@example.com", "reset", EmailLogKinds.PasswordReset, EmailLogStatuses.Sent, DateTime.UtcNow, recipientName: "Eva Kralova");
        Seed("jan@example.com", "reminder", EmailLogKinds.IntakeReminder, EmailLogStatuses.Sent, DateTime.UtcNow, recipientName: "Jan Novak");

        var byName = await _controller.GetLogs(search: "Kralova");
        var byNameResponse = Assert.IsType<EmailLogsController.EmailLogListResponse>(
            Assert.IsType<OkObjectResult>(byName.Result).Value);
        Assert.Equal("e***@example.com", Assert.Single(byNameResponse.Logs).ToAddress);

        var bySubject = await _controller.GetLogs(search: "reminder");
        var bySubjectResponse = Assert.IsType<EmailLogsController.EmailLogListResponse>(
            Assert.IsType<OkObjectResult>(bySubject.Result).Value);
        Assert.Equal("j***@example.com", Assert.Single(bySubjectResponse.Logs).ToAddress);
    }

    [Fact]
    public async Task GetLogs_ReturnsNewestFirstWithPaging()
    {
        var oldest = Seed("old@example.com", "old", EmailLogKinds.PasswordReset, EmailLogStatuses.Sent, DateTime.UtcNow.AddHours(-3));
        var middle = Seed("mid@example.com", "mid", EmailLogKinds.PasswordReset, EmailLogStatuses.Sent, DateTime.UtcNow.AddHours(-2));
        var newest = Seed("new@example.com", "new", EmailLogKinds.PasswordReset, EmailLogStatuses.Sent, DateTime.UtcNow.AddHours(-1));

        var firstPage = await _controller.GetLogs(page: 0, limit: 2);
        var firstResponse = Assert.IsType<EmailLogsController.EmailLogListResponse>(
            Assert.IsType<OkObjectResult>(firstPage.Result).Value);
        Assert.Equal(new[] { newest.Id, middle.Id }, firstResponse.Logs.Select(log => log.Id));
        Assert.True(firstResponse.HasMore);
        Assert.Equal(3, firstResponse.TotalCount);

        var secondPage = await _controller.GetLogs(page: 1, limit: 2);
        var secondResponse = Assert.IsType<EmailLogsController.EmailLogListResponse>(
            Assert.IsType<OkObjectResult>(secondPage.Result).Value);
        Assert.Equal(oldest.Id, Assert.Single(secondResponse.Logs).Id);
        Assert.False(secondResponse.HasMore);
    }

    [Fact]
    public async Task ExportCsv_ContainsMatchingRows()
    {
        Seed("eva@example.com", "reset", EmailLogKinds.PasswordReset, EmailLogStatuses.Sent, DateTime.UtcNow, recipientName: "Eva Kralova");
        Seed("jan@example.com", "reminder", EmailLogKinds.IntakeReminder, EmailLogStatuses.Failed, DateTime.UtcNow, error: "timeout");

        var result = await _controller.ExportCsv(kind: EmailLogKinds.IntakeReminder);

        var file = Assert.IsType<FileContentResult>(result);
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("j***@example.com", csv);
        Assert.Contains("timeout", csv);
        Assert.DoesNotContain("eva@example.com", csv);
    }
}
