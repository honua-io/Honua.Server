using System.Text.Json;

namespace HonuaField.Services;

/// <summary>
/// Implementation of ISettingsService using MAUI Preferences for general settings
/// and SecureStorage for sensitive data (tokens, passwords)
/// </summary>
public class SettingsService : ISettingsService
{
	private readonly HashSet<string> _secureKeys = new()
	{
		"access_token",
		"refresh_token",
		"user_password",
		"api_key"
	};

	public async Task<T?> GetAsync<T>(string key, T? defaultValue = default)
	{
		try
		{
			// Use SecureStorage for sensitive data
			if (_secureKeys.Contains(key))
			{
				var value = await SecureStorage.GetAsync(key);
				if (value == null)
				{
					return defaultValue;
				}

				// Handle different types
				if (typeof(T) == typeof(string))
				{
					return (T)(object)value;
				}

				return JsonSerializer.Deserialize<T>(value);
			}

			// Use Preferences for general settings
			if (typeof(T) == typeof(string))
			{
				return (T)(object)Preferences.Get(key, defaultValue?.ToString() ?? string.Empty);
			}
			else if (typeof(T) == typeof(int))
			{
				return (T)(object)Preferences.Get(key, Convert.ToInt32(defaultValue));
			}
			else if (typeof(T) == typeof(double))
			{
				return (T)(object)Preferences.Get(key, Convert.ToDouble(defaultValue));
			}
			else if (typeof(T) == typeof(float))
			{
				return (T)(object)Preferences.Get(key, Convert.ToSingle(defaultValue));
			}
			else if (typeof(T) == typeof(bool))
			{
				return (T)(object)Preferences.Get(key, Convert.ToBoolean(defaultValue));
			}
			else if (typeof(T) == typeof(DateTime))
			{
				return (T)(object)Preferences.Get(key, (DateTime)(object)(defaultValue ?? DateTime.MinValue));
			}
			else
			{
				// For complex types, serialize to JSON
				var json = Preferences.Get(key, string.Empty);
				if (string.IsNullOrEmpty(json))
				{
					return defaultValue;
				}

				return JsonSerializer.Deserialize<T>(json);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error getting setting {key}: {ex.Message}");
			return defaultValue;
		}
	}

	public async Task SetAsync<T>(string key, T value)
	{
		try
		{
			if (value == null)
			{
				await RemoveAsync(key);
				return;
			}

			// Use SecureStorage for sensitive data
			if (_secureKeys.Contains(key))
			{
				var stringValue = value is string str
					? str
					: JsonSerializer.Serialize(value);

				await SecureStorage.SetAsync(key, stringValue);
				return;
			}

			// Use Preferences for general settings
			if (value is string stringVal)
			{
				Preferences.Set(key, stringVal);
			}
			else if (value is int intVal)
			{
				Preferences.Set(key, intVal);
			}
			else if (value is double doubleVal)
			{
				Preferences.Set(key, doubleVal);
			}
			else if (value is float floatVal)
			{
				Preferences.Set(key, floatVal);
			}
			else if (value is bool boolVal)
			{
				Preferences.Set(key, boolVal);
			}
			else if (value is DateTime dateVal)
			{
				Preferences.Set(key, dateVal);
			}
			else
			{
				// For complex types, serialize to JSON
				var json = JsonSerializer.Serialize(value);
				Preferences.Set(key, json);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error setting {key}: {ex.Message}");
		}
	}

	public async Task RemoveAsync(string key)
	{
		try
		{
			if (_secureKeys.Contains(key))
			{
				SecureStorage.Remove(key);
			}
			else
			{
				Preferences.Remove(key);
			}

			await Task.CompletedTask;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error removing setting {key}: {ex.Message}");
		}
	}

	public async Task ClearAsync()
	{
		try
		{
			Preferences.Clear();
			SecureStorage.RemoveAll();
			await Task.CompletedTask;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error clearing settings: {ex.Message}");
		}
	}

	public async Task<bool> ContainsKeyAsync(string key)
	{
		try
		{
			if (_secureKeys.Contains(key))
			{
				var value = await SecureStorage.GetAsync(key);
				return !string.IsNullOrEmpty(value);
			}

			return Preferences.ContainsKey(key);
		}
		catch
		{
			return false;
		}
	}
}
