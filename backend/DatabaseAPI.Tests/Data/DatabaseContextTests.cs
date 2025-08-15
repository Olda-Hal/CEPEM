using Microsoft.EntityFrameworkCore;
using Xunit;
using DatabaseAPI.Data;

namespace DatabaseAPI.Tests.Data;

public class DatabaseContextTests
{
    private DbContextOptions<DatabaseContext> GetInMemoryDbOptions()
    {
        return new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public void DatabaseContext_CanBeCreated()
    {
        // Arrange
        var options = GetInMemoryDbOptions();

        // Act & Assert
        using var context = new DatabaseContext(options);
        Assert.NotNull(context);
    }

    [Fact]
    public void DatabaseContext_CanConnect()
    {
        // Arrange
        var options = GetInMemoryDbOptions();

        // Act & Assert
        using var context = new DatabaseContext(options);
        Assert.True(context.Database.CanConnect());
    }

    [Fact]
    public void DatabaseContext_ModelCreatedSuccessfully()
    {
        // Arrange
        var options = GetInMemoryDbOptions();

        // Act & Assert
        using var context = new DatabaseContext(options);
        var model = context.Model;
        Assert.NotNull(model);
    }
}
