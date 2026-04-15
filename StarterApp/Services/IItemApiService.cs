using StarterApp.Models;

namespace StarterApp.Services;

public interface IItemApiService
{
    Task<List<ItemSummaryDto>> GetItemsAsync(CancellationToken cancellationToken = default);
    Task<ItemDetailDto?> GetItemAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateItemAsync(UpsertItemDto request, CancellationToken cancellationToken = default);
    Task UpdateItemAsync(int id, UpsertItemDto request, CancellationToken cancellationToken = default);
}
