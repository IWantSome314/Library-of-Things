using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarterApp.Services;

namespace StarterApp.ViewModels;

public partial class ItemListViewModel : BaseViewModel
{
    private readonly IItemApiService _itemApiService;
    private readonly INavigationService _navigationService;

    private List<ItemSummaryDto> _allItems = new();

    [ObservableProperty]
    private List<ItemListRow> items = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    public ItemListViewModel(IItemApiService itemApiService, INavigationService navigationService)
    {
        _itemApiService = itemApiService;
        _navigationService = navigationService;
        Title = "Items";
    }

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CreateItemAsync()
    {
        await _navigationService.NavigateToAsync("ItemDetailPage", new Dictionary<string, object>
        {
            ["itemId"] = 0
        });
    }

    [RelayCommand]
    private async Task OpenItemAsync(ItemListRow? row)
    {
        if (row is null)
        {
            return;
        }

        await _navigationService.NavigateToAsync("ItemDetailPage", new Dictionary<string, object>
        {
            ["itemId"] = row.Id
        });
    }

    [RelayCommand]
    private async Task NavigateToDashboardAsync()
    {
        await _navigationService.NavigateToAsync("MainPage");
    }

    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            _allItems = await _itemApiService.GetItemsAsync();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            SetError($"Failed to load items: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim().ToLowerInvariant();
            filtered = filtered.Where(i =>
                i.Title.ToLowerInvariant().Contains(term) ||
                i.Category.ToLowerInvariant().Contains(term) ||
                i.Location.ToLowerInvariant().Contains(term));
        }

        Items = filtered
            .Select(i => new ItemListRow
            {
                Id = i.Id,
                Title = i.Title,
                Category = i.Category,
                DailyRate = i.DailyRate,
                Location = i.Location,
                OwnerName = i.OwnerName
            })
            .ToList();
    }
}

public sealed class ItemListRow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal DailyRate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
}
