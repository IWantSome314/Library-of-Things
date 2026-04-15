using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace StarterApp.Services;

public class RentalApiService : IRentalApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public RentalApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<int> CreateRentalRequestAsync(CreateRentalRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("/rentals", request, cancellationToken);
        await EnsureSuccessWithMessageAsync(response);

        var body = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<CreateRentalResponse>(body, _jsonOptions);
        if (payload == null || payload.Id <= 0)
        {
            throw new InvalidOperationException("Rental created but response did not include an ID.");
        }

        return payload.Id;
    }

    public async Task<List<RentalRequestSummaryDto>> GetIncomingRequestsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _httpClient.GetFromJsonAsync<List<RentalRequestSummaryDto>>("/rentals/incoming", cancellationToken);
        return items ?? new List<RentalRequestSummaryDto>();
    }

    public async Task<List<RentalRequestSummaryDto>> GetOutgoingRequestsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _httpClient.GetFromJsonAsync<List<RentalRequestSummaryDto>>("/rentals/outgoing", cancellationToken);
        return items ?? new List<RentalRequestSummaryDto>();
    }

    public async Task UpdateRentalRequestStatusAsync(int rentalRequestId, string status, CancellationToken cancellationToken = default)
    {
        var normalizedStatus = status.Trim().ToLowerInvariant();
        var path = normalizedStatus switch
        {
            "approved" => $"/rentals/{rentalRequestId}/approve",
            "denied" => $"/rentals/{rentalRequestId}/deny",
            _ => throw new ArgumentOutOfRangeException(nameof(status), "Status must be Approved or Denied.")
        };

        using var response = await _httpClient.PostAsync(path, content: null, cancellationToken);
        await EnsureSuccessWithMessageAsync(response);
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

    private sealed class CreateRentalResponse
    {
        public int Id { get; set; }
    }
}
