using StarterApp.Models;

namespace StarterApp.Services;

public interface IRentalApiService
{
    Task<int> CreateRentalRequestAsync(CreateRentalRequestDto request, CancellationToken cancellationToken = default);
    Task<List<RentalRequestSummaryDto>> GetIncomingRequestsAsync(CancellationToken cancellationToken = default);
    Task<List<RentalRequestSummaryDto>> GetOutgoingRequestsAsync(CancellationToken cancellationToken = default);
    Task UpdateRentalRequestStatusAsync(int rentalRequestId, string status, CancellationToken cancellationToken = default);
}
