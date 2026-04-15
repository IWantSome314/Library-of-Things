using StarterApp.ViewModels;

namespace StarterApp.Views;

public partial class TempPage : ContentPage
{
	public TempPage(TempViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}