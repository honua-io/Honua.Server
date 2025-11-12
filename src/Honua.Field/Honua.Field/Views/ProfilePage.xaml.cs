// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.ViewModels;

namespace HonuaField.Views;

public partial class ProfilePage : ContentPage
{
	private readonly ProfileViewModel _viewModel;

	public ProfilePage(ProfileViewModel viewModel)
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

	private async void OnOfflineModeToggled(object sender, ToggledEventArgs e)
	{
		// The toggle command is handled by the ViewModel
		await _viewModel.ToggleOfflineModeCommand.ExecuteAsync(null);
	}
}
