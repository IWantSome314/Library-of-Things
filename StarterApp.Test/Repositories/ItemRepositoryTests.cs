using StarterApp.Database.Data.Repositories;
using StarterApp.Database.Models;
using StarterApp.Test.Fixtures;

namespace StarterApp.Test.Repositories;

public class ItemRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly ItemRepository _repository;

    public ItemRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _repository = new ItemRepository(_fixture.Context);
    }

    private User CreateUser(string email) => new User
    {
        FirstName = "Test",
        LastName = "User",
        Email = email,
        PasswordHash = "hash",
        PasswordSalt = "salt"
    };

    private Item CreateItem(int ownerUserId, string title = "Test Item") => new Item
    {
        Title = title,
        Description = "A test item",
        DailyRate = 10.00m,
        Category = "Tools",
        Location = "Edinburgh",
        OwnerUserId = ownerUserId
    };

    [Fact]
    public async Task AddAsync_AddsItemToDatabase()
    {
        // Arrange
        var user = CreateUser("owner1@test.com");
        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(user.Id, "Drill");

        // Act
        var result = await _repository.AddAsync(item);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal("Drill", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectItem()
    {
        // Arrange
        var user = CreateUser("owner2@test.com");
        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(user.Id, "Ladder");
        await _repository.AddAsync(item);

        // Act
        var result = await _repository.GetByIdAsync(item.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Ladder", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenItemNotFound()
    {
        // Act
        var result = await _repository.GetByIdAsync(99999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllItems()
    {
        // Arrange
        var user = CreateUser("owner3@test.com");
        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        await _repository.AddAsync(CreateItem(user.Id, "Saw"));
        await _repository.AddAsync(CreateItem(user.Id, "Hammer"));

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.True(result.Count >= 2);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesItemInDatabase()
    {
        // Arrange
        var user = CreateUser("owner4@test.com");
        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(user.Id, "Spanner");
        await _repository.AddAsync(item);

        // Act
        item.Title = "Updated Spanner";
        await _repository.UpdateAsync(item);

        var updated = await _repository.GetByIdAsync(item.Id);

        // Assert
        Assert.Equal("Updated Spanner", updated!.Title);
    }

    [Fact]
    public async Task DeleteAsync_RemovesItemFromDatabase()
    {
        // Arrange
        var user = CreateUser("owner5@test.com");
        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(user.Id, "Chisel");
        await _repository.AddAsync(item);

        // Act
        await _repository.DeleteAsync(item);

        var result = await _repository.GetByIdAsync(item.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByOwnerAsync_ReturnsOnlyOwnerItems()
    {
        // Arrange
        var owner = CreateUser("owner6@test.com");
        var other = CreateUser("other6@test.com");
        _fixture.Context.Users.AddRange(owner, other);
        await _fixture.Context.SaveChangesAsync();

        await _repository.AddAsync(CreateItem(owner.Id, "Owner Item 1"));
        await _repository.AddAsync(CreateItem(owner.Id, "Owner Item 2"));
        await _repository.AddAsync(CreateItem(other.Id, "Other Item"));

        // Act
        var results = await _repository.GetByOwnerAsync(owner.Id);

        // Assert
        Assert.All(results, item => Assert.Equal(owner.Id, item.OwnerUserId));
        Assert.True(results.Count >= 2);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveItems()
    {
        // Arrange
        var user = CreateUser("owner7@test.com");
        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        var activeItem = CreateItem(user.Id, "Active Item");
        var inactiveItem = CreateItem(user.Id, "Inactive Item");
        inactiveItem.IsActive = false;

        _fixture.Context.Items.AddRange(activeItem, inactiveItem);
        await _fixture.Context.SaveChangesAsync();

        // Act
        var results = await _repository.GetActiveAsync();

        // Assert
        Assert.Contains(results, i => i.Id == activeItem.Id);
        Assert.DoesNotContain(results, i => i.Id == inactiveItem.Id);
    }
}
