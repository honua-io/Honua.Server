// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

#if ANDROID
using Android.Content;
using AndroidX.Biometric;
using AndroidX.Fragment.App;
using Java.Util.Concurrent;

namespace HonuaField.Services;

/// <summary>
/// Android-specific biometric authentication implementation
/// Uses AndroidX.Biometric for fingerprint and face authentication
/// </summary>
public partial class BiometricService
{
	partial Task<bool> PlatformIsBiometricAvailableAsync()
	{
		try
		{
			var context = Platform.CurrentActivity ?? Android.App.Application.Context;
			var biometricManager = BiometricManager.From(context);
			var canAuthenticate = biometricManager.CanAuthenticate(BiometricManager.Authenticators.BiometricStrong);

			return Task.FromResult(canAuthenticate == BiometricManager.BiometricSuccess);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Android biometric check failed: {ex.Message}");
			return Task.FromResult(false);
		}
	}

	partial Task<BiometricType> PlatformGetBiometricTypeAsync()
	{
		try
		{
			var context = Platform.CurrentActivity ?? Android.App.Application.Context;
			var biometricManager = BiometricManager.From(context);
			var canAuthenticate = biometricManager.CanAuthenticate(BiometricManager.Authenticators.BiometricStrong);

			if (canAuthenticate == BiometricManager.BiometricSuccess)
			{
				// Android doesn't distinguish between fingerprint and face recognition in BiometricManager
				// Return generic Fingerprint type (most common on Android)
				return Task.FromResult(BiometricType.Fingerprint);
			}

			return Task.FromResult(BiometricType.None);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Android get biometric type failed: {ex.Message}");
			return Task.FromResult(BiometricType.None);
		}
	}

	partial async Task<BiometricResult> PlatformAuthenticateAsync(string reason)
	{
		try
		{
			var context = Platform.CurrentActivity ?? Android.App.Application.Context;
			var biometricManager = BiometricManager.From(context);
			var canAuthenticate = biometricManager.CanAuthenticate(BiometricManager.Authenticators.BiometricStrong);

			if (canAuthenticate != BiometricManager.BiometricSuccess)
			{
				var errorType = canAuthenticate switch
				{
					BiometricManager.BiometricErrorNoneEnrolled => BiometricErrorType.NotEnrolled,
					BiometricManager.BiometricErrorNoHardware => BiometricErrorType.NotAvailable,
					BiometricManager.BiometricErrorHwUnavailable => BiometricErrorType.NotAvailable,
					BiometricManager.BiometricErrorSecurityUpdateRequired => BiometricErrorType.NotAvailable,
					BiometricManager.BiometricErrorUnsupported => BiometricErrorType.NotAvailable,
					_ => BiometricErrorType.Unknown
				};

				return BiometricResult.Failed("Biometric authentication not available", errorType);
			}

			// Create TaskCompletionSource to bridge callback to async/await
			var tcs = new TaskCompletionSource<BiometricResult>();

			// Build biometric prompt
			var executor = Executors.NewSingleThreadExecutor();
			var promptInfo = new BiometricPrompt.PromptInfo.Builder()
				.SetTitle("Biometric Authentication")
				.SetSubtitle(reason)
				.SetNegativeButtonText("Cancel")
				.SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong)
				.Build();

			// Create authentication callback
			var authCallback = new BiometricAuthCallback(
				onSuccess: () => tcs.TrySetResult(BiometricResult.Successful()),
				onError: (errorCode, errorMessage) =>
				{
					var errorType = errorCode switch
					{
						BiometricPrompt.ErrorCanceled => BiometricErrorType.UserCanceled,
						BiometricPrompt.ErrorLockout => BiometricErrorType.Locked,
						BiometricPrompt.ErrorLockoutPermanent => BiometricErrorType.Locked,
						BiometricPrompt.ErrorNegativeButton => BiometricErrorType.UserCanceled,
						BiometricPrompt.ErrorNoSpace => BiometricErrorType.Failed,
						BiometricPrompt.ErrorTimeout => BiometricErrorType.Failed,
						BiometricPrompt.ErrorUnableToProcess => BiometricErrorType.Failed,
						BiometricPrompt.ErrorUserCanceled => BiometricErrorType.UserCanceled,
						BiometricPrompt.ErrorVendor => BiometricErrorType.Failed,
						_ => BiometricErrorType.Unknown
					};

					tcs.TrySetResult(BiometricResult.Failed(errorMessage ?? "Authentication failed", errorType));
				},
				onFailed: () => tcs.TrySetResult(BiometricResult.Failed("Authentication failed", BiometricErrorType.Failed))
			);

			// Show biometric prompt
			var activity = Platform.CurrentActivity;
			if (activity is FragmentActivity fragmentActivity)
			{
				var biometricPrompt = new BiometricPrompt(fragmentActivity, executor, authCallback);
				biometricPrompt.Authenticate(promptInfo);

				return await tcs.Task;
			}
			else
			{
				return BiometricResult.Failed("Current activity is not a FragmentActivity", BiometricErrorType.Unknown);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Android biometric authentication failed: {ex.Message}");
			return BiometricResult.Failed(ex.Message, BiometricErrorType.Unknown);
		}
	}

	partial Task<bool> PlatformIsBiometricEnrolledAsync()
	{
		try
		{
			var context = Platform.CurrentActivity ?? Android.App.Application.Context;
			var biometricManager = BiometricManager.From(context);
			var canAuthenticate = biometricManager.CanAuthenticate(BiometricManager.Authenticators.BiometricStrong);

			// BiometricSuccess means hardware is available AND biometrics are enrolled
			return Task.FromResult(canAuthenticate == BiometricManager.BiometricSuccess);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Android biometric enrollment check failed: {ex.Message}");
			return Task.FromResult(false);
		}
	}
}

/// <summary>
/// Callback for BiometricPrompt authentication
/// </summary>
internal class BiometricAuthCallback : BiometricPrompt.AuthenticationCallback
{
	private readonly Action _onSuccess;
	private readonly Action<int, string?> _onError;
	private readonly Action _onFailed;

	public BiometricAuthCallback(Action onSuccess, Action<int, string?> onError, Action onFailed)
	{
		_onSuccess = onSuccess;
		_onError = onError;
		_onFailed = onFailed;
	}

	public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
	{
		base.OnAuthenticationSucceeded(result);
		_onSuccess?.Invoke();
	}

	public override void OnAuthenticationError(int errorCode, ICharSequence? errString)
	{
		base.OnAuthenticationError(errorCode, errString);
		_onError?.Invoke(errorCode, errString?.ToString());
	}

	public override void OnAuthenticationFailed()
	{
		base.OnAuthenticationFailed();
		_onFailed?.Invoke();
	}
}
#endif
