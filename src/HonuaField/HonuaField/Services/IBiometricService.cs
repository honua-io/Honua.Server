namespace HonuaField.Services;

/// <summary>
/// Service for biometric authentication (Face ID, Touch ID, Fingerprint)
/// Platform-specific implementations required for iOS and Android
/// </summary>
public interface IBiometricService
{
	/// <summary>
	/// Check if biometric authentication is available on the device
	/// </summary>
	Task<bool> IsBiometricAvailableAsync();

	/// <summary>
	/// Get the type of biometric authentication available
	/// </summary>
	Task<BiometricType> GetBiometricTypeAsync();

	/// <summary>
	/// Authenticate user with biometrics
	/// </summary>
	/// <param name="reason">Reason to show to the user</param>
	Task<BiometricResult> AuthenticateAsync(string reason = "Please authenticate to continue");

	/// <summary>
	/// Check if biometric authentication is enrolled (user has set up Face ID/Touch ID/Fingerprint)
	/// </summary>
	Task<bool> IsBiometricEnrolledAsync();
}

/// <summary>
/// Types of biometric authentication
/// </summary>
public enum BiometricType
{
	None,
	Fingerprint,
	FaceId,
	TouchId,
	Iris,
	Voice
}

/// <summary>
/// Result of biometric authentication attempt
/// </summary>
public class BiometricResult
{
	public bool Success { get; set; }
	public string? ErrorMessage { get; set; }
	public BiometricErrorType ErrorType { get; set; }

	public static BiometricResult Successful() => new() { Success = true };

	public static BiometricResult Failed(string message, BiometricErrorType errorType = BiometricErrorType.Unknown)
		=> new() { Success = false, ErrorMessage = message, ErrorType = errorType };
}

/// <summary>
/// Types of biometric errors
/// </summary>
public enum BiometricErrorType
{
	Unknown,
	NotAvailable,
	NotEnrolled,
	Locked,
	UserCanceled,
	SystemCanceled,
	Failed,
	PasscodeFallback
}
