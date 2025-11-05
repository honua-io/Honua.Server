// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using RichardSzalay.MockHttp;
using System.Net;
using System.Text.Json;

namespace Honua.Admin.Blazor.Tests.Infrastructure;

/// <summary>
/// Factory for creating mock HttpClient instances for testing API clients.
/// Uses MockHttp to simulate HTTP responses.
/// </summary>
public class MockHttpClientFactory
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly string _baseUrl;

    public MockHttpClientFactory(string baseUrl = "https://localhost:5001")
    {
        _baseUrl = baseUrl;
        _mockHandler = new MockHttpMessageHandler();
    }

    /// <summary>
    /// Creates an HttpClient with the mock handler.
    /// </summary>
    public HttpClient CreateClient()
    {
        return new HttpClient(_mockHandler)
        {
            BaseAddress = new Uri(_baseUrl)
        };
    }

    /// <summary>
    /// Configures a mock GET request that returns JSON.
    /// </summary>
    public MockHttpClientFactory MockGetJson<T>(string url, T responseData, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(responseData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHandler
            .When(HttpMethod.Get, $"{_baseUrl}{url}")
            .Respond(statusCode, "application/json", json);

        return this;
    }

    /// <summary>
    /// Configures a mock POST request that returns JSON.
    /// </summary>
    public MockHttpClientFactory MockPostJson<T>(string url, T responseData, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(responseData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHandler
            .When(HttpMethod.Post, $"{_baseUrl}{url}")
            .Respond(statusCode, "application/json", json);

        return this;
    }

    /// <summary>
    /// Configures a mock PUT request that returns JSON.
    /// </summary>
    public MockHttpClientFactory MockPutJson<T>(string url, T responseData, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(responseData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHandler
            .When(HttpMethod.Put, $"{_baseUrl}{url}")
            .Respond(statusCode, "application/json", json);

        return this;
    }

    /// <summary>
    /// Configures a mock DELETE request.
    /// </summary>
    public MockHttpClientFactory MockDelete(string url, HttpStatusCode statusCode = HttpStatusCode.NoContent)
    {
        _mockHandler
            .When(HttpMethod.Delete, $"{_baseUrl}{url}")
            .Respond(statusCode);

        return this;
    }

    /// <summary>
    /// Configures a mock request that returns an error.
    /// </summary>
    public MockHttpClientFactory MockError(HttpMethod method, string url, HttpStatusCode statusCode = HttpStatusCode.InternalServerError, string errorMessage = "Server error")
    {
        _mockHandler
            .When(method, $"{_baseUrl}{url}")
            .Respond(statusCode, "application/json", $"{{\"error\": \"{errorMessage}\"}}");

        return this;
    }

    /// <summary>
    /// Configures a mock request that throws an exception.
    /// </summary>
    public MockHttpClientFactory MockException(HttpMethod method, string url, Exception exception)
    {
        _mockHandler
            .When(method, $"{_baseUrl}{url}")
            .Throw(exception);

        return this;
    }

    /// <summary>
    /// Gets the underlying MockHttpMessageHandler for advanced scenarios.
    /// </summary>
    public MockHttpMessageHandler GetHandler() => _mockHandler;
}
