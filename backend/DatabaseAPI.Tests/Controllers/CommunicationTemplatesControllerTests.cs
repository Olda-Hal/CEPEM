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
    public async Task Create_CenterScopeResolvesHospitalName()
    {
        var hospital = new Hospital { Name = "CEPEM Praha", CountryScopeId = 203 };
        _context.Hospitals.Add(hospital);
        await _context.SaveChangesAsync();

        var result = await _controller.Create(Request(
            scopeType: CommunicationScopes.Center,
            scopeId: hospital.Id));

        var dto = Assert.IsType<CommunicationTemplateDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("CEPEM Praha", dto.ScopeName);
    }

    [Fact]
    public async Task Create_UnknownCenterReturnsBadRequest()
    {
        var result = await _controller.Create(Request(
            scopeType: CommunicationScopes.Center,
            scopeId: 999));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_SmsWithoutSubjectIsAllowed()
    {
        var result = await _controller.Create(Request(
            channel: CommunicationChannels.Sms,
            subject: null,
            body: "SMS text"));

        var dto = Assert.IsType<CommunicationTemplateDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Null(dto.Subject);
        Assert.Equal(CommunicationChannels.Sms, dto.Channel);
    }

    [Fact]
    public async Task Create_RejectsUnknownChannelAndBadLanguage()
    {
        var badChannel = await _controller.Create(Request(channel: "Push"));
        Assert.IsType<BadRequestObjectResult>(badChannel.Result);

        var badLanguage = await _controller.Create(Request(language: "x"));
        Assert.IsType<BadRequestObjectResult>(badLanguage.Result);
    }

    [Fact]
    public async Task Update_ToDuplicateVariantReturnsConflict()
    {
        var first = Assert.IsType<CommunicationTemplateDto>(
            Assert.IsType<OkObjectResult>((await _controller.Create(Request(language: "cs"))).Result).Value);
        await _controller.Create(Request(language: "en"));

        var result = await _controller.Update(first.Id, Request(language: "en"));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAll_FiltersByScopeAndSearch()
    {
        await _controller.Create(Request(body: "Pripominka vstupniho formularu"));
        await _controller.Create(Request(language: "en", body: "Intake reminder"));

        var bySearch = await _controller.GetAll(search: "vstupniho");
        var searchRows = Assert.IsAssignableFrom<List<CommunicationTemplateDto>>(
            Assert.IsType<OkObjectResult>(bySearch.Result).Value);
        Assert.Single(searchRows);

        var byScope = await _controller.GetAll(scopeType: CommunicationScopes.Global, scopeId: 0);
        var scopeRows = Assert.IsAssignableFrom<List<CommunicationTemplateDto>>(
            Assert.IsType<OkObjectResult>(byScope.Result).Value);
        Assert.Equal(2, scopeRows.Count);
    }

    [Fact]
    public async Task GetSmsLog_ClampsLimit()
    {
        for (var index = 0; index < 3; index++)
        {
            _context.SmsLogs.Add(new SmsLog
            {
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(index),
                ToNumber = $"+420{index}",
                TemplateCode = CommunicationTemplateCodes.AppointmentReminderSms,
                Status = EmailLogStatuses.Sent
            });
        }
        await _context.SaveChangesAsync();

        var result = await _controller.GetSmsLog(limit: 2);

        var rows = Assert.IsAssignableFrom<List<SmsLogDto>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, rows.Count);
        Assert.Equal("+4202", rows[0].ToNumber);
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
