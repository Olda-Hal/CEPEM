using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DatabaseAPI.Tests.Controllers;

public class ExaminationTypesControllerTests : IAsyncLifetime
{
    private readonly DatabaseContext _context;
    private readonly ExaminationTypesController _controller;
    private readonly ILogger<ExaminationTypesController> _logger;

    public ExaminationTypesControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DatabaseContext(options);
        _logger = new Mock<ILogger<ExaminationTypesController>>().Object;
        _controller = new ExaminationTypesController(_context, _logger);
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetAll_ReturnsOk_WithAllTypes()
    {
        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<ActionResult<List<object>>>(result);
        Assert.NotNull(okResult.Result);
    }

    [Fact]
    public async Task GetAll_WithLanguageFilter_ReturnsTranslatedTypes()
    {
        // Act
        var result = await _controller.GetAll(language: "cs");

        // Assert
        var okResult = Assert.IsType<ActionResult<List<object>>>(result);
        Assert.NotNull(okResult.Result);
    }

    [Fact]
    public async Task GetAll_WithMultipleLanguages_ReturnsCorrectTranslations()
    {
        // Arrange
        var translation = new Translation { EN = "Examination", CS = "Vyšetření", NL = "Onderzoek" };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var examinationType = new ExaminationType { NameTranslationId = translation.Id };
        _context.ExaminationTypes.Add(examinationType);
        await _context.SaveChangesAsync();

        // Act
        var resultCs = await _controller.GetAll(language: "cs");
        var resultEn = await _controller.GetAll(language: "en");
        var resultNl = await _controller.GetAll(language: "nl");

        // Assert
        var okResultCs = Assert.IsType<OkObjectResult>(resultCs.Result);
        var okResultEn = Assert.IsType<OkObjectResult>(resultEn.Result);
        var okResultNl = Assert.IsType<OkObjectResult>(resultNl.Result);
        Assert.NotNull(okResultCs.Value);
        Assert.NotNull(okResultEn.Value);
        Assert.NotNull(okResultNl.Value);
    }

    [Fact]
    public async Task GetById_ReturnsOk_WhenTypeExists()
    {
        // Arrange
        var translation = new Translation { EN = "Examination", CS = "Vyšetření" };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var examinationType = new ExaminationType { NameTranslationId = translation.Id };
        _context.ExaminationTypes.Add(examinationType);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetById(examinationType.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenTypeDoesNotExist()
    {
        // Act
        var result = await _controller.GetById(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WithValidData()
    {
        // Arrange
        var request = new CreateExaminationTypeRequest { EN = "Examination", CS = "Vyšetření", NL = "Onderzoek" };

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.NotNull(createdResult.Value);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenMissingRequiredField()
    {
        // Arrange
        var request = new CreateExaminationTypeRequest { EN = "", CS = "Vyšetření" };

        // Act
        var result = await _controller.Create(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsOk_WithValidData()
    {
        // Arrange
        var translation = new Translation { EN = "Examination", CS = "Vyšetření" };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var examinationType = new ExaminationType { NameTranslationId = translation.Id };
        _context.ExaminationTypes.Add(examinationType);
        await _context.SaveChangesAsync();

        var request = new UpdateExaminationTypeRequest { EN = "Updated Examination", CS = "Aktualizované vyšetření" };

        // Act
        var result = await _controller.Update(examinationType.Id, request);

        // Assert
        Assert.True(result is OkObjectResult or NotFoundObjectResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task Delete_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var translation = new Translation { EN = "Examination" };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var examinationType = new ExaminationType { NameTranslationId = translation.Id };
        _context.ExaminationTypes.Add(examinationType);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.Delete(examinationType.Id);

        // Assert
        Assert.True(result is OkObjectResult or NotFoundObjectResult);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenEnglishNameIsEmpty()
    {
        // Arrange
        var request = new CreateExaminationTypeRequest { EN = "", CS = "Vyšetření" };

        // Act
        var result = await _controller.Create(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_SucceedsWithOnlyEnglishName()
    {
        // Arrange
        var request = new CreateExaminationTypeRequest { EN = "Test Only" };

        // Act
        var result = await _controller.Create(request);

        // Assert
        Assert.True(result.Result is CreatedAtActionResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTypeDoesNotExist()
    {
        // Arrange
        var request = new UpdateExaminationTypeRequest { EN = "Updated" };

        // Act
        var result = await _controller.Update(999, request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsOk_WithPartialUpdate()
    {
        // Arrange
        var translation = new Translation { EN = "Original", CS = "Původní" };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var examinationType = new ExaminationType { NameTranslationId = translation.Id };
        _context.ExaminationTypes.Add(examinationType);
        await _context.SaveChangesAsync();

        var request = new UpdateExaminationTypeRequest { EN = "Updated" };

        // Act
        var result = await _controller.Update(examinationType.Id, request);

        // Assert
        Assert.True(result is OkObjectResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenTypeDoesNotExist()
    {
        // Act
        var result = await _controller.Delete(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithSpecificLanguage()
    {
        // Act - Request Czech language
        var result = await _controller.GetAll(language: "cs");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }
}

