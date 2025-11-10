// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HonuaField.Services;

/// <summary>
/// HTTP API client for communicating with Honua Server
/// Handles authentication, error handling, and request/response logging
/// </summary>
public class ApiClient : IApiClient
{
	private readonly HttpClient _httpClient;
	private readonly ISettingsService _settingsService;
	private readonly JsonSerializerOptions _jsonOptions;

	public ApiClient(ISettingsService settingsService)
	{
		_settingsService = settingsService;
		_httpClient = new HttpClient();

		// Configure JSON serialization options
		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		// Set default headers
		_httpClient.DefaultRequestHeaders.Accept.Add(
			new MediaTypeWithQualityHeaderValue("application/json"));
		_httpClient.DefaultRequestHeaders.UserAgent.Add(
			new ProductInfoHeaderValue("HonuaField", "1.0.0"));
	}

	public async Task<T?> GetAsync<T>(string endpoint, string? accessToken = null)
	{
		try
		{
			var baseUrl = await GetBaseUrlAsync();
			var url = $"{baseUrl}{endpoint}";

			var request = new HttpRequestMessage(HttpMethod.Get, url);
			await AddAuthorizationHeaderAsync(request, accessToken);

			LogRequest(request);

			var response = await _httpClient.SendAsync(request);
			await LogResponseAsync(response);

			if (response.IsSuccessStatusCode)
			{
				var content = await response.Content.ReadAsStringAsync();
				if (string.IsNullOrWhiteSpace(content))
				{
					return default;
				}

				return JsonSerializer.Deserialize<T>(content, _jsonOptions);
			}

			await HandleErrorResponseAsync(response);
			return default;
		}
		catch (HttpRequestException ex)
		{
			System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
			throw new ApiException("Network error. Please check your connection.", ex);
		}
		catch (JsonException ex)
		{
			System.Diagnostics.Debug.WriteLine($"JSON deserialization failed: {ex.Message}");
			throw new ApiException("Invalid response from server.", ex);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Unexpected error: {ex.Message}");
			throw new ApiException("An unexpected error occurred.", ex);
		}
	}

	public async Task<T?> PostAsync<T>(string endpoint, object data, string? accessToken = null)
	{
		try
		{
			var baseUrl = await GetBaseUrlAsync();
			var url = $"{baseUrl}{endpoint}";

			var request = new HttpRequestMessage(HttpMethod.Post, url);
			await AddAuthorizationHeaderAsync(request, accessToken);

			// Serialize request body
			var json = JsonSerializer.Serialize(data, _jsonOptions);
			request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

			LogRequest(request, json);

			var response = await _httpClient.SendAsync(request);
			await LogResponseAsync(response);

			if (response.IsSuccessStatusCode)
			{
				var content = await response.Content.ReadAsStringAsync();
				if (string.IsNullOrWhiteSpace(content))
				{
					return default;
				}

				return JsonSerializer.Deserialize<T>(content, _jsonOptions);
			}

			await HandleErrorResponseAsync(response);
			return default;
		}
		catch (HttpRequestException ex)
		{
			System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
			throw new ApiException("Network error. Please check your connection.", ex);
		}
		catch (JsonException ex)
		{
			System.Diagnostics.Debug.WriteLine($"JSON serialization/deserialization failed: {ex.Message}");
			throw new ApiException("Invalid data format.", ex);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Unexpected error: {ex.Message}");
			throw new ApiException("An unexpected error occurred.", ex);
		}
	}

	public async Task<T?> PutAsync<T>(string endpoint, object data, string? accessToken = null)
	{
		try
		{
			var baseUrl = await GetBaseUrlAsync();
			var url = $"{baseUrl}{endpoint}";

			var request = new HttpRequestMessage(HttpMethod.Put, url);
			await AddAuthorizationHeaderAsync(request, accessToken);

			// Serialize request body
			var json = JsonSerializer.Serialize(data, _jsonOptions);
			request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

			LogRequest(request, json);

			var response = await _httpClient.SendAsync(request);
			await LogResponseAsync(response);

			if (response.IsSuccessStatusCode)
			{
				var content = await response.Content.ReadAsStringAsync();
				if (string.IsNullOrWhiteSpace(content))
				{
					return default;
				}

				return JsonSerializer.Deserialize<T>(content, _jsonOptions);
			}

			await HandleErrorResponseAsync(response);
			return default;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"PUT request failed: {ex.Message}");
			throw new ApiException("Request failed.", ex);
		}
	}

	public async Task<bool> DeleteAsync(string endpoint, string? accessToken = null)
	{
		try
		{
			var baseUrl = await GetBaseUrlAsync();
			var url = $"{baseUrl}{endpoint}";

			var request = new HttpRequestMessage(HttpMethod.Delete, url);
			await AddAuthorizationHeaderAsync(request, accessToken);

			LogRequest(request);

			var response = await _httpClient.SendAsync(request);
			await LogResponseAsync(response);

			if (response.IsSuccessStatusCode)
			{
				return true;
			}

			await HandleErrorResponseAsync(response);
			return false;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"DELETE request failed: {ex.Message}");
			throw new ApiException("Request failed.", ex);
		}
	}

	private async Task<string> GetBaseUrlAsync()
	{
		var baseUrl = await _settingsService.GetAsync("server_url", "https://api.honua.io");
		return baseUrl.TrimEnd('/');
	}

	private async Task AddAuthorizationHeaderAsync(HttpRequestMessage request, string? accessToken)
	{
		var token = accessToken;

		// If no token provided, try to get from settings
		if (string.IsNullOrEmpty(token))
		{
			token = await _settingsService.GetAsync<string>("access_token");
		}

		if (!string.IsNullOrEmpty(token))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}
	}

	private void LogRequest(HttpRequestMessage request, string? body = null)
	{
#if DEBUG
		System.Diagnostics.Debug.WriteLine($"[API] {request.Method} {request.RequestUri}");
		if (!string.IsNullOrEmpty(body))
		{
			System.Diagnostics.Debug.WriteLine($"[API] Request Body: {body}");
		}
#endif
	}

	private async Task LogResponseAsync(HttpResponseMessage response)
	{
#if DEBUG
		var content = await response.Content.ReadAsStringAsync();
		System.Diagnostics.Debug.WriteLine($"[API] {(int)response.StatusCode} {response.ReasonPhrase}");
		if (!string.IsNullOrEmpty(content))
		{
			System.Diagnostics.Debug.WriteLine($"[API] Response Body: {content}");
		}
#else
		await Task.CompletedTask;
#endif
	}

	private async Task HandleErrorResponseAsync(HttpResponseMessage response)
	{
		var content = await response.Content.ReadAsStringAsync();

		var message = response.StatusCode switch
		{
			System.Net.HttpStatusCode.Unauthorized => "Unauthorized. Please log in again.",
			System.Net.HttpStatusCode.Forbidden => "Access denied.",
			System.Net.HttpStatusCode.NotFound => "Resource not found.",
			System.Net.HttpStatusCode.BadRequest => "Invalid request.",
			System.Net.HttpStatusCode.InternalServerError => "Server error. Please try again later.",
			_ => $"Request failed with status {(int)response.StatusCode}."
		};

		// Try to parse error details from response
		try
		{
			if (!string.IsNullOrWhiteSpace(content))
			{
				var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);
				if (errorResponse?.Message != null)
				{
					message = errorResponse.Message;
				}
			}
		}
		catch
		{
			// Ignore JSON parsing errors, use default message
		}

		throw new ApiException(message)
		{
			StatusCode = response.StatusCode
		};
	}
}

/// <summary>
/// Exception thrown when API request fails
/// </summary>
public class ApiException : Exception
{
	public System.Net.HttpStatusCode? StatusCode { get; init; }

	public ApiException(string message) : base(message) { }
	public ApiException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Standard error response from API
/// </summary>
internal record ErrorResponse
{
	public string? Message { get; init; }
	public string? Code { get; init; }
	public Dictionary<string, string[]>? Errors { get; init; }
}
