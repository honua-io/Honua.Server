// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

#if !IOS && !ANDROID
namespace HonuaField.Services;

/// <summary>
/// Default biometric service implementation for platforms without biometric support
/// (Windows, macOS, Tizen, etc.)
/// </summary>
public partial class BiometricService
{
	partial Task<bool> PlatformIsBiometricAvailableAsync()
	{
		// Biometric authentication not implemented for this platform
		return Task.FromResult(false);
	}

	partial Task<BiometricType> PlatformGetBiometricTypeAsync()
	{
		return Task.FromResult(BiometricType.None);
	}

	partial Task<BiometricResult> PlatformAuthenticateAsync(string reason)
	{
		return Task.FromResult(
			BiometricResult.Failed(
				"Biometric authentication is not supported on this platform",
				BiometricErrorType.NotAvailable
			)
		);
	}

	partial Task<bool> PlatformIsBiometricEnrolledAsync()
	{
		return Task.FromResult(false);
	}
}
#endif
