// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using System.Text.Json;

namespace HonuaField.Tests.Integration.Infrastructure;

/// <summary>
/// Mock HTTP message handler for testing API calls without actual network requests
/// Supports configurable responses, network failures, and request history tracking
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
	private readonly Dictionary<string, ResponseConfig> _responseConfigs = new();
	private readonly List<HttpRequestMessage> _requestHistory = new();
	private readonly object _lock = new();

	/// <summary>
	/// Get the list of all HTTP requests made
	/// </summary>
	public IReadOnlyList<HttpRequestMessage> RequestHistory
	{
		get
		{
			lock (_lock)
			{
				return _requestHistory.ToList();
			}
		}
	}

	/// <summary>
	/// Configure a response for a specific endpoint pattern
	/// </summary>
	public void ConfigureResponse(
		string urlPattern,
		HttpStatusCode statusCode = HttpStatusCode.OK,
		object? responseBody = null,
		int delayMs = 0,
		Exception? exception = null)
	{
		lock (_lock)
		{
			_responseConfigs[urlPattern] = new ResponseConfig
			{
				StatusCode = statusCode,
				ResponseBody = responseBody,
				DelayMs = delayMs,
				Exception = exception
			};
		}
	}

	/// <summary>
	/// Configure a JSON response for a specific endpoint pattern
	/// </summary>
	public void ConfigureJsonResponse<T>(
		string urlPattern,
		T responseBody,
		HttpStatusCode statusCode = HttpStatusCode.OK,
		int delayMs = 0)
	{
		ConfigureResponse(urlPattern, statusCode, responseBody, delayMs);
	}

	/// <summary>
	/// Configure a network failure for a specific endpoint pattern
	/// </summary>
	public void ConfigureNetworkFailure(string urlPattern, string errorMessage = "Network error")
	{
		ConfigureResponse(
			urlPattern,
			exception: new HttpRequestException(errorMessage));
	}

	/// <summary>
	/// Configure a slow response for a specific endpoint pattern
	/// </summary>
	public void ConfigureSlowResponse(
		string urlPattern,
		int delayMs,
		HttpStatusCode statusCode = HttpStatusCode.OK,
		object? responseBody = null)
	{
		ConfigureResponse(urlPattern, statusCode, responseBody, delayMs);
	}

	/// <summary>
	/// Clear all configured responses
	/// </summary>
	public void ClearResponses()
	{
		lock (_lock)
		{
			_responseConfigs.Clear();
		}
	}

	/// <summary>
	/// Clear request history
	/// </summary>
	public void ClearHistory()
	{
		lock (_lock)
		{
			_requestHistory.Clear();
		}
	}

	/// <summary>
	/// Get the number of requests made to a specific URL pattern
	/// </summary>
	public int GetRequestCount(string urlPattern)
	{
		lock (_lock)
		{
			return _requestHistory.Count(r => r.RequestUri?.ToString().Contains(urlPattern) == true);
		}
	}

	/// <summary>
	/// Get the last request made to a specific URL pattern
	/// </summary>
	public HttpRequestMessage? GetLastRequest(string urlPattern)
	{
		lock (_lock)
		{
			return _requestHistory
				.LastOrDefault(r => r.RequestUri?.ToString().Contains(urlPattern) == true);
		}
	}

	/// <summary>
	/// Get all requests made to a specific URL pattern
	/// </summary>
	public List<HttpRequestMessage> GetRequests(string urlPattern)
	{
		lock (_lock)
		{
			return _requestHistory
				.Where(r => r.RequestUri?.ToString().Contains(urlPattern) == true)
				.ToList();
		}
	}

	/// <summary>
	/// Override SendAsync to intercept HTTP requests
	/// </summary>
	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		// Record request
		lock (_lock)
		{
			_requestHistory.Add(request);
		}

		// Find matching response configuration
		ResponseConfig? config = null;
		lock (_lock)
		{
			var requestUrl = request.RequestUri?.ToString() ?? string.Empty;
			config = _responseConfigs
				.FirstOrDefault(kvp => requestUrl.Contains(kvp.Key))
				.Value;
		}

		// If no config found, return 404
		if (config == null)
		{
			return new HttpResponseMessage(HttpStatusCode.NotFound)
			{
				Content = new StringContent(
					JsonSerializer.Serialize(new { error = "No mock response configured for this endpoint" }),
					Encoding.UTF8,
					"application/json")
			};
		}

		// Simulate delay if configured
		if (config.DelayMs > 0)
		{
			await Task.Delay(config.DelayMs, cancellationToken);
		}

		// Throw exception if configured
		if (config.Exception != null)
		{
			throw config.Exception;
		}

		// Create response
		var response = new HttpResponseMessage(config.StatusCode);

		// Set response content
		if (config.ResponseBody != null)
		{
			string json;
			if (config.ResponseBody is string str)
			{
				json = str;
			}
			else
			{
				json = JsonSerializer.Serialize(config.ResponseBody);
			}

			response.Content = new StringContent(json, Encoding.UTF8, "application/json");
		}

		return response;
	}

	/// <summary>
	/// Response configuration
	/// </summary>
	private class ResponseConfig
	{
		public HttpStatusCode StatusCode { get; set; }
		public object? ResponseBody { get; set; }
		public int DelayMs { get; set; }
		public Exception? Exception { get; set; }
	}
}

/// <summary>
/// Extension methods for MockHttpMessageHandler
/// </summary>
public static class MockHttpMessageHandlerExtensions
{
	/// <summary>
	/// Create an HttpClient with the mock handler
	/// </summary>
	public static HttpClient CreateClient(this MockHttpMessageHandler handler, string baseAddress = "https://api.honua.io/")
	{
		var client = new HttpClient(handler)
		{
			BaseAddress = new Uri(baseAddress)
		};
		return client;
	}

	/// <summary>
	/// Assert that a request was made to a specific URL pattern
	/// </summary>
	public static void AssertRequestMade(this MockHttpMessageHandler handler, string urlPattern)
	{
		var count = handler.GetRequestCount(urlPattern);
		if (count == 0)
		{
			throw new InvalidOperationException($"Expected at least one request to '{urlPattern}', but none were made.");
		}
	}

	/// <summary>
	/// Assert that a specific number of requests were made to a URL pattern
	/// </summary>
	public static void AssertRequestCount(this MockHttpMessageHandler handler, string urlPattern, int expectedCount)
	{
		var actualCount = handler.GetRequestCount(urlPattern);
		if (actualCount != expectedCount)
		{
			throw new InvalidOperationException(
				$"Expected {expectedCount} request(s) to '{urlPattern}', but {actualCount} were made.");
		}
	}

	/// <summary>
	/// Assert that no requests were made to a specific URL pattern
	/// </summary>
	public static void AssertNoRequestMade(this MockHttpMessageHandler handler, string urlPattern)
	{
		AssertRequestCount(handler, urlPattern, 0);
	}
}
