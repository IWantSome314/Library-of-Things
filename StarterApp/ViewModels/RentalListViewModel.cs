using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarterApp.Services;

namespace StarterApp.ViewModels;

public partial class RentalListViewModel : BaseViewModel
{
    private readonly IRentalApiService _rentalApiService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<RentalRequestSummaryDto> incomingRequests = new();

    [ObservableProperty]
    private ObservableCollection<RentalRequestSummaryDto> outgoingRequests = new();

    [ObservableProperty]
    private bool showIncoming = true;

    [ObservableProperty]
    private bool showOutgoing = false;

    public RentalListViewModel(IRentalApiService rentalApiService, INavigationService navigationService)
    {
        _rentalApiService = rentalApiService;
        _navigationService = navigationService;
        Title = "Rental Requests";
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
        if (view == "incoming")
        {
            ShowIncoming = true;
            ShowOutgoing = false;
        }
        else
        {
            ShowIncoming = false;
            ShowOutgoing = true;
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

            var incoming = await _rentalApiService.GetIncomingRequestsAsync();
            var outgoing = await _rentalApiService.GetOutgoingRequestsAsync();

            IncomingRequests = new ObservableCollection<RentalRequestSummaryDto>(incoming);
            OutgoingRequests = new ObservableCollection<RentalRequestSummaryDto>(outgoing);
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
}
