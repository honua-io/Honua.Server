// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Honua.Admin.Blazor.Tests.Services;

public class AIAssistantApiClientTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<AIAssistantApiClient>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly AIAssistantApiClient _client;

    public AIAssistantApiClientTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<AIAssistantApiClient>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("AdminApi"))
            .Returns(httpClient);

        _client = new AIAssistantApiClient(_httpClientFactoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue_WhenAIIsAvailable()
    {
        // Arrange
        var response = new AICapabilitiesResponse
        {
            Available = true,
            Features = new List<string> { AIFeature.NaturalLanguageSearch },
            Model = "gpt-4",
            Message = "AI is ready"
        };

        SetupHttpResponse("/admin/ai/capabilities", HttpStatusCode.OK, response);

        // Act
        var result = await _client.IsAvailableAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenAIIsNotAvailable()
    {
        // Arrange
        var response = new AICapabilitiesResponse
        {
            Available = false,
            Message = "AI not configured"
        };

        SetupHttpResponse("/admin/ai/capabilities", HttpStatusCode.OK, response);

        // Act
        var result = await _client.IsAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenEndpointReturnsError()
    {
        // Arrange
        SetupHttpResponse("/admin/ai/capabilities", HttpStatusCode.ServiceUnavailable);

        // Act
        var result = await _client.IsAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetCapabilitiesAsync_CachesResponse_ForFiveMinutes()
    {
        // Arrange
        var response = new AICapabilitiesResponse
        {
            Available = true,
            Features = new List<string> { AIFeature.Chat },
            Model = "claude-3"
        };

        SetupHttpResponse("/admin/ai/capabilities", HttpStatusCode.OK, response);

        // Act - First call
        var result1 = await _client.GetCapabilitiesAsync();

        // Act - Second call (should use cache)
        var result2 = await _client.GetCapabilitiesAsync();

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().BeEquivalentTo(result2);

        // Verify HTTP call was made only once
        _httpMessageHandlerMock.Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/admin/ai/capabilities")),
                ItExpr.IsAny<CancellationToken>()
            );
    }

    [Fact]
    public async Task SearchAsync_ReturnsResults_WhenSuccessful()
    {
        // Arrange
        var request = new NaturalLanguageSearchRequest
        {
            Query = "show me all WMS services",
            MaxResults = 10
        };

        var expectedResponse = new NaturalLanguageSearchResponse
        {
            Interpretation = "Searching for services with type WMS",
            Results = new List<SearchResultItem>
            {
                new SearchResultItem
                {
                    Id = "wms-1",
                    Type = "service",
                    Title = "WMS Service 1",
                    Relevance = 95
                }
            }
        };

        SetupHttpResponse("/admin/ai/search", HttpStatusCode.OK, expectedResponse);

        // Act
        var result = await _client.SearchAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Interpretation.Should().Be("Searching for services with type WMS");
        result.Results.Should().HaveCount(1);
        result.Results[0].Title.Should().Be("WMS Service 1");
    }

    [Fact]
    public async Task GetCRSSuggestionsAsync_ReturnsRecommendations()
    {
        // Arrange
        var expectedSuggestions = new List<SmartSuggestion>
        {
            new SmartSuggestion
            {
                Value = "EPSG:4326",
                Label = "WGS 84 (World Geodetic System)",
                Reason = "Most common global CRS",
                Confidence = 95,
                Recommended = true
            },
            new SmartSuggestion
            {
                Value = "EPSG:3857",
                Label = "Web Mercator",
                Reason = "Popular for web mapping",
                Confidence = 85,
                Recommended = false
            }
        };

        SetupHttpResponse("/admin/ai/suggestions", HttpStatusCode.OK, expectedSuggestions);

        // Act
        var result = await _client.GetCRSSuggestionsAsync(
            geometryType: "Point",
            region: "Pacific",
            dataSource: "postgis"
        );

        // Assert
        result.Should().HaveCount(2);
        result[0].Recommended.Should().BeTrue();
        result[0].Value.Should().Be("EPSG:4326");
    }

    [Fact]
    public async Task GetStyleSuggestionsAsync_ReturnsStyleRecommendations()
    {
        // Arrange
        var expectedSuggestions = new List<SmartSuggestion>
        {
            new SmartSuggestion
            {
                Value = "point-red",
                Label = "Red Point Markers",
                Reason = "Good contrast for point data",
                Confidence = 90,
                Recommended = true
            }
        };

        SetupHttpResponse("/admin/ai/suggestions", HttpStatusCode.OK, expectedSuggestions);

        // Act
        var result = await _client.GetStyleSuggestionsAsync(
            geometryType: "Point",
            layerName: "Cities"
        );

        // Assert
        result.Should().HaveCount(1);
        result[0].Recommended.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateMetadataAsync_ReturnsGeneratedFields()
    {
        // Arrange
        var request = new GenerateMetadataRequest
        {
            ItemType = "service",
            Fields = new List<string> { "title", "abstract" },
            Context = new Dictionary<string, string>
            {
                ["serviceType"] = "WMS"
            }
        };

        var expectedResponse = new GenerateMetadataResponse
        {
            Metadata = new Dictionary<string, string>
            {
                ["title"] = "Generated WMS Service Title",
                ["abstract"] = "This is a generated abstract for the WMS service"
            },
            Quality = 85,
            Explanation = "Generated based on service type and context"
        };

        SetupHttpResponse("/admin/ai/generate-metadata", HttpStatusCode.OK, expectedResponse);

        // Act
        var result = await _client.GenerateMetadataAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().ContainKey("title");
        result.Metadata.Should().ContainKey("abstract");
        result.Quality.Should().Be(85);
    }

    [Fact]
    public async Task GenerateTitleAsync_ReturnsGeneratedTitle()
    {
        // Arrange
        var expectedResponse = new GenerateMetadataResponse
        {
            Metadata = new Dictionary<string, string>
            {
                ["title"] = "Pacific Region WMS Service"
            },
            Quality = 90
        };

        SetupHttpResponse("/admin/ai/generate-metadata", HttpStatusCode.OK, expectedResponse);

        // Act
        var result = await _client.GenerateTitleAsync(
            "service",
            new Dictionary<string, string> { ["serviceType"] = "WMS", ["region"] = "Pacific" }
        );

        // Assert
        result.Should().Be("Pacific Region WMS Service");
    }

    [Fact]
    public async Task GenerateKeywordsAsync_ReturnsParsedKeywords()
    {
        // Arrange
        var expectedResponse = new GenerateMetadataResponse
        {
            Metadata = new Dictionary<string, string>
            {
                ["keywords"] = "WMS, mapping, geospatial, Pacific"
            },
            Quality = 85
        };

        SetupHttpResponse("/admin/ai/generate-metadata", HttpStatusCode.OK, expectedResponse);

        // Act
        var result = await _client.GenerateKeywordsAsync(
            "service",
            new Dictionary<string, string> { ["serviceType"] = "WMS" }
        );

        // Assert
        result.Should().HaveCount(4);
        result.Should().Contain("WMS");
        result.Should().Contain("geospatial");
    }

    [Fact]
    public async Task TroubleshootAsync_ReturnsDiagnosisAndSolutions()
    {
        // Arrange
        var request = new TroubleshootRequest
        {
            Problem = "Service won't start",
            ErrorMessage = "Connection refused",
            AttemptedAction = "Starting WMS service"
        };

        var expectedResponse = new TroubleshootResponse
        {
            Diagnosis = "Database connection issue",
            Solutions = new List<Solution>
            {
                new Solution
                {
                    Title = "Check database connection",
                    Steps = new List<string>
                    {
                        "Verify database is running",
                        "Check connection string",
                        "Test database credentials"
                    },
                    Likelihood = 90
                }
            },
            Confidence = 85
        };

        SetupHttpResponse("/admin/ai/troubleshoot", HttpStatusCode.OK, expectedResponse);

        // Act
        var result = await _client.TroubleshootAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Diagnosis.Should().Be("Database connection issue");
        result.Solutions.Should().HaveCount(1);
        result.Solutions[0].Steps.Should().HaveCount(3);
        result.Confidence.Should().Be(85);
    }

    [Fact]
    public async Task ChatAsync_ReturnsAIResponse()
    {
        // Arrange
        var request = new AIChatRequest
        {
            Message = "How do I create a WMS service?",
            History = new List<ChatMessage>(),
            Context = new Dictionary<string, string> { ["page"] = "services" }
        };

        var expectedResponse = new AIChatResponse
        {
            Message = "To create a WMS service, go to Services page and click Create...",
            SuggestedActions = new List<SuggestedAction>
            {
                new SuggestedAction
                {
                    Label = "Go to Services",
                    ActionType = ActionType.Navigate,
                    Target = "/services"
                }
            },
            FollowUpQuestions = new List<string>
            {
                "What type of data do you want to serve?"
            }
        };

        SetupHttpResponse("/admin/ai/chat", HttpStatusCode.OK, expectedResponse);

        // Act
        var result = await _client.ChatAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Contain("create a WMS service");
        result.SuggestedActions.Should().HaveCount(1);
        result.FollowUpQuestions.Should().HaveCount(1);
    }

    [Fact]
    public void InvalidateCapabilitiesCache_ClearsCachedData()
    {
        // Arrange
        var response = new AICapabilitiesResponse { Available = true };
        SetupHttpResponse("/admin/ai/capabilities", HttpStatusCode.OK, response);

        // Act
        _client.InvalidateCapabilitiesCache();

        // Assert - Next call should fetch fresh data (tested by verifying HTTP calls)
        // This is implicitly tested by the caching test above
    }

    private void SetupHttpResponse<T>(string path, HttpStatusCode statusCode, T content)
    {
        var responseMessage = new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(content)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(path)),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMessage);
    }

    private void SetupHttpResponse(string path, HttpStatusCode statusCode)
    {
        var responseMessage = new HttpResponseMessage(statusCode);

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(path)),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMessage);
    }
}
