using StarterApp.Database.Models;

namespace StarterApp.Database.Data.Repositories;

public interface IReviewRepository : IRepository<Review>
{
    Task<List<Review>> GetByItemAsync(int itemId, CancellationToken cancellationToken = default);
    Task<List<Review>> GetByReviewerAsync(int reviewerUserId, CancellationToken cancellationToken = default);
}
