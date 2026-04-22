using StarterApp.Database.Data.Repositories;
using StarterApp.Database.Models;
using StarterApp.Test.Fixtures;

namespace StarterApp.Test.Repositories;

public class RentalRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly RentalRepository _repository;

    public RentalRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _repository = new RentalRepository(_fixture.Context);
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
        Description = "Test description",
        DailyRate = 15.00m,
        Category = "Tools",
        Location = "Edinburgh",
        OwnerUserId = ownerUserId
    };

    private RentalRequest CreateRequest(int itemId, int requestorId, string status = "Pending") =>
        new RentalRequest
        {
            ItemId = itemId,
            RequestorUserId = requestorId,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(3),
            Status = status,
            TotalPrice = 30.00m,
            Message = "Please"
        };

    [Fact]
    public async Task AddAsync_CreatesPendingRequest()
    {
        // Arrange
        var owner = CreateUser("rowner1@test.com");
        var requester = CreateUser("rreq1@test.com");
        _fixture.Context.Users.AddRange(owner, requester);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(owner.Id, "Drill");
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        var request = CreateRequest(item.Id, requester.Id);

        // Act
        var result = await _repository.AddAsync(request);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsRequest()
    {
        // Arrange
        var owner = CreateUser("rowner2@test.com");
        var requester = CreateUser("rreq2@test.com");
        _fixture.Context.Users.AddRange(owner, requester);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(owner.Id);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        var request = CreateRequest(item.Id, requester.Id);
        await _repository.AddAsync(request);

        // Act
        var result = await _repository.GetByIdAsync(request.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Id, result.Id);
    }

    [Fact]
    public async Task UpdateAsync_ChangesStatus()
    {
        // Arrange
        var owner = CreateUser("rowner3@test.com");
        var requester = CreateUser("rreq3@test.com");
        _fixture.Context.Users.AddRange(owner, requester);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(owner.Id);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        var request = CreateRequest(item.Id, requester.Id);
        await _repository.AddAsync(request);

        // Act
        request.Status = "Approved";
        await _repository.UpdateAsync(request);
        var updated = await _repository.GetByIdAsync(request.Id);

        // Assert
        Assert.Equal("Approved", updated!.Status);
    }

    [Fact]
    public async Task GetOutgoingForUserAsync_ReturnsOnlyUsersRequests()
    {
        // Arrange
        var owner = CreateUser("rowner4@test.com");
        var requester = CreateUser("rreq4@test.com");
        var other = CreateUser("rother4@test.com");
        _fixture.Context.Users.AddRange(owner, requester, other);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(owner.Id);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        await _repository.AddAsync(CreateRequest(item.Id, requester.Id));
        await _repository.AddAsync(CreateRequest(item.Id, other.Id));

        // Act
        var results = await _repository.GetOutgoingForUserAsync(requester.Id);

        // Assert
        Assert.All(results, r => Assert.Equal(requester.Id, r.RequestorUserId));
    }

    [Fact]
    public async Task GetIncomingForOwnerAsync_ReturnsRequestsForOwnedItems()
    {
        // Arrange
        var owner = CreateUser("rowner5@test.com");
        var requester = CreateUser("rreq5@test.com");
        _fixture.Context.Users.AddRange(owner, requester);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(owner.Id);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        await _repository.AddAsync(CreateRequest(item.Id, requester.Id));

        // Act
        var results = await _repository.GetIncomingForOwnerAsync(owner.Id);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(owner.Id, r.Item.OwnerUserId));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRequests()
    {
        // Arrange
        var owner = CreateUser("rowner6@test.com");
        var requester = CreateUser("rreq6@test.com");
        _fixture.Context.Users.AddRange(owner, requester);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(owner.Id, "Saw");
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        await _repository.AddAsync(CreateRequest(item.Id, requester.Id));

        // Act
        var all = await _repository.GetAllAsync();

        // Assert
        Assert.NotEmpty(all);
        Assert.Contains(all, r => r.ItemId == item.Id && r.RequestorUserId == requester.Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRequestFromDatabase()
    {
        // Arrange
        var owner = CreateUser("rowner7@test.com");
        var requester = CreateUser("rreq7@test.com");
        _fixture.Context.Users.AddRange(owner, requester);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(owner.Id, "Planer");
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        var request = await _repository.AddAsync(CreateRequest(item.Id, requester.Id));

        // Act
        await _repository.DeleteAsync(request);
        var result = await _repository.GetByIdAsync(request.Id);

        // Assert
        Assert.Null(result);
    }
}
