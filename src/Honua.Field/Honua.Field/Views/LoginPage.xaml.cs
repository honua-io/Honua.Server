// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.ViewModels;

namespace HonuaField.Views;

public partial class LoginPage : ContentPage
{
	private readonly LoginViewModel _viewModel;

	public LoginPage(LoginViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		// Fire and forget with proper exception handling
		_ = HandleOnAppearingAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Fire and forget with proper exception handling
		_ = HandleOnDisappearingAsync();
	}

	private async Task HandleOnAppearingAsync()
	{
		try
		{
			await _viewModel.OnAppearingAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error in OnAppearing: {ex}");
		}
	}

	private async Task HandleOnDisappearingAsync()
	{
		try
		{
			await _viewModel.OnDisappearingAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error in OnDisappearing: {ex}");
		}
	}
}
