using Microsoft.EntityFrameworkCore;
using StarterApp.Database.Data;

namespace StarterApp.Test.Fixtures;

// File purpose:
// Provides an isolated in-memory EF Core context for integration-style repository tests.
/// <summary>
/// Shared database fixture that provides a fresh in-memory EF Core context for each test class.
/// Each test that implements IClassFixture<DatabaseFixture> gets an isolated database.
/// </summary>
public class DatabaseFixture : IDisposable
{
    public AppDbContext Context { get; }

    public DatabaseFixture()
    {
        // A unique DB name per fixture prevents state leakage between test classes.
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
