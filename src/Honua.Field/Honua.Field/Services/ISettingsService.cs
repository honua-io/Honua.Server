namespace HonuaField.Services;

/// <summary>
/// Service for storing and retrieving app settings and preferences
/// Uses secure storage for sensitive data (tokens, passwords)
/// </summary>
public interface ISettingsService
{
	/// <summary>
	/// Get a setting value
	/// </summary>
	Task<T?> GetAsync<T>(string key, T? defaultValue = default);

	/// <summary>
	/// Set a setting value
	/// </summary>
	Task SetAsync<T>(string key, T value);

	/// <summary>
	/// Remove a setting
	/// </summary>
	Task RemoveAsync(string key);

	/// <summary>
	/// Clear all settings
	/// </summary>
	Task ClearAsync();

	/// <summary>
	/// Check if a setting exists
	/// </summary>
	Task<bool> ContainsKeyAsync(string key);
}
