using StarterApp.ViewModels;

namespace StarterApp.Views;

public partial class RentalRequestPage : ContentPage
{
    public RentalRequestPage(RentalRequestViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
