using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarterApp.Models;
using StarterApp.Services;

namespace StarterApp.ViewModels;

// File purpose:
// Backs the rental request form. Validates dates, submits the request, and handles user feedback.
[QueryProperty(nameof(ItemId), "itemId")]
[QueryProperty(nameof(ItemTitle), "itemTitle")]
public partial class RentalRequestViewModel : BaseViewModel
{
    private readonly IRentalApiService _rentalApiService;
    private readonly INavigationService _navigationService;
    private readonly IUserNotificationService _notificationService;

    [ObservableProperty]
    private int itemId;

    [ObservableProperty]
    private string itemTitle = string.Empty;

    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime endDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private string message = string.Empty;

    public RentalRequestViewModel(
        IRentalApiService rentalApiService,
        INavigationService navigationService,
        IUserNotificationService notificationService)
    {
        _rentalApiService = rentalApiService;
        _navigationService = navigationService;
        _notificationService = notificationService;
        Title = "Request Rental";
    }

    [RelayCommand]
    private async Task SubmitRequestAsync()
    {
        if (IsBusy) return;

        // Basic guard to avoid invalid rental durations being sent to the API.
        if (StartDate >= EndDate)
        {
            SetError("End date must be after start date.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            var request = new CreateRentalRequestDto
            {
                ItemId = ItemId,
                StartDate = StartDate,
                EndDate = EndDate,
                // Keep the payload clean and avoid accidental whitespace-only messages.
                Message = Message.Trim()
            };

            await _rentalApiService.CreateRentalRequestAsync(request);

            await _notificationService.ShowAlertAsync("Success", "Rental request submitted.");

            await _navigationService.NavigateBackAsync();
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

    [RelayCommand]
    private async Task CancelAsync()
    {
        await _navigationService.NavigateBackAsync();
    }
}
