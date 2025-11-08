using HonuaField.ViewModels;

namespace HonuaField.Views;

public partial class FeatureEditorPage : ContentPage
{
	private readonly FeatureEditorViewModel _viewModel;

	public FeatureEditorPage(FeatureEditorViewModel viewModel)
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

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Cleanup if needed
	}
}
