using Microsoft.EntityFrameworkCore;
using StarterApp.Database.Models;

namespace StarterApp.Database.Data.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly AppDbContext _dbContext;

    public ReviewRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Review>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reviews
            .Include(r => r.Item)
            .Include(r => r.ReviewerUser)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Review?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reviews
            .Include(r => r.Item)
            .Include(r => r.ReviewerUser)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Review> AddAsync(Review entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Reviews.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Review entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Reviews.Update(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Review entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Reviews.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Review>> GetByItemAsync(int itemId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reviews
            .Include(r => r.ReviewerUser)
            .Where(r => r.ItemId == itemId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Review>> GetByReviewerAsync(int reviewerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reviews
            .Include(r => r.Item)
            .Where(r => r.ReviewerUserId == reviewerUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
