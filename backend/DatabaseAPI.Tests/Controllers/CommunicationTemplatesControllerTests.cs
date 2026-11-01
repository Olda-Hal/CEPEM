using DatabaseAPI.APIModels;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabaseAPI.Tests.Controllers;

/// <summary>Admin template browser: CRUD, filters, duplicate protection and SMS log.</summary>
public class CommunicationTemplatesControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly CommunicationTemplatesController _controller;

    public CommunicationTemplatesControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _controller = new CommunicationTemplatesController(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private static SaveCommunicationTemplateRequest Request(
        string code = CommunicationTemplateCodes.IntakeReminder,
        string channel = CommunicationChannels.Email,
        string language = "cs",
        string scopeType = CommunicationScopes.Global,
        int scopeId = 0,
        string? subject = "Subject",
        string body = "Body")
    {
        return new SaveCommunicationTemplateRequest
        {
            Code = code,
            Channel = channel,
            Language = language,
            ScopeType = scopeType,
            ScopeId = scopeId,
            Subject = subject,
            Body = body,
            IsActive = true
        };
    }

    [Fact]
    public async Task Create_PersistsTemplateAndReturnsScopeName()
    {
        var result = await _controller.Create(Request());

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<CommunicationTemplateDto>(ok.Value);
        Assert.Equal(CommunicationScopes.Global, dto.ScopeType);
        Assert.Equal("Global", dto.ScopeName);
        Assert.Single(await _context.CommunicationTemplates.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsDuplicateVariant()
    {
        await _controller.Create(Request());

        var second = await _controller.Create(Request());

        Assert.IsType<ConflictObjectResult>(second.Result);
    }

    [Fact]
    public async Task Create_ValidatesEmailSubjectAndScope()
    {
        var noSubject = await _controller.Create(Request(subject: null));
        Assert.IsType<BadRequestObjectResult>(noSubject.Result);

        var badScope = await _controller.Create(Request(scopeType: CommunicationScopes.Global, scopeId: 203));
        Assert.IsType<BadRequestObjectResult>(badScope.Result);

        var missingCountryScope = await _controller.Create(Request(scopeType: CommunicationScopes.Country, scopeId: 0));
        Assert.IsType<BadRequestObjectResult>(missingCountryScope.Result);
    }

    [Fact]
    public async Task GetAll_FiltersByChannelAndLanguage()
    {
        await _controller.Create(Request());
        await _controller.Create(Request(language: "en"));
        await _controller.Create(Request(channel: CommunicationChannels.Sms, subject: null));

        var result = await _controller.GetAll(channel: CommunicationChannels.Sms);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<List<CommunicationTemplateDto>>(ok.Value);
        Assert.Single(rows);
        Assert.Equal(CommunicationChannels.Sms, rows[0].Channel);
    }

    [Fact]
    public async Task Update_ChangesFieldsAndDeleteRemovesTemplate()
    {
        var created = Assert.IsType<CommunicationTemplateDto>(
            Assert.IsType<OkObjectResult>((await _controller.Create(Request())).Result).Value);

        var updatedRequest = Request(body: "Updated body", language: "en");
        var updated = await _controller.Update(created.Id, updatedRequest);

        var ok = Assert.IsType<OkObjectResult>(updated.Result);
        var dto = Assert.IsType<CommunicationTemplateDto>(ok.Value);
        Assert.Equal("Updated body", dto.Body);
        Assert.Equal("en", dto.Language);

        var deleted = await _controller.Delete(created.Id);
        Assert.IsType<NoContentResult>(deleted);
        Assert.Empty(await _context.CommunicationTemplates.ToListAsync());
    }

    [Fact]
    public async Task GetSmsLog_ReturnsNewestFirst()
    {
        _context.SmsLogs.AddRange(
            new SmsLog
            {
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                ToNumber = "+420111",
                TemplateCode = CommunicationTemplateCodes.AppointmentReminderSms,
                Status = EmailLogStatuses.Sent
            },
            new SmsLog
            {
                CreatedAtUtc = DateTime.UtcNow,
                ToNumber = "+420222",
                TemplateCode = CommunicationTemplateCodes.AppointmentReminderSms,
                Status = EmailLogStatuses.Failed,
                Error = "gateway down"
            });
        await _context.SaveChangesAsync();

        var result = await _controller.GetSmsLog();

        var rows = Assert.IsAssignableFrom<List<SmsLogDto>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("+420222", rows[0].ToNumber);
        Assert.Equal("gateway down", rows[0].Error);
    }
}
