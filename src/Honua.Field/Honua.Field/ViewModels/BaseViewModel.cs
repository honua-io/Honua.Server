// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HonuaField.ViewModels;

/// <summary>
/// Base ViewModel for all ViewModels in the application.
/// Uses CommunityToolkit.Mvvm for MVVM infrastructure.
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsNotBusy))]
	private bool _isBusy;

	[ObservableProperty]
	private string _title = string.Empty;

	[ObservableProperty]
	private string _errorMessage = string.Empty;

	public bool IsNotBusy => !IsBusy;

	/// <summary>
	/// Called when the view appears
	/// </summary>
	public virtual Task OnAppearingAsync()
	{
		return Task.CompletedTask;
	}

	/// <summary>
	/// Called when the view disappears
	/// </summary>
	public virtual Task OnDisappearingAsync()
	{
		return Task.CompletedTask;
	}

	/// <summary>
	/// Handle errors in a consistent way
	/// </summary>
	protected virtual async Task HandleErrorAsync(Exception ex, string message = "An error occurred")
	{
		IsBusy = false;
		ErrorMessage = message;

		// Log the error
		System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
		System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

		// Show error to user
		await ShowAlertAsync("Error", $"{message}: {ex.Message}");
	}

	/// <summary>
	/// Show an alert dialog
	/// </summary>
	protected virtual async Task ShowAlertAsync(string title, string message, string button = "OK")
	{
		if (Application.Current?.MainPage != null)
		{
			await Application.Current.MainPage.DisplayAlert(title, message, button);
		}
	}

	/// <summary>
	/// Show a confirmation dialog
	/// </summary>
	protected virtual async Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No")
	{
		if (Application.Current?.MainPage != null)
		{
			return await Application.Current.MainPage.DisplayAlert(title, message, accept, cancel);
		}
		return false;
	}

	/// <summary>
	/// Show an action sheet dialog
	/// </summary>
	protected virtual async Task<string> ShowActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
	{
		if (Application.Current?.MainPage != null)
		{
			return await Application.Current.MainPage.DisplayActionSheet(title, cancel, destruction, buttons);
		}
		return cancel;
	}
}
