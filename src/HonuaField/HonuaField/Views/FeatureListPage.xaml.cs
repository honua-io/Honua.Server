using HonuaField.ViewModels;

namespace HonuaField.Views;

public partial class FeatureListPage : ContentPage
{
	private readonly FeatureListViewModel _viewModel;

	public FeatureListPage(FeatureListViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = _viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.OnAppearingAsync();
	}
}
