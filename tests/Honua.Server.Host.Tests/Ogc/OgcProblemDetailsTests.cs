using System;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Performance;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc;

[Collection("HostTests")]
[Trait("Category", "Integration")]
public class OgcProblemDetailsTests
{
    [Fact]
    public void CreateValidationProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Parameter value is invalid";
        const string paramName = "limit";

        // Act
        var result = OgcProblemDetails.CreateValidationProblem(detail, paramName);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-parameter");
        problemDetails.Title.Should().Be("Invalid Parameter");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().ContainKey("parameter");
        problemDetails.Extensions["parameter"].Should().Be(paramName);
    }

    [Fact]
    public void CreateValidationProblem_WithoutParameter_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Request is invalid";

        // Act
        var result = OgcProblemDetails.CreateValidationProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-parameter");
        problemDetails.Title.Should().Be("Invalid Parameter");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().NotContainKey("parameter");
    }

    [Fact]
    public void CreateNotFoundProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Collection 'test-collection' was not found";

        // Act
        var result = OgcProblemDetails.CreateNotFoundProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/not-found");
        problemDetails.Title.Should().Be("Not Found");
        problemDetails.Status.Should().Be(StatusCodes.Status404NotFound);
        problemDetails.Detail.Should().Be(detail);
    }

    [Fact]
    public void CreateConflictProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Feature with id 'feature-123' already exists";

        // Act
        var result = OgcProblemDetails.CreateConflictProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/conflict");
        problemDetails.Title.Should().Be("Conflict");
        problemDetails.Status.Should().Be(StatusCodes.Status409Conflict);
        problemDetails.Detail.Should().Be(detail);
    }

    [Fact]
    public void CreateServerErrorProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "An internal server error occurred";

        // Act
        var result = OgcProblemDetails.CreateServerErrorProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-common/1.0/server-error");
        problemDetails.Title.Should().Be("Server Error");
        problemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Detail.Should().Be(detail);
    }

    [Fact]
    public void CreateNotAcceptableProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Requested format 'text/xml' is not supported";

        // Act
        var result = OgcProblemDetails.CreateNotAcceptableProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/not-acceptable");
        problemDetails.Title.Should().Be("Not Acceptable");
        problemDetails.Status.Should().Be(StatusCodes.Status406NotAcceptable);
        problemDetails.Detail.Should().Be(detail);
    }

    [Fact]
    public void CreateUnsupportedMediaTypeProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Media type 'application/xml' is not supported";

        // Act
        var result = OgcProblemDetails.CreateUnsupportedMediaTypeProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/unsupported-media-type");
        problemDetails.Title.Should().Be("Unsupported Media Type");
        problemDetails.Status.Should().Be(StatusCodes.Status415UnsupportedMediaType);
        problemDetails.Detail.Should().Be(detail);
    }

    [Fact]
    public void CreateOperationNotSupportedProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "PATCH operation is not supported on this resource";

        // Act
        var result = OgcProblemDetails.CreateOperationNotSupportedProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/operation-not-supported");
        problemDetails.Title.Should().Be("Operation Not Supported");
        problemDetails.Status.Should().Be(StatusCodes.Status501NotImplemented);
        problemDetails.Detail.Should().Be(detail);
    }

    [Fact]
    public void CreateInvalidValueProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Value must be between 1 and 10000";
        const string paramName = "limit";

        // Act
        var result = OgcProblemDetails.CreateInvalidValueProblem(detail, paramName);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-value");
        problemDetails.Title.Should().Be("Invalid Value");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().ContainKey("parameter");
        problemDetails.Extensions["parameter"].Should().Be(paramName);
    }

    [Fact]
    public void CreateInvalidValueProblem_WithoutParameter_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Value format is invalid";

        // Act
        var result = OgcProblemDetails.CreateInvalidValueProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-value");
        problemDetails.Title.Should().Be("Invalid Value");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().NotContainKey("parameter");
    }

    [Fact]
    public void ExceptionTypes_InvalidParameter_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.InvalidParameter.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-parameter");
    }

    [Fact]
    public void ExceptionTypes_NotFound_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.NotFound.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/not-found");
    }

    [Fact]
    public void ExceptionTypes_Conflict_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.Conflict.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/conflict");
    }

    [Fact]
    public void ExceptionTypes_ServerError_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.ServerError.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-common/1.0/server-error");
    }

    [Fact]
    public void ExceptionTypes_NotAcceptable_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.NotAcceptable.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/not-acceptable");
    }

    [Fact]
    public void ExceptionTypes_InvalidCrs_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.InvalidCrs.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-crs");
    }

    [Fact]
    public void ExceptionTypes_InvalidBbox_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.InvalidBbox.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-bbox");
    }

    [Fact]
    public void ExceptionTypes_InvalidDatetime_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.InvalidDatetime.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-datetime");
    }

    [Fact]
    public void ExceptionTypes_LimitOutOfRange_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.LimitOutOfRange.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/limit-out-of-range");
    }

    [Fact]
    public void ExceptionTypes_Forbidden_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.Forbidden.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/forbidden");
    }

    [Fact]
    public void ExceptionTypes_Unauthorized_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.Unauthorized.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-common/1.0/unauthorized");
    }

    [Fact]
    public void ExceptionTypes_UnsupportedMediaType_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.UnsupportedMediaType.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/unsupported-media-type");
    }

    [Fact]
    public void ExceptionTypes_OperationNotSupported_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.OperationNotSupported.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/operation-not-supported");
    }

    [Fact]
    public void ExceptionTypes_InvalidValue_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.InvalidValue.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-value");
    }

    [Fact]
    public void ExceptionTypes_NoApplicableCode_HasCorrectUri()
    {
        // Assert
        OgcProblemDetails.ExceptionTypes.NoApplicableCode.Should().Be(
            "http://www.opengis.net/def/exceptions/ogcapi-common/1.0/no-applicable-code");
    }

    [Fact]
    public void CreateForbiddenProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Insufficient permissions to access this resource";

        // Act
        var result = OgcProblemDetails.CreateForbiddenProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/forbidden");
        problemDetails.Title.Should().Be("Forbidden");
        problemDetails.Status.Should().Be(StatusCodes.Status403Forbidden);
        problemDetails.Detail.Should().Be(detail);
    }

    [Fact]
    public void CreateUnauthorizedProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Authentication is required to access this resource";

        // Act
        var result = OgcProblemDetails.CreateUnauthorizedProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-common/1.0/unauthorized");
        problemDetails.Title.Should().Be("Unauthorized");
        problemDetails.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problemDetails.Detail.Should().Be(detail);
    }

    [Fact]
    public void CreateInvalidCrsProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "CRS 'EPSG:99999' is not supported";
        const string paramName = "crs";

        // Act
        var result = OgcProblemDetails.CreateInvalidCrsProblem(detail, paramName);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-crs");
        problemDetails.Title.Should().Be("Invalid CRS");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().ContainKey("parameter");
        problemDetails.Extensions["parameter"].Should().Be(paramName);
    }

    [Fact]
    public void CreateInvalidCrsProblem_WithoutParameter_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "CRS format is invalid";

        // Act
        var result = OgcProblemDetails.CreateInvalidCrsProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-crs");
        problemDetails.Title.Should().Be("Invalid CRS");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().NotContainKey("parameter");
    }

    [Fact]
    public void CreateInvalidBboxProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Bbox coordinates are invalid";
        const string paramName = "bbox";

        // Act
        var result = OgcProblemDetails.CreateInvalidBboxProblem(detail, paramName);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-bbox");
        problemDetails.Title.Should().Be("Invalid Bbox");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().ContainKey("parameter");
        problemDetails.Extensions["parameter"].Should().Be(paramName);
    }

    [Fact]
    public void CreateInvalidBboxProblem_WithoutParameter_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Bbox format is invalid";

        // Act
        var result = OgcProblemDetails.CreateInvalidBboxProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-bbox");
        problemDetails.Title.Should().Be("Invalid Bbox");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().NotContainKey("parameter");
    }

    [Fact]
    public void CreateInvalidDatetimeProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Datetime '2024-13-45T99:99:99Z' is invalid";
        const string paramName = "datetime";

        // Act
        var result = OgcProblemDetails.CreateInvalidDatetimeProblem(detail, paramName);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-datetime");
        problemDetails.Title.Should().Be("Invalid Datetime");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().ContainKey("parameter");
        problemDetails.Extensions["parameter"].Should().Be(paramName);
    }

    [Fact]
    public void CreateInvalidDatetimeProblem_WithoutParameter_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Datetime format is invalid";

        // Act
        var result = OgcProblemDetails.CreateInvalidDatetimeProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-datetime");
        problemDetails.Title.Should().Be("Invalid Datetime");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().NotContainKey("parameter");
    }

    [Fact]
    public void CreateLimitOutOfRangeProblem_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Limit must be between 1 and 10000";
        const string paramName = "limit";

        // Act
        var result = OgcProblemDetails.CreateLimitOutOfRangeProblem(detail, paramName);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/limit-out-of-range");
        problemDetails.Title.Should().Be("Limit Out Of Range");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().ContainKey("parameter");
        problemDetails.Extensions["parameter"].Should().Be(paramName);
    }

    [Fact]
    public void CreateLimitOutOfRangeProblem_WithoutParameter_SetsCorrectOgcTypeUri()
    {
        // Arrange
        const string detail = "Limit value is out of acceptable range";

        // Act
        var result = OgcProblemDetails.CreateLimitOutOfRangeProblem(detail);

        // Assert
        var problemDetails = ExtractProblemDetails(result);
        problemDetails.Should().NotBeNull();
        problemDetails!.Type.Should().Be("http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/limit-out-of-range");
        problemDetails.Title.Should().Be("Limit Out Of Range");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be(detail);
        problemDetails.Extensions.Should().NotContainKey("parameter");
    }

    /// <summary>
    /// Helper method to extract ProblemDetails from IResult.
    /// This is a simplified version for testing purposes.
    /// </summary>
    private static ProblemDetails? ExtractProblemDetails(IResult result)
    {
        // Use reflection to access the internal ObjectResult
        var resultType = result.GetType();
        var statusCodeProperty = resultType.GetProperty("StatusCode");
        var problemProperty = resultType.GetProperty("ProblemDetails");

        if (problemProperty != null)
        {
            return problemProperty.GetValue(result) as ProblemDetails;
        }

        // Fallback: try to execute the result and capture the response
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new System.IO.MemoryStream();

        // Execute the result
        var executeMethod = resultType.GetMethod("ExecuteAsync");
        if (executeMethod != null)
        {
            var task = executeMethod.Invoke(result, new object[] { httpContext }) as System.Threading.Tasks.Task;
            task?.GetAwaiter().GetResult();

            // Try to read ProblemDetails from response
            httpContext.Response.Body.Position = 0;
            using var reader = new System.IO.StreamReader(httpContext.Response.Body);
            var json = reader.ReadToEnd();

            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    return JsonSerializer.Deserialize<ProblemDetails>(json, JsonSerializerOptionsRegistry.DevTooling);
                }
                catch
                {
                    // Failed to deserialize
                }
            }
        }

        return null;
    }
}
