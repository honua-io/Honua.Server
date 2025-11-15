// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace HonuaField.Services;

/// <summary>
/// Cross-platform biometric authentication service
/// Uses platform-specific APIs via conditional compilation
/// </summary>
public partial class BiometricService : IBiometricService
{
	public async Task<bool> IsBiometricAvailableAsync()
	{
		try
		{
			return await PlatformIsBiometricAvailableAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Biometric availability check failed");
			return false;
		}
	}

	public async Task<BiometricType> GetBiometricTypeAsync()
	{
		try
		{
			return await PlatformGetBiometricTypeAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Get biometric type failed");
			return BiometricType.None;
		}
	}

	public async Task<BiometricResult> AuthenticateAsync(string reason = "Please authenticate to continue")
	{
		try
		{
			var isAvailable = await IsBiometricAvailableAsync();
			if (!isAvailable)
			{
				return BiometricResult.Failed("Biometric authentication is not available on this device", BiometricErrorType.NotAvailable);
			}

			var isEnrolled = await IsBiometricEnrolledAsync();
			if (!isEnrolled)
			{
				return BiometricResult.Failed("No biometric credentials are enrolled", BiometricErrorType.NotEnrolled);
			}

			return await PlatformAuthenticateAsync(reason);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Biometric authentication failed");
			return BiometricResult.Failed(ex.Message, BiometricErrorType.Unknown);
		}
	}

	public async Task<bool> IsBiometricEnrolledAsync()
	{
		try
		{
			return await PlatformIsBiometricEnrolledAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Biometric enrollment check failed");
			return false;
		}
	}

	// Platform-specific implementations
	// These partial methods are implemented in platform-specific files:
	// - Platforms/iOS/BiometricService.cs
	// - Platforms/Android/BiometricService.cs

	partial Task<bool> PlatformIsBiometricAvailableAsync();
	partial Task<BiometricType> PlatformGetBiometricTypeAsync();
	partial Task<BiometricResult> PlatformAuthenticateAsync(string reason);
	partial Task<bool> PlatformIsBiometricEnrolledAsync();
}
