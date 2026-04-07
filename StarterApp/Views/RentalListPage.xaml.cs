using StarterApp.ViewModels;

namespace StarterApp.Views;

public partial class RentalListPage : ContentPage
{
    private readonly RentalListViewModel _viewModel;

    public RentalListPage(RentalListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
