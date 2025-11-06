using System.Net;

namespace Honua.MapSDK.Tests.TestHelpers;

/// <summary>
/// Mock HTTP message handler for testing HTTP requests without actual network calls
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
    private readonly List<HttpRequestMessage> _requests = new();

    /// <summary>
    /// All requests that have been made
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> Requests => _requests.AsReadOnly();

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return Task.FromResult(_responseFactory(request));
    }

    /// <summary>
    /// Clear all tracked requests
    /// </summary>
    public void ClearRequests()
    {
        _requests.Clear();
    }

    /// <summary>
    /// Create a handler that always returns success with JSON content
    /// </summary>
    public static MockHttpMessageHandler CreateJsonHandler(string jsonContent)
    {
        return new MockHttpMessageHandler(request =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
        });
    }

    /// <summary>
    /// Create a handler that returns different responses based on URL
    /// </summary>
    public static MockHttpMessageHandler CreateRoutingHandler(
        Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> routes)
    {
        return new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? "";

            foreach (var route in routes)
            {
                if (url.Contains(route.Key))
                {
                    return route.Value(request);
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
    }

    /// <summary>
    /// Create a handler that simulates network errors
    /// </summary>
    public static MockHttpMessageHandler CreateErrorHandler(HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
    {
        return new MockHttpMessageHandler(request =>
        {
            return new HttpResponseMessage(statusCode);
        });
    }
}
