using Microsoft.EntityFrameworkCore;
using StarterApp.Database.Data;

namespace StarterApp.Test.Fixtures;

/// <summary>
/// Shared database fixture that provides a fresh in-memory EF Core context for each test class.
/// Each test that implements IClassFixture<DatabaseFixture> gets an isolated database.
/// </summary>
public class DatabaseFixture : IDisposable
{
    public AppDbContext Context { get; }

    public DatabaseFixture()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
