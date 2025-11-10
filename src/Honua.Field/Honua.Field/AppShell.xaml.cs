// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Views;
using HonuaField.ViewModels;

namespace HonuaField;

public partial class AppShell : Shell
{
	private readonly AppShellViewModel _viewModel;

	public AppShell(AppShellViewModel viewModel)
	{
		InitializeComponent();

		_viewModel = viewModel;
		BindingContext = _viewModel;

		// Register routes for navigation
		RegisterRoutes();
	}

	private void RegisterRoutes()
	{
		// Register modal/detail pages that aren't in the main navigation
		Routing.RegisterRoute(nameof(FeatureDetailPage), typeof(FeatureDetailPage));
		Routing.RegisterRoute(nameof(FeatureEditorPage), typeof(FeatureEditorPage));
		Routing.RegisterRoute(nameof(OnboardingPage), typeof(OnboardingPage));
		Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
		Routing.RegisterRoute(nameof(SyncPage), typeof(SyncPage));
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.OnAppearingAsync();
	}
}
