using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarterApp.Services;

namespace StarterApp.ViewModels;

[QueryProperty(nameof(ItemId), "itemId")]
[QueryProperty(nameof(ItemTitle), "itemTitle")]
public partial class RentalRequestViewModel : BaseViewModel
{
    private readonly IRentalApiService _rentalApiService;
    private readonly INavigationService _navigationService;

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

    public RentalRequestViewModel(IRentalApiService rentalApiService, INavigationService navigationService)
    {
        _rentalApiService = rentalApiService;
        _navigationService = navigationService;
        Title = "Request Rental";
    }

    [RelayCommand]
    private async Task SubmitRequestAsync()
    {
        if (IsBusy) return;

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
                Message = Message.Trim()
            };

            await _rentalApiService.CreateRentalRequestAsync(request);
            
            await Application.Current!.MainPage!.DisplayAlert("Success", "Rental request submitted.", "OK");
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
