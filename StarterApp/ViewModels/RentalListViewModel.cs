using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarterApp.Models;
using StarterApp.Services;

namespace StarterApp.ViewModels;

public partial class RentalListViewModel : BaseViewModel
{
    private readonly IItemApiService _itemApiService;
    private readonly IRentalApiService _rentalApiService;
    private readonly INavigationService _navigationService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserNotificationService _notificationService;

    [ObservableProperty]
    private ObservableCollection<ItemSummaryDto> myListings = new();

    [ObservableProperty]
    private ObservableCollection<RentalRequestSummaryDto> pendingRequests = new();

    [ObservableProperty]
    private ObservableCollection<RentalRequestSummaryDto> activeRentals = new();

    [ObservableProperty]
    private ObservableCollection<RentalRequestSummaryDto> pastRentals = new();

    [ObservableProperty]
    private ObservableCollection<RentalRequestSummaryDto> outgoingRequests = new();

    [ObservableProperty]
    private bool showMyListings = true;

    [ObservableProperty]
    private bool showRequests;

    [ObservableProperty]
    private bool hasPendingRequests;

    [ObservableProperty]
    private bool hasActiveRentals;

    [ObservableProperty]
    private bool hasPastRentals;

    [ObservableProperty]
    private bool hasOutgoingRequests;

    public RentalListViewModel(
        IItemApiService itemApiService,
        IRentalApiService rentalApiService,
        INavigationService navigationService,
        IAuthenticationService authenticationService,
        IUserNotificationService notificationService)
    {
        _itemApiService = itemApiService;
        _rentalApiService = rentalApiService;
        _navigationService = navigationService;
        _authenticationService = authenticationService;
        _notificationService = notificationService;
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

    private async Task LoadAsync(bool allowBusyRefresh = false)
    {
        if (IsBusy && !allowBusyRefresh) return;

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
                PastRentals = new ObservableCollection<RentalRequestSummaryDto>();
                OutgoingRequests = new ObservableCollection<RentalRequestSummaryDto>();
                HasPendingRequests = false;
                HasActiveRentals = false;
                HasPastRentals = false;
                HasOutgoingRequests = false;
                return;
            }

            var allItems = await _itemApiService.GetItemsAsync();
            var incoming = await _rentalApiService.GetIncomingRequestsAsync();
            var outgoing = await _rentalApiService.GetOutgoingRequestsAsync();

            var ownedItems = allItems
                .Where(item => item.OwnerUserId == currentUserId.Value)
                .OrderByDescending(item => item.Id)
                .ToList();

            var pending = incoming
                .Where(request => string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(request => request.CreatedAtUtc)
                .ToList();

            var approvedRentals = incoming
                .Where(request => string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(request => request.CreatedAtUtc)
                .ToList();

            var now = DateTime.UtcNow;
            var active = approvedRentals.Where(r => r.EndDate >= now).ToList();
            var past = approvedRentals.Where(r => r.EndDate < now).ToList();
            var outgoingSorted = outgoing
                .OrderByDescending(request => request.CreatedAtUtc)
                .ToList();

            MyListings = new ObservableCollection<ItemSummaryDto>(ownedItems);
            PendingRequests = new ObservableCollection<RentalRequestSummaryDto>(pending);
            ActiveRentals = new ObservableCollection<RentalRequestSummaryDto>(active);
            PastRentals = new ObservableCollection<RentalRequestSummaryDto>(past);
            OutgoingRequests = new ObservableCollection<RentalRequestSummaryDto>(outgoingSorted);
            HasPendingRequests = pending.Count > 0;
            HasActiveRentals = active.Count > 0;
            HasPastRentals = past.Count > 0;
            HasOutgoingRequests = outgoingSorted.Count > 0;
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

    private void MoveRequestToCorrectSection(RentalRequestSummaryDto request, string status)
    {
        request.Status = status;

        var pendingMatch = PendingRequests.FirstOrDefault(item => item.Id == request.Id);
        if (pendingMatch is not null)
        {
            PendingRequests.Remove(pendingMatch);
        }

        if (string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTime.UtcNow;
            if (request.EndDate < now)
            {
                if (!PastRentals.Any(item => item.Id == request.Id))
                    PastRentals.Insert(0, request);
            }
            else
            {
                if (!ActiveRentals.Any(item => item.Id == request.Id))
                    ActiveRentals.Insert(0, request);
            }
        }

        HasPendingRequests = PendingRequests.Count > 0;
        HasActiveRentals = ActiveRentals.Count > 0;
        HasPastRentals = PastRentals.Count > 0;
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
            MoveRequestToCorrectSection(request, status);

            await _notificationService.ShowAlertAsync("Success", successMessage);

            await LoadAsync(allowBusyRefresh: true);
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
