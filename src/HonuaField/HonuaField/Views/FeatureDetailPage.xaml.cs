using HonuaField.ViewModels;

namespace HonuaField.Views;

public partial class FeatureDetailPage : ContentPage
{
	private readonly FeatureDetailViewModel _viewModel;

	public FeatureDetailPage(FeatureDetailViewModel viewModel)
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
