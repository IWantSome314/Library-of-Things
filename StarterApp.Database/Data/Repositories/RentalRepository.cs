using Microsoft.EntityFrameworkCore;
using StarterApp.Database.Models;

namespace StarterApp.Database.Data.Repositories;

public class RentalRepository : IRentalRepository
{
    private readonly AppDbContext _dbContext;

    public RentalRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<RentalRequest>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.RentalRequests
            .Include(r => r.Item)
            .ThenInclude(i => i.OwnerUser)
            .Include(r => r.RequestorUser)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<RentalRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RentalRequests
            .Include(r => r.Item)
            .ThenInclude(i => i.OwnerUser)
            .Include(r => r.RequestorUser)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<RentalRequest> AddAsync(RentalRequest entity, CancellationToken cancellationToken = default)
    {
        _dbContext.RentalRequests.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(RentalRequest entity, CancellationToken cancellationToken = default)
    {
        _dbContext.RentalRequests.Update(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(RentalRequest entity, CancellationToken cancellationToken = default)
    {
        _dbContext.RentalRequests.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<RentalRequest>> GetIncomingForOwnerAsync(int ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RentalRequests
            .Include(r => r.Item)
            .ThenInclude(i => i.OwnerUser)
            .Include(r => r.RequestorUser)
            .Where(r => r.Item.OwnerUserId == ownerUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RentalRequest>> GetOutgoingForUserAsync(int requestorUserId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RentalRequests
            .Include(r => r.Item)
            .ThenInclude(i => i.OwnerUser)
            .Include(r => r.RequestorUser)
            .Where(r => r.RequestorUserId == requestorUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
