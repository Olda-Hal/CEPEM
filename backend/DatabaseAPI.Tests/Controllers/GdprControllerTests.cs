using DatabaseAPI.APIModels;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Controllers;

/// <summary>Consent template registry and per-patient consent records.</summary>
public class GdprControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly GdprController _controller;

    public GdprControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);

        var actor = new Mock<IActorCountryContextService>();
        actor.Setup(service => service.GetActorEmployeeId()).Returns(1);

        _controller = new GdprController(
            _context,
            new Mock<IGdprService>().Object,
            actor.Object,
            NullLogger<GdprController>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<ConsentTemplateDto> CreateTemplateAsync(
        string type = "gdpr", string version = "1", string language = "cs")
    {
        var result = await _controller.CreateTemplate(new SaveConsentTemplateRequest
        {
            ConsentType = type,
            Version = version,
            Language = language,
            Title = "Souhlas",
            Text = "Text souhlasu",
            IsActive = true
        });
        return Assert.IsType<ConsentTemplateDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Template_CreateListUpdateDelete_Roundtrip()
    {
        var created = await CreateTemplateAsync();

        var list = await _controller.GetTemplates();
        Assert.Single(Assert.IsType<List<ConsentTemplateDto>>(
            Assert.IsType<OkObjectResult>(list.Result).Value));

        var updated = await _controller.UpdateTemplate(created.Id, new SaveConsentTemplateRequest
        {
            ConsentType = "gdpr",
            Version = "2",
            Language = "cs",
            Title = "Souhlas v2",
            Text = "Nový text",
            IsActive = true
        });
        var updatedDto = Assert.IsType<ConsentTemplateDto>(
            Assert.IsType<OkObjectResult>(updated.Result).Value);
        Assert.Equal("2", updatedDto.Version);
        Assert.Equal("Nový text", updatedDto.Text);

        Assert.IsType<NoContentResult>(await _controller.DeleteTemplate(created.Id));
        Assert.Empty(await _context.ConsentTemplates.ToListAsync());
    }

    [Fact]
    public async Task Template_DuplicateTypeVersionLanguage_ReturnsConflict()
    {
        await CreateTemplateAsync();

        var result = await _controller.CreateTemplate(new SaveConsentTemplateRequest
        {
            ConsentType = "gdpr",
            Version = "1",
            Language = "cs",
            Title = "Duplicitní",
            Text = "Text"
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Template_WithEmptyText_ReturnsBadRequest()
    {
        var result = await _controller.CreateTemplate(new SaveConsentTemplateRequest
        {
            ConsentType = "gdpr",
            Version = "1",
            Language = "cs",
            Title = "Bez textu",
            Text = ""
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RecordConsent_AndList()
    {
        var patient = new Patient
        {
            Person = new Person
            {
                FirstName = "Eva", LastName = "Test", UID = "1", Gender = "F", CountryCode = "CZ", Active = true
            },
            BirthDate = new DateTime(1990, 1, 1),
            CountryCode = "CZ"
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        var recorded = await _controller.RecordConsent(patient.Id, new RecordConsentRequest
        {
            ConsentType = "gdpr",
            Version = "1",
            Language = "cs",
            Source = "Paper"
        });

        var dto = Assert.IsType<PatientConsentDto>(
            Assert.IsType<OkObjectResult>(recorded.Result).Value);
        Assert.Equal("Paper", dto.Source);

        var list = await _controller.GetConsents(patient.Id);
        Assert.Single(Assert.IsType<List<PatientConsentDto>>(
            Assert.IsType<OkObjectResult>(list.Result).Value));
    }

    [Fact]
    public async Task RecordConsent_ForUnknownPatient_ReturnsNotFound()
    {
        var result = await _controller.RecordConsent(999, new RecordConsentRequest());

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task PublicCurrent_ReturnsActiveVersion_AndAvailableLanguages()
    {
        await CreateTemplateAsync(version: "1", language: "cs");
        await CreateTemplateAsync(version: "2", language: "en");

        var result = await _controller.GetPublicCurrent("en");

        var document = Assert.IsType<PublicGdprDocumentDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(document.Available);
        Assert.Equal("2", document.Version);
        Assert.Equal("en", document.Language);
        Assert.Equal("Souhlas", document.Title);
        Assert.Equal(new[] { "cs", "en" }, document.AvailableLanguages);
    }

    [Fact]
    public async Task PublicCurrent_WhenLanguageNotPublished_ReturnsUnavailable()
    {
        await CreateTemplateAsync(version: "1", language: "cs");

        var result = await _controller.GetPublicCurrent("nl");

        var document = Assert.IsType<PublicGdprDocumentDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(document.Available);
        Assert.Equal(new[] { "cs" }, document.AvailableLanguages);
    }

    [Fact]
    public async Task ConsentRegistry_ReturnsPatientIdentityAndVersion()
    {
        var patient = new Patient
        {
            Person = new Person
            {
                FirstName = "Eva", LastName = "Test", UID = "1", Gender = "F", CountryCode = "CZ", Active = true
            },
            BirthDate = new DateTime(1990, 1, 1),
            CountryCode = "CZ"
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        await _controller.RecordConsent(patient.Id, new RecordConsentRequest
        {
            ConsentType = "gdpr",
            Version = "3",
            Language = "nl",
            Source = "IntakeForm"
        });

        var result = await _controller.GetConsentRegistry(consentType: "gdpr", take: 10);

        var rows = Assert.IsType<List<PatientConsentRegistryItemDto>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        var row = Assert.Single(rows);
        Assert.Equal("Eva Test", row.PatientName);
        Assert.Equal("1", row.Uid);
        Assert.Equal("3", row.Version);
        Assert.Equal("nl", row.Language);
    }
}
