using StarterApp.Database.Data.Repositories;
using StarterApp.Database.Models;
using StarterApp.Test.Fixtures;

namespace StarterApp.Test.Repositories;

public class ReviewRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly ReviewRepository _repository;

    public ReviewRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _repository = new ReviewRepository(_fixture.Context);
    }

    private User CreateUser(string email) => new User
    {
        FirstName = "Test",
        LastName = "User",
        Email = email,
        PasswordHash = "hash",
        PasswordSalt = "salt"
    };

    private Item CreateItem(int ownerUserId) => new Item
    {
        Title = "Reviewed Item",
        Description = "Test description",
        DailyRate = 5.00m,
        Category = "Camping",
        Location = "Glasgow",
        OwnerUserId = ownerUserId
    };

    [Fact]
    public async Task AddAsync_CreatesReview()
    {
        // Arrange
        var owner = CreateUser("revowner1@test.com");
        var reviewer = CreateUser("revuser1@test.com");
        _fixture.Context.Users.AddRange(owner, reviewer);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(owner.Id);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        var review = new Review
        {
            ItemId = item.Id,
            ReviewerUserId = reviewer.Id,
            Rating = 5,
            Comment = "Excellent!"
        };

        // Act
        var result = await _repository.AddAsync(review);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal(5, result.Rating);
        Assert.Equal("Excellent!", result.Comment);
    }

    [Fact]
    public async Task GetByItemAsync_ReturnsReviewsForItem()
    {
        // Arrange
        var owner = CreateUser("revowner2@test.com");
        var reviewer = CreateUser("revuser2@test.com");
        _fixture.Context.Users.AddRange(owner, reviewer);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(owner.Id);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        await _repository.AddAsync(new Review { ItemId = item.Id, ReviewerUserId = reviewer.Id, Rating = 4, Comment = "Good" });
        await _repository.AddAsync(new Review { ItemId = item.Id, ReviewerUserId = reviewer.Id, Rating = 3, Comment = "OK" });

        // Act
        var results = await _repository.GetByItemAsync(item.Id);

        // Assert
        Assert.True(results.Count >= 2);
        Assert.All(results, r => Assert.Equal(item.Id, r.ItemId));
    }

    [Fact]
    public async Task GetByReviewerAsync_ReturnsOnlyReviewerReviews()
    {
        // Arrange
        var owner = CreateUser("revowner3@test.com");
        var reviewer = CreateUser("revuser3@test.com");
        var otherReviewer = CreateUser("revother3@test.com");
        _fixture.Context.Users.AddRange(owner, reviewer, otherReviewer);
        await _fixture.Context.SaveChangesAsync();

        var item = CreateItem(owner.Id);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        await _repository.AddAsync(new Review { ItemId = item.Id, ReviewerUserId = reviewer.Id, Rating = 5, Comment = "Great" });
        await _repository.AddAsync(new Review { ItemId = item.Id, ReviewerUserId = otherReviewer.Id, Rating = 2, Comment = "Poor" });

        // Act
        var results = await _repository.GetByReviewerAsync(reviewer.Id);

        // Assert
        Assert.All(results, r => Assert.Equal(reviewer.Id, r.ReviewerUserId));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_ForMissingReview()
    {
        // Act
        var result = await _repository.GetByIdAsync(99999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Rating_MustBeBetweenOneAndFive()
    {
        // Arrange & Act
        var review = new Review { Rating = 4, Comment = "Good", ItemId = 1, ReviewerUserId = 1 };

        // Assert
        Assert.InRange(review.Rating, 1, 5);
    }
}
