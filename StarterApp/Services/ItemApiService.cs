using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace StarterApp.Services;

public class ItemApiService : IItemApiService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationService _authService;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ItemApiService(HttpClient httpClient, IAuthenticationService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<List<ItemSummaryDto>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _httpClient.GetFromJsonAsync<List<ItemSummaryDto>>("/items", cancellationToken);
        return items ?? new List<ItemSummaryDto>();
    }

    public async Task<ItemDetailDto?> GetItemAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<ItemDetailDto>($"/items/{id}", cancellationToken);
    }

    public async Task<int> CreateItemAsync(UpsertItemDto request, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();

        using var response = await _httpClient.PostAsJsonAsync("/items", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshed = await _authService.ForceRefreshTokenAsync();
            if (refreshed)
            {
                using var retry = await _httpClient.PostAsJsonAsync("/items", request, cancellationToken);
                return await ReadCreateResponseAsync(retry);
            }
        }

        return await ReadCreateResponseAsync(response);
    }

    public async Task UpdateItemAsync(int id, UpsertItemDto request, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();

        using var response = await _httpClient.PutAsJsonAsync($"/items/{id}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshed = await _authService.ForceRefreshTokenAsync();
            if (refreshed)
            {
                using var retry = await _httpClient.PutAsJsonAsync($"/items/{id}", request, cancellationToken);
                await EnsureSuccessWithMessageAsync(retry);
                return;
            }
        }

        await EnsureSuccessWithMessageAsync(response);
    }

    private async Task EnsureAuthenticatedAsync()
    {
        await _authService.InitializeAsync();
        var valid = await _authService.EnsureValidTokenAsync();
        if (!valid)
        {
            throw new InvalidOperationException("You must be logged in to perform this action.");
        }
    }

    private async Task<int> ReadCreateResponseAsync(HttpResponseMessage response)
    {
        await EnsureSuccessWithMessageAsync(response);

        var body = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<CreateItemResponse>(body, _jsonOptions);
        if (payload == null || payload.Id <= 0)
        {
            throw new InvalidOperationException("Item created but response did not include an ID.");
        }

        return payload.Id;
    }

    private static async Task EnsureSuccessWithMessageAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("Only the owner can update this item.");
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException($"API request failed ({(int)response.StatusCode}): {body}");
        }

        throw new InvalidOperationException($"API request failed with status {(int)response.StatusCode}.");
    }

    private sealed class CreateItemResponse
    {
        public int Id { get; set; }
    }
}
