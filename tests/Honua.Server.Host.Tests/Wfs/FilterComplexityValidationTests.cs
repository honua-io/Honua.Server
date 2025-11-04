using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Wfs;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

/// <summary>
/// Integration tests for filter complexity validation in WFS operations.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class FilterComplexityValidationTests
{
    private readonly LayerDefinition _testLayer;

    public FilterComplexityValidationTests()
    {
        _testLayer = new LayerDefinition
        {
            Id = "test-layer",
            Name = "Test Layer",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "int", IsNullable = false },
                new() { Name = "name", DataType = "string", IsNullable = true },
                new() { Name = "value", DataType = "double", IsNullable = true },
                new() { Name = "category", DataType = "string", IsNullable = true },
                new() { Name = "active", DataType = "boolean", IsNullable = true }
            }
        };
    }

    #region Simple Filter Tests

    [Fact]
    public async Task BuildFilterAsync_SimpleFilter_PassesComplexityCheck()
    {
        // Arrange
        var options = new WfsOptions
        {
            MaxFilterComplexity = 100,
            EnableComplexityCheck = true
        };

        var request = CreateMockRequest("GET");
        var query = CreateQueryCollection(new Dictionary<string, string>
        {
            ["cql_filter"] = "name = 'test'"
        });

        // Act
        var filter = await WfsHelpers.BuildFilterAsync(request, query, _testLayer, options, CancellationToken.None);

        // Assert
        Assert.NotNull(filter);
        Assert.NotNull(filter.Expression);
    }

    [Fact]
    public async Task BuildFilterAsync_ModerateFilter_PassesComplexityCheck()
    {
        // Arrange
        var options = new WfsOptions
        {
            MaxFilterComplexity = 100,
            EnableComplexityCheck = true
        };

        var request = CreateMockRequest("GET");
        var query = CreateQueryCollection(new Dictionary<string, string>
        {
            ["cql_filter"] = "(name = 'test' AND value > 100) OR (category = 'important' AND active = true)"
        });

        // Act
        var filter = await WfsHelpers.BuildFilterAsync(request, query, _testLayer, options, CancellationToken.None);

        // Assert
        Assert.NotNull(filter);
        Assert.NotNull(filter.Expression);
    }

    #endregion

    #region Complex Filter Tests

    [Fact]
    public async Task BuildFilterAsync_VeryComplexFilter_ThrowsException()
    {
        // Arrange
        var options = new WfsOptions
        {
            MaxFilterComplexity = 100,
            EnableComplexityCheck = true
        };

        var request = CreateMockRequest("GET");
        var query = CreateQueryCollection(new Dictionary<string, string>
        {
            ["cql_filter"] = "((name = 'a' OR name = 'b' OR name = 'c') AND (value > 1 OR value < 100)) OR " +
                            "((category = 'x' AND active = true) OR (category = 'y' AND active = false)) OR " +
                            "((name = 'd' OR name = 'e') AND (value > 200 OR value < 300 OR value = 250)) OR " +
                            "((category = 'z' OR category = 'w') AND (active = true OR name = 'special'))"
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WfsHelpers.BuildFilterAsync(request, query, _testLayer, options, CancellationToken.None));

        Assert.Contains("Filter complexity", exception.Message);
        Assert.Contains("exceeds maximum", exception.Message);
    }

    [Fact]
    public async Task BuildFilterAsync_ComplexFilterWithHighLimit_Passes()
    {
        // Arrange
        var options = new WfsOptions
        {
            MaxFilterComplexity = 500, // Increased limit
            EnableComplexityCheck = true
        };

        var request = CreateMockRequest("GET");
        var query = CreateQueryCollection(new Dictionary<string, string>
        {
            ["cql_filter"] = "((name = 'a' OR name = 'b' OR name = 'c') AND (value > 1 OR value < 100)) OR " +
                            "((category = 'x' AND active = true) OR (category = 'y' AND active = false))"
        });

        // Act
        var filter = await WfsHelpers.BuildFilterAsync(request, query, _testLayer, options, CancellationToken.None);

        // Assert
        Assert.NotNull(filter);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task BuildFilterAsync_ComplexityCheckDisabled_AllowsAnyFilter()
    {
        // Arrange
        var options = new WfsOptions
        {
            MaxFilterComplexity = 10, // Very low limit
            EnableComplexityCheck = false // But disabled
        };

        var request = CreateMockRequest("GET");
        var query = CreateQueryCollection(new Dictionary<string, string>
        {
            ["cql_filter"] = "((name = 'a' OR name = 'b' OR name = 'c') AND (value > 1 OR value < 100)) OR " +
                            "((category = 'x' AND active = true) OR (category = 'y' AND active = false))"
        });

        // Act
        var filter = await WfsHelpers.BuildFilterAsync(request, query, _testLayer, options, CancellationToken.None);

        // Assert
        Assert.NotNull(filter);
    }

    [Fact]
    public async Task BuildFilterAsync_NullOptions_UsesDefaults()
    {
        // Arrange
        var request = CreateMockRequest("GET");
        var query = CreateQueryCollection(new Dictionary<string, string>
        {
            ["cql_filter"] = "name = 'test'"
        });

        // Act
        var filter = await WfsHelpers.BuildFilterAsync(request, query, _testLayer, options: null, CancellationToken.None);

        // Assert
        Assert.NotNull(filter);
    }

    [Fact]
    public async Task BuildFilterAsync_DefaultOptions_EnforcesComplexity()
    {
        // Arrange - Default options should have complexity checking enabled
        var request = CreateMockRequest("GET");
        var query = CreateQueryCollection(new Dictionary<string, string>
        {
            ["cql_filter"] = "((name = 'a' OR name = 'b' OR name = 'c') AND (value > 1 OR value < 100)) OR " +
                            "((category = 'x' AND active = true) OR (category = 'y' AND active = false)) OR " +
                            "((name = 'd' OR name = 'e') AND (value > 200 OR value < 300 OR value = 250)) OR " +
                            "((category = 'z' OR category = 'w') AND (active = true OR name = 'special'))"
        });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WfsHelpers.BuildFilterAsync(request, query, _testLayer, options: null, CancellationToken.None));
    }

    #endregion

    #region XML Filter Tests

    [Fact]
    public async Task BuildFilterAsync_SimpleXmlFilter_PassesComplexityCheck()
    {
        // Arrange
        var options = new WfsOptions
        {
            MaxFilterComplexity = 100,
            EnableComplexityCheck = true
        };

        var xml = @"
            <Filter xmlns='http://www.opengis.net/fes/2.0'>
                <PropertyIsEqualTo>
                    <PropertyName>name</PropertyName>
                    <Literal>test</Literal>
                </PropertyIsEqualTo>
            </Filter>";

        var request = CreateMockRequest("POST", xml);
        var query = CreateQueryCollection(new Dictionary<string, string>());

        // Act
        var filter = await WfsHelpers.BuildFilterAsync(request, query, _testLayer, options, CancellationToken.None);

        // Assert
        Assert.NotNull(filter);
    }

    [Fact]
    public async Task BuildFilterAsync_ComplexXmlFilter_ThrowsException()
    {
        // Arrange
        var options = new WfsOptions
        {
            MaxFilterComplexity = 50, // Lower limit to trigger failure
            EnableComplexityCheck = true
        };

        var xml = @"
            <Filter xmlns='http://www.opengis.net/fes/2.0'>
                <And>
                    <Or>
                        <PropertyIsEqualTo>
                            <PropertyName>name</PropertyName>
                            <Literal>a</Literal>
                        </PropertyIsEqualTo>
                        <PropertyIsEqualTo>
                            <PropertyName>name</PropertyName>
                            <Literal>b</Literal>
                        </PropertyIsEqualTo>
                        <PropertyIsEqualTo>
                            <PropertyName>name</PropertyName>
                            <Literal>c</Literal>
                        </PropertyIsEqualTo>
                    </Or>
                    <Or>
                        <PropertyIsGreaterThan>
                            <PropertyName>value</PropertyName>
                            <Literal>100</Literal>
                        </PropertyIsGreaterThan>
                        <PropertyIsLessThan>
                            <PropertyName>value</PropertyName>
                            <Literal>200</Literal>
                        </PropertyIsLessThan>
                        <PropertyIsEqualTo>
                            <PropertyName>value</PropertyName>
                            <Literal>150</Literal>
                        </PropertyIsEqualTo>
                    </Or>
                    <Or>
                        <PropertyIsEqualTo>
                            <PropertyName>category</PropertyName>
                            <Literal>x</Literal>
                        </PropertyIsEqualTo>
                        <PropertyIsEqualTo>
                            <PropertyName>category</PropertyName>
                            <Literal>y</Literal>
                        </PropertyIsEqualTo>
                    </Or>
                </And>
            </Filter>";

        var request = CreateMockRequest("POST", xml);
        var query = CreateQueryCollection(new Dictionary<string, string>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WfsHelpers.BuildFilterAsync(request, query, _testLayer, options, CancellationToken.None));
    }

    #endregion

    #region Query Parameter Variants

    [Fact]
    public async Task BuildFilterAsync_FilterParameter_ChecksComplexity()
    {
        // Arrange
        var options = new WfsOptions { MaxFilterComplexity = 10, EnableComplexityCheck = true };
        var request = CreateMockRequest("GET");
        var query = CreateQueryCollection(new Dictionary<string, string>
        {
            ["filter"] = "name = 'a' OR name = 'b' OR name = 'c' OR name = 'd'"
        });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WfsHelpers.BuildFilterAsync(request, query, _testLayer, options, CancellationToken.None));
    }

    [Fact]
    public async Task BuildFilterAsync_UppercaseFILTER_ChecksComplexity()
    {
        // Arrange
        var options = new WfsOptions { MaxFilterComplexity = 10, EnableComplexityCheck = true };
        var request = CreateMockRequest("GET");
        var query = CreateQueryCollection(new Dictionary<string, string>
        {
            ["FILTER"] = "name = 'a' OR name = 'b' OR name = 'c' OR name = 'd'"
        });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WfsHelpers.BuildFilterAsync(request, query, _testLayer, options, CancellationToken.None));
    }

    #endregion

    #region Helper Methods

    private static HttpRequest CreateMockRequest(string method, string? body = null)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = method;

        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            request.Body = new MemoryStream(bytes);
            request.ContentLength = bytes.Length;
        }
        else
        {
            request.Body = new MemoryStream();
        }

        return request;
    }

    private static IQueryCollection CreateQueryCollection(Dictionary<string, string> parameters)
    {
        var dict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
        foreach (var kvp in parameters)
        {
            dict[kvp.Key] = new Microsoft.Extensions.Primitives.StringValues(kvp.Value);
        }

        return new QueryCollection(dict);
    }

    #endregion
}
