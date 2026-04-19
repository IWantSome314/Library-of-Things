using StarterApp.Database.Models;

namespace StarterApp.Database.Data.Repositories;

public interface IRentalRepository : IRepository<RentalRequest>
{
    Task<List<RentalRequest>> GetIncomingForOwnerAsync(int ownerUserId, CancellationToken cancellationToken = default);
    Task<List<RentalRequest>> GetOutgoingForUserAsync(int requestorUserId, CancellationToken cancellationToken = default);
}
