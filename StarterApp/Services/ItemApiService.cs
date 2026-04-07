using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace StarterApp.Services;

public class ItemApiService : IItemApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    // The injected HttpClient ("ApiClient") automatically handles JWT authentication 
    // via the AuthenticationInterceptor configured in MauiProgram.cs.
    // No manual token validation or HTTP 401 retries are required here.
    public ItemApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
        using var response = await _httpClient.PostAsJsonAsync("/items", request, cancellationToken);
        return await ReadCreateResponseAsync(response);
    }

    public async Task UpdateItemAsync(int id, UpsertItemDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync($"/items/{id}", request, cancellationToken);
        await EnsureSuccessWithMessageAsync(response);
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

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("You must be logged in and authorized to perform this action.");
        }

        var body = await response.Content.ReadAsStringAsync();
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
