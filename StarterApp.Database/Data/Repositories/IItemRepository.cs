using StarterApp.Database.Models;

namespace StarterApp.Database.Data.Repositories;

public interface IItemRepository : IRepository<Item>
{
    Task<List<Item>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<List<Item>> GetByOwnerAsync(int ownerUserId, CancellationToken cancellationToken = default);
}
