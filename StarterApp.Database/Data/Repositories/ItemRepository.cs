using Microsoft.EntityFrameworkCore;
using StarterApp.Database.Models;

namespace StarterApp.Database.Data.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly AppDbContext _dbContext;

    public ItemRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Item>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Items
            .Include(i => i.OwnerUser)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Item?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Items
            .Include(i => i.OwnerUser)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<Item> AddAsync(Item entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Items.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Item entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Items.Update(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Item entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Items.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Item>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Items
            .Include(i => i.OwnerUser)
            .Where(i => i.IsActive)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Item>> GetByOwnerAsync(int ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Items
            .Include(i => i.OwnerUser)
            .Where(i => i.OwnerUserId == ownerUserId && i.IsActive)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
