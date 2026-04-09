using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarterApp.Services;

namespace StarterApp.ViewModels;

public partial class RentalListViewModel : BaseViewModel
{
    private readonly IItemApiService _itemApiService;
    private readonly IRentalApiService _rentalApiService;
    private readonly INavigationService _navigationService;
    private readonly IAuthenticationService _authenticationService;

    [ObservableProperty]
    private ObservableCollection<ItemSummaryDto> myListings = new();

    [ObservableProperty]
    private ObservableCollection<RentalRequestSummaryDto> pendingRequests = new();

    [ObservableProperty]
    private ObservableCollection<RentalRequestSummaryDto> activeRentals = new();

    [ObservableProperty]
    private bool showMyListings = true;

    [ObservableProperty]
    private bool showRequests;

    [ObservableProperty]
    private bool hasPendingRequests;

    [ObservableProperty]
    private bool hasActiveRentals;

    public RentalListViewModel(
        IItemApiService itemApiService,
        IRentalApiService rentalApiService,
        INavigationService navigationService,
        IAuthenticationService authenticationService)
    {
        _itemApiService = itemApiService;
        _rentalApiService = rentalApiService;
        _navigationService = navigationService;
        _authenticationService = authenticationService;
        Title = "My Rentals";
    }

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private void ToggleView(string view)
    {
        if (view == "listings")
        {
            ShowMyListings = true;
            ShowRequests = false;
        }
        else
        {
            ShowMyListings = false;
            ShowRequests = true;
        }
    }

    [RelayCommand]
    private async Task NavigateToDashboardAsync()
    {
        await _navigationService.NavigateToAsync("MainPage");
    }

    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();
            await _authenticationService.InitializeAsync();

            var currentUserId = _authenticationService.CurrentUser?.Id;
            if (currentUserId is null)
            {
                SetError("You must be logged in to view your listings and requests.");
                MyListings = new ObservableCollection<ItemSummaryDto>();
                PendingRequests = new ObservableCollection<RentalRequestSummaryDto>();
                ActiveRentals = new ObservableCollection<RentalRequestSummaryDto>();
                HasPendingRequests = false;
                HasActiveRentals = false;
                return;
            }

            var allItems = await _itemApiService.GetItemsAsync();
            var incoming = await _rentalApiService.GetIncomingRequestsAsync();

            var ownedItems = allItems
                .Where(item => item.OwnerUserId == currentUserId.Value)
                .OrderByDescending(item => item.Id)
                .ToList();

            var pending = incoming
                .Where(request => string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(request => request.CreatedAtUtc)
                .ToList();

            var active = incoming
                .Where(request => string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(request => request.CreatedAtUtc)
                .ToList();

            MyListings = new ObservableCollection<ItemSummaryDto>(ownedItems);
            PendingRequests = new ObservableCollection<RentalRequestSummaryDto>(pending);
            ActiveRentals = new ObservableCollection<RentalRequestSummaryDto>(active);
            HasPendingRequests = pending.Count > 0;
            HasActiveRentals = active.Count > 0;
        }
        catch (Exception ex)
        {
            SetError($"Failed to load rentals: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApproveRequestAsync(RentalRequestSummaryDto? request)
    {
        await UpdateRequestStatusAsync(request, "Approved", "Rental request approved.");
    }

    [RelayCommand]
    private async Task DenyRequestAsync(RentalRequestSummaryDto? request)
    {
        await UpdateRequestStatusAsync(request, "Denied", "Rental request denied.");
    }

    private async Task UpdateRequestStatusAsync(RentalRequestSummaryDto? request, string status, string successMessage)
    {
        if (request is null || IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            await _rentalApiService.UpdateRentalRequestStatusAsync(request.Id, status);

            var currentPage = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (currentPage is not null)
            {
                await currentPage.DisplayAlert("Success", successMessage, "OK");
            }

            await LoadAsync();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
