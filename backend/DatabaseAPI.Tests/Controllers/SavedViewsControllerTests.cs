using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Controllers;

/// <summary>Saved filter views are per-employee, validated and have one default per surface.</summary>
public class SavedViewsControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly SavedViewsController _controller;
    private int? _employeeId = 1;

    public SavedViewsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);

        var actor = new Mock<IActorCountryContextService>();
        actor.Setup(service => service.GetActorEmployeeId()).Returns(() => _employeeId);

        _controller = new SavedViewsController(_context, actor.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<SavedViewsController.SavedViewDto> CreateAsync(
        string name = "Moje pacienti",
        string filters = "{\"search\":\"eva\"}",
        bool isDefault = false)
    {
        var result = await _controller.Create(new SavedViewsController.SaveSavedViewRequest
        {
            Surface = "patients",
            Name = name,
            FiltersJson = filters,
            IsDefault = isDefault
        });
        return Assert.IsType<SavedViewsController.SavedViewDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task CreateAndList_IsScopedToTheActingEmployee()
    {
        await CreateAsync();

        var mine = await _controller.GetAll("patients");
        Assert.Single(Assert.IsType<List<SavedViewsController.SavedViewDto>>(
            Assert.IsType<OkObjectResult>(mine.Result).Value));

        _employeeId = 2;
        var other = await _controller.GetAll("patients");
        Assert.Empty(Assert.IsType<List<SavedViewsController.SavedViewDto>>(
            Assert.IsType<OkObjectResult>(other.Result).Value));
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        await CreateAsync();

        var result = await _controller.Create(new SavedViewsController.SaveSavedViewRequest
        {
            Surface = "patients",
            Name = "Moje pacienti",
            FiltersJson = "{}"
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_UnknownSurface_ReturnsBadRequest()
    {
        var result = await _controller.Create(new SavedViewsController.SaveSavedViewRequest
        {
            Surface = "unknown",
            Name = "Test",
            FiltersJson = "{}"
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_InvalidJson_ReturnsBadRequest()
    {
        var result = await _controller.Create(new SavedViewsController.SaveSavedViewRequest
        {
            Surface = "patients",
            Name = "Test",
            FiltersJson = "not-json"
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetDefault_ClearsThePreviousDefault()
    {
        var first = await CreateAsync("First", isDefault: true);
        var second = await CreateAsync("Second");

        var result = await _controller.SetDefault(second.Id);

        Assert.IsType<NoContentResult>(result);
        var views = await _context.SavedViews.OrderBy(view => view.Id).ToListAsync();
        Assert.False(views.Single(view => view.Id == first.Id).IsDefault);
        Assert.True(views.Single(view => view.Id == second.Id).IsDefault);
    }

    [Fact]
    public async Task Update_ForAnotherEmployee_ReturnsNotFound()
    {
        var created = await CreateAsync();
        _employeeId = 2;

        var result = await _controller.Update(created.Id, new SavedViewsController.UpdateSavedViewRequest
        {
            Name = "Cizi",
            FiltersJson = "{}"
        });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Delete_RemovesOnlyOwnView()
    {
        var created = await CreateAsync();

        Assert.IsType<NoContentResult>(await _controller.Delete(created.Id));
        Assert.Empty(await _context.SavedViews.ToListAsync());
    }

    [Fact]
    public async Task WithoutActor_ReturnsUnauthorized()
    {
        _employeeId = null;

        Assert.IsType<UnauthorizedResult>((await _controller.GetAll("patients")).Result);
    }
}
