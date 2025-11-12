// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

#if IOS
using LocalAuthentication;
using Foundation;

namespace HonuaField.Services;

/// <summary>
/// iOS-specific biometric authentication implementation
/// Uses LocalAuthentication framework for Touch ID and Face ID
/// </summary>
public partial class BiometricService
{
	partial Task<bool> PlatformIsBiometricAvailableAsync()
	{
		var context = new LAContext();
		NSError? error;
		var canEvaluate = context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out error);

		return Task.FromResult(canEvaluate);
	}

	partial Task<BiometricType> PlatformGetBiometricTypeAsync()
	{
		var context = new LAContext();
		NSError? error;

		if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out error))
		{
			return Task.FromResult(BiometricType.None);
		}

		// iOS 11+ supports biometryType property
		if (OperatingSystem.IsIOSVersionAtLeast(11))
		{
			var biometricType = context.BiometryType switch
			{
				LABiometryType.FaceId => BiometricType.FaceId,
				LABiometryType.TouchId => BiometricType.TouchId,
				LABiometryType.None => BiometricType.None,
				_ => BiometricType.None
			};
			return Task.FromResult(biometricType);
		}

		// Fallback for older iOS versions (assume Touch ID)
		return Task.FromResult(BiometricType.TouchId);
	}

	partial async Task<BiometricResult> PlatformAuthenticateAsync(string reason)
	{
		var context = new LAContext();
		NSError? error;

		// Check if biometric authentication is available
		if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out error))
		{
			return BiometricResult.Failed(
				error?.LocalizedDescription ?? "Biometric authentication not available",
				BiometricErrorType.NotAvailable
			);
		}

		try
		{
			// Attempt authentication
			var (success, authError) = await context.EvaluatePolicyAsync(
				LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
				reason
			);

			if (success)
			{
				return BiometricResult.Successful();
			}

			// Handle authentication failure
			var errorType = authError?.Code switch
			{
				-1 => BiometricErrorType.Failed, // LAError.AuthenticationFailed
				-2 => BiometricErrorType.UserCanceled, // LAError.UserCancel
				-3 => BiometricErrorType.PasscodeFallback, // LAError.UserFallback
				-4 => BiometricErrorType.SystemCanceled, // LAError.SystemCancel
				-5 => BiometricErrorType.NotAvailable, // LAError.PasscodeNotSet
				-6 => BiometricErrorType.NotAvailable, // LAError.BiometryNotAvailable
				-7 => BiometricErrorType.NotEnrolled, // LAError.BiometryNotEnrolled
				-8 => BiometricErrorType.Locked, // LAError.BiometryLockout
				_ => BiometricErrorType.Unknown
			};

			return BiometricResult.Failed(
				authError?.LocalizedDescription ?? "Authentication failed",
				errorType
			);
		}
		catch (Exception ex)
		{
			return BiometricResult.Failed(ex.Message, BiometricErrorType.Unknown);
		}
	}

	partial Task<bool> PlatformIsBiometricEnrolledAsync()
	{
		var context = new LAContext();
		NSError? error;

		// CanEvaluatePolicy returns true only if biometrics are both available AND enrolled
		var canEvaluate = context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out error);

		// If error code is BiometryNotEnrolled (-7), then it's available but not enrolled
		if (!canEvaluate && error?.Code == -7)
		{
			return Task.FromResult(false);
		}

		return Task.FromResult(canEvaluate);
	}
}
#endif
