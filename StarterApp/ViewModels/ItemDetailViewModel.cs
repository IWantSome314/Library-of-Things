using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarterApp.Services;
using System.Collections.ObjectModel;

namespace StarterApp.ViewModels;

[QueryProperty(nameof(ItemId), "itemId")]
public partial class ItemDetailViewModel : BaseViewModel
{
    private static readonly string[] DefaultCategories =
    {
        "Tools",
        "Garden",
        "Home",
        "Electronics",
        "Photography",
        "Sports",
        "Outdoors",
        "Events",
        "DIY",
        "Cleaning"
    };

    private readonly IItemApiService _itemApiService;
    private readonly INavigationService _navigationService;
    private readonly IAuthenticationService _authService;

    public ObservableCollection<string> AvailableCategories { get; } = new(DefaultCategories);

    [ObservableProperty]
    private int itemId = -1;

    [ObservableProperty]
    private string titleText = string.Empty;

    [ObservableProperty]
    private string descriptionText = string.Empty;

    [ObservableProperty]
    private string category = string.Empty;

    [ObservableProperty]
    private string location = string.Empty;

    [ObservableProperty]
    private string dailyRateText = string.Empty;

    [ObservableProperty]
    private string ownerDisplay = string.Empty;

    [ObservableProperty]
    private bool isNewItem;

    [ObservableProperty]
    private bool canEdit;

    [ObservableProperty]
    private bool canRent;

    private ItemDetailDto? _loadedItem;

    public ItemDetailViewModel(IItemApiService itemApiService, INavigationService navigationService, IAuthenticationService authService)
    {
        _itemApiService = itemApiService;
        _navigationService = navigationService;
        _authService = authService;
        Title = "Item";
    }

    partial void OnItemIdChanged(int value)
    {
        _ = LoadAsync(value);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await _authService.InitializeAsync();

        if (!_authService.IsAuthenticated || _authService.CurrentUser is null)
        {
            SetError("You must be logged in to create or edit items.");
            return;
        }

        if (!ValidateInput(out var dailyRate, out var latitude, out var longitude))
        {
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            if (IsNewItem)
            {
                var request = new UpsertItemDto
                {
                    Title = TitleText.Trim(),
                    Description = DescriptionText.Trim(),
                    Category = Category.Trim(),
                    Location = Location.Trim(),
                    DailyRate = dailyRate,
                    Latitude = latitude,
                    Longitude = longitude
                };

                ItemId = await _itemApiService.CreateItemAsync(request);
                IsNewItem = false;
                _loadedItem = await _itemApiService.GetItemAsync(ItemId);
            }
            else
            {
                if (_loadedItem is null)
                {
                    SetError("Item could not be loaded.");
                    return;
                }

                if (_loadedItem.OwnerUserId != _authService.CurrentUser.Id)
                {
                    SetError("Only the owner can update this item.");
                    return;
                }

                var request = new UpsertItemDto
                {
                    Title = TitleText.Trim(),
                    Description = DescriptionText.Trim(),
                    Category = Category.Trim(),
                    Location = Location.Trim(),
                    DailyRate = dailyRate,
                    Latitude = latitude,
                    Longitude = longitude
                };

                await _itemApiService.UpdateItemAsync(_loadedItem.Id, request);
            }

            await _navigationService.NavigateToAsync("ItemListPage");
        }
        catch (Exception ex)
        {
            SetError($"Failed to save item: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NavigateBackAsync()
    {
        await _navigationService.NavigateToAsync("ItemListPage");
    }

    [RelayCommand]
    private async Task NavigateToDashboardAsync()
    {
        await _navigationService.NavigateToAsync("MainPage");
    }

    [RelayCommand]
    private async Task RequestToRentAsync()
    {
        if (ItemId <= 0) return;

        await _navigationService.NavigateToAsync("RentalRequestPage", new Dictionary<string, object>
        {
            ["itemId"] = ItemId,
            ["itemTitle"] = TitleText
        });
    }

    private async Task LoadAsync(int id)
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

            if (id <= 0)
            {
                IsNewItem = true;
                CanEdit = _authService.IsAuthenticated;
                OwnerDisplay = _authService.CurrentUser?.FullName ?? "Unknown";
                Title = "Create Item";
                TitleText = string.Empty;
                DescriptionText = string.Empty;
                Category = string.Empty;
                Location = string.Empty;
                DailyRateText = string.Empty;
                _loadedItem = null;
                return;
            }

            var item = await _itemApiService.GetItemAsync(id);

            if (item is null)
            {
                SetError("Item not found.");
                return;
            }

            _loadedItem = item;
            IsNewItem = false;
            Title = "Item Details";
            TitleText = item.Title;
            DescriptionText = item.Description;
            EnsureCategoryOption(item.Category);
            Category = item.Category;
            Location = item.Location;
            DailyRateText = item.DailyRate.ToString("0.00", CultureInfo.InvariantCulture);
            OwnerDisplay = item.OwnerName;
            CanEdit = _authService.CurrentUser is not null && _authService.CurrentUser.Id == item.OwnerUserId;
            CanRent = _authService.CurrentUser is not null && _authService.CurrentUser.Id != item.OwnerUserId && !IsNewItem;
        }
        catch (Exception ex)
        {
            SetError($"Failed to load item: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ValidateInput(out decimal dailyRate, out double? latitude, out double? longitude)
    {
        dailyRate = 0m;
        latitude = null;
        longitude = null;

        if (!CanEdit)
        {
            SetError("You do not have permission to edit this item.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TitleText) || string.IsNullOrWhiteSpace(DescriptionText) ||
            string.IsNullOrWhiteSpace(Category) || string.IsNullOrWhiteSpace(Location))
        {
            SetError("Title, description, category, and location are required.");
            return false;
        }

        if (!decimal.TryParse(DailyRateText, NumberStyles.Number, CultureInfo.InvariantCulture, out dailyRate) || dailyRate <= 0)
        {
            SetError("Daily rate must be a number greater than 0 (use dot for decimals). ");
            return false;
        }

        return true;
    }

    private void EnsureCategoryOption(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return;
        }

        if (!AvailableCategories.Contains(categoryName))
        {
            AvailableCategories.Add(categoryName);
        }
    }
}
