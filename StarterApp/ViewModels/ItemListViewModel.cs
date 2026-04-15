using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarterApp.Models;
using StarterApp.Services;

namespace StarterApp.ViewModels;

public partial class ItemListViewModel : BaseViewModel
{
    private readonly IItemApiService _itemApiService;
    private readonly INavigationService _navigationService;
    private readonly IAuthenticationService _authService;

    private List<ItemSummaryDto> _allItems = new();

    [ObservableProperty]
    private List<ItemListRow> items = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool showingBrowseListings = true;

    [ObservableProperty]
    private bool showingMyListings;

    public ItemListViewModel(IItemApiService itemApiService, INavigationService navigationService, IAuthenticationService authService)
    {
        _itemApiService = itemApiService;
        _navigationService = navigationService;
        _authService = authService;
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
    private void ShowBrowseListings()
    {
        ShowingBrowseListings = true;
        ShowingMyListings = false;
        ApplyFilters();
    }

    [RelayCommand]
    private void ShowMyListings()
    {
        ShowingBrowseListings = false;
        ShowingMyListings = true;
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
    private async Task RequestItemAsync(ItemListRow? row)
    {
        if (row is null || !row.CanRequest)
        {
            return;
        }

        await _navigationService.NavigateToAsync("RentalRequestPage", new Dictionary<string, object>
        {
            ["itemId"] = row.Id,
            ["itemTitle"] = row.Title
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

            await _authService.InitializeAsync();

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
        var currentUserId = _authService.CurrentUser?.Id;

        if (currentUserId is not null)
        {
            filtered = ShowingMyListings
                ? filtered.Where(i => i.OwnerUserId == currentUserId.Value)
                : filtered.Where(i => i.OwnerUserId != currentUserId.Value);
        }

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
                OwnerName = i.OwnerName,
                CanRequest = currentUserId is not null && i.OwnerUserId != currentUserId.Value
            })
            .ToList();
    }
}
