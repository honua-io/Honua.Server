// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

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
