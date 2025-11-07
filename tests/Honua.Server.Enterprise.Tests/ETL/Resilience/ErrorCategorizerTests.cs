// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net;
using System.Net.Http;
using Honua.Server.Enterprise.ETL.Resilience;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL.Resilience;

public class ErrorCategorizerTests
{
    [Fact]
    public void Categorize_TimeoutException_ReturnsTransient()
    {
        var ex = new TimeoutException("Connection timed out");

        var category = ErrorCategorizer.Categorize(ex);

        Assert.Equal(ErrorCategory.Transient, category);
    }

    [Fact]
    public void Categorize_OutOfMemoryException_ReturnsResource()
    {
        var ex = new OutOfMemoryException();

        var category = ErrorCategorizer.Categorize(ex);

        Assert.Equal(ErrorCategory.Resource, category);
    }

    [Fact]
    public void Categorize_UnauthorizedException_ReturnsConfiguration()
    {
        var ex = new UnauthorizedAccessException();

        var category = ErrorCategorizer.Categorize(ex);

        Assert.Equal(ErrorCategory.Configuration, category);
    }

    [Fact]
    public void Categorize_FormatException_ReturnsData()
    {
        var ex = new FormatException("Invalid data format");

        var category = ErrorCategorizer.Categorize(ex);

        Assert.Equal(ErrorCategory.Data, category);
    }

    [Fact]
    public void Categorize_NullReferenceException_ReturnsLogic()
    {
        var ex = new NullReferenceException();

        var category = ErrorCategorizer.Categorize(ex);

        Assert.Equal(ErrorCategory.Logic, category);
    }

    [Fact]
    public void CategorizeByMessage_TimeoutPattern_ReturnsTransient()
    {
        var message = "Connection timed out while reading data";

        var category = ErrorCategorizer.CategorizeByMessage(message);

        Assert.Equal(ErrorCategory.Transient, category);
    }

    [Fact]
    public void CategorizeByMessage_InvalidGeometry_ReturnsData()
    {
        var message = "Invalid geometry: coordinates are invalid";

        var category = ErrorCategorizer.CategorizeByMessage(message);

        Assert.Equal(ErrorCategory.Data, category);
    }

    [Fact]
    public void CategorizeByMessage_RateLimit_ReturnsResource()
    {
        var message = "Rate limit exceeded, try again later";

        var category = ErrorCategorizer.CategorizeByMessage(message);

        Assert.Equal(ErrorCategory.Resource, category);
    }

    [Fact]
    public void CategorizeByMessage_Unauthorized_ReturnsConfiguration()
    {
        var message = "Authentication failed: invalid API key";

        var category = ErrorCategorizer.CategorizeByMessage(message);

        Assert.Equal(ErrorCategory.Configuration, category);
    }

    [Fact]
    public void CategorizeByMessage_ServiceUnavailable_ReturnsExternal()
    {
        var message = "External service unavailable";

        var category = ErrorCategorizer.CategorizeByMessage(message);

        Assert.Equal(ErrorCategory.External, category);
    }

    [Fact]
    public void CategorizeByMessage_UnknownError_ReturnsUnknown()
    {
        var message = "Something unexpected happened";

        var category = ErrorCategorizer.CategorizeByMessage(message);

        Assert.Equal(ErrorCategory.Unknown, category);
    }

    [Theory]
    [InlineData(ErrorCategory.Transient, "This is a temporary error. The system will automatically retry this operation.")]
    [InlineData(ErrorCategory.Data, "This appears to be a data quality issue. Please check your input data for validity.")]
    [InlineData(ErrorCategory.Resource, "System resources are constrained. Consider reducing data volume or increasing system capacity.")]
    [InlineData(ErrorCategory.Configuration, "This appears to be a configuration issue. Please verify your settings and credentials.")]
    [InlineData(ErrorCategory.External, "An external service is unavailable. Please try again later or check service status.")]
    [InlineData(ErrorCategory.Logic, "This is an unexpected error. Please contact support with the error details.")]
    public void GetSuggestion_ReturnsAppropriateMessage(ErrorCategory category, string expectedSubstring)
    {
        var suggestion = ErrorCategorizer.GetSuggestion(category);

        Assert.Contains(expectedSubstring, suggestion);
    }
}
