// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.ViewModels;

namespace HonuaField.Views;

public partial class OnboardingPage : ContentPage
{
	private readonly OnboardingViewModel _viewModel;

	public OnboardingPage(OnboardingViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = _viewModel;

		// Subscribe to property changes to update UI
		_viewModel.PropertyChanged += ViewModel_PropertyChanged;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.OnAppearingAsync();
		UpdateStepVisibility();
	}

	private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(_viewModel.CurrentStep))
		{
			UpdateStepVisibility();
			UpdateProgressDots();
		}
		else if (e.PropertyName == nameof(_viewModel.LocationPermissionGranted))
		{
			UpdatePermissionIcon(LocationPermissionIcon, _viewModel.LocationPermissionGranted);
		}
		else if (e.PropertyName == nameof(_viewModel.CameraPermissionGranted))
		{
			UpdatePermissionIcon(CameraPermissionIcon, _viewModel.CameraPermissionGranted);
		}
		else if (e.PropertyName == nameof(_viewModel.StoragePermissionGranted))
		{
			UpdatePermissionIcon(StoragePermissionIcon, _viewModel.StoragePermissionGranted);
		}
	}

	private void UpdateStepVisibility()
	{
		// Hide all steps
		WelcomeStep.IsVisible = false;
		PermissionsStep.IsVisible = false;
		ServerStep.IsVisible = false;
		AllSetStep.IsVisible = false;

		// Show current step
		switch (_viewModel.CurrentStep)
		{
			case 0:
				WelcomeStep.IsVisible = true;
				break;
			case 1:
				PermissionsStep.IsVisible = true;
				UpdatePermissionIcons();
				break;
			case 2:
				ServerStep.IsVisible = true;
				break;
			case 3:
				AllSetStep.IsVisible = true;
				break;
		}
	}

	private void UpdateProgressDots()
	{
		// Reset all dots to inactive color
		ProgressDot0.BackgroundColor = Colors.LightGray;
		ProgressDot1.BackgroundColor = Colors.LightGray;
		ProgressDot2.BackgroundColor = Colors.LightGray;
		ProgressDot3.BackgroundColor = Colors.LightGray;

		// Set active dot
		switch (_viewModel.CurrentStep)
		{
			case 0:
				ProgressDot0.BackgroundColor = Color.FromArgb("#0066CC"); // PrimaryBlue
				break;
			case 1:
				ProgressDot1.BackgroundColor = Color.FromArgb("#0066CC");
				break;
			case 2:
				ProgressDot2.BackgroundColor = Color.FromArgb("#0066CC");
				break;
			case 3:
				ProgressDot3.BackgroundColor = Color.FromArgb("#0066CC");
				break;
		}
	}

	private void UpdatePermissionIcons()
	{
		UpdatePermissionIcon(LocationPermissionIcon, _viewModel.LocationPermissionGranted);
		UpdatePermissionIcon(CameraPermissionIcon, _viewModel.CameraPermissionGranted);
		UpdatePermissionIcon(StoragePermissionIcon, _viewModel.StoragePermissionGranted);
	}

	private void UpdatePermissionIcon(Label icon, bool granted)
	{
		if (granted)
		{
			icon.Text = "✓";
			icon.TextColor = Color.FromArgb("#28A745"); // SuccessGreen
		}
		else
		{
			icon.Text = "○";
			icon.TextColor = Color.FromArgb("#ADB5BD"); // Gray400
		}
	}
}
