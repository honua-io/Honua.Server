// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.Http;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Helper methods for creating OGC API problem detail responses.
/// This class now delegates to ApiErrorResponse.ProblemDetails for consistency across the application.
/// </summary>
[Obsolete("Use ApiErrorResponse.ProblemDetails instead for new code. This class is maintained for backward compatibility.")]
internal static class OgcProblemDetails
{
    /// <summary>
    /// OGC exception type URIs as defined in OGC API specifications.
    /// Based on http://www.opengis.net/def/exceptions/ogcapi-* pattern.
    /// </summary>
    [Obsolete("Use ApiErrorResponse.ProblemDetails.Types instead.")]
    public static class ExceptionTypes
    {
        public const string InvalidParameter = ApiErrorResponse.ProblemDetails.Types.InvalidParameter;
        public const string NotFound = ApiErrorResponse.ProblemDetails.Types.NotFound;
        public const string Conflict = ApiErrorResponse.ProblemDetails.Types.Conflict;
        public const string OperationNotSupported = ApiErrorResponse.ProblemDetails.Types.OperationNotSupported;
        public const string InvalidValue = ApiErrorResponse.ProblemDetails.Types.InvalidValue;
        public const string ServerError = ApiErrorResponse.ProblemDetails.Types.ServerError;
        public const string NoApplicableCode = ApiErrorResponse.ProblemDetails.Types.NoApplicableCode;
        public const string NotAcceptable = ApiErrorResponse.ProblemDetails.Types.NotAcceptable;
        public const string UnsupportedMediaType = ApiErrorResponse.ProblemDetails.Types.UnsupportedMediaType;
        public const string InvalidCrs = ApiErrorResponse.ProblemDetails.Types.InvalidCrs;
        public const string InvalidBbox = ApiErrorResponse.ProblemDetails.Types.InvalidBbox;
        public const string InvalidDatetime = ApiErrorResponse.ProblemDetails.Types.InvalidDatetime;
        public const string LimitOutOfRange = ApiErrorResponse.ProblemDetails.Types.LimitOutOfRange;
        public const string Forbidden = ApiErrorResponse.ProblemDetails.Types.Forbidden;
        public const string Unauthorized = ApiErrorResponse.ProblemDetails.Types.Unauthorized;
    }

    public static IResult CreateValidationProblem(string detail, string? parameterName = null)
    {
        return ApiErrorResponse.ProblemDetails.InvalidParameter(detail, parameterName);
    }

    public static IResult CreateNotFoundProblem(string detail)
    {
        return ApiErrorResponse.ProblemDetails.NotFound(detail);
    }

    public static IResult CreateConflictProblem(string detail)
    {
        return ApiErrorResponse.ProblemDetails.Conflict(detail);
    }

    public static IResult CreateServerErrorProblem(string detail)
    {
        return ApiErrorResponse.ProblemDetails.ServerError(detail);
    }

    public static IResult CreateNotAcceptableProblem(string detail)
    {
        return ApiErrorResponse.ProblemDetails.NotAcceptable(detail);
    }

    public static IResult CreateUnsupportedMediaTypeProblem(string detail)
    {
        return ApiErrorResponse.ProblemDetails.UnsupportedMediaType(detail);
    }

    public static IResult CreateOperationNotSupportedProblem(string detail)
    {
        return ApiErrorResponse.ProblemDetails.OperationNotSupported(detail);
    }

    public static IResult CreateInvalidValueProblem(string detail, string? parameterName = null)
    {
        return ApiErrorResponse.ProblemDetails.InvalidValue(detail, parameterName);
    }

    public static IResult CreateForbiddenProblem(string detail)
    {
        return ApiErrorResponse.ProblemDetails.Forbidden(detail);
    }

    public static IResult CreateUnauthorizedProblem(string detail)
    {
        return ApiErrorResponse.ProblemDetails.Unauthorized(detail);
    }

    public static IResult CreateInvalidCrsProblem(string detail, string? parameterName = null)
    {
        return ApiErrorResponse.ProblemDetails.InvalidCrs(detail, parameterName);
    }

    public static IResult CreateInvalidBboxProblem(string detail, string? parameterName = null)
    {
        return ApiErrorResponse.ProblemDetails.InvalidBbox(detail, parameterName);
    }

    public static IResult CreateInvalidDatetimeProblem(string detail, string? parameterName = null)
    {
        return ApiErrorResponse.ProblemDetails.InvalidDatetime(detail, parameterName);
    }

    public static IResult CreateLimitOutOfRangeProblem(string detail, string? parameterName = null)
    {
        return ApiErrorResponse.ProblemDetails.LimitOutOfRange(detail, parameterName);
    }

    /// <summary>
    /// Creates a problem details response with the specified parameters.
    /// </summary>
    /// <param name="title">The problem title.</param>
    /// <param name="detail">The problem detail message.</param>
    /// <param name="status">The HTTP status code.</param>
    /// <param name="parameterName">Optional parameter name that caused the problem.</param>
    /// <returns>A problem details object.</returns>
    public static object Create(string title, string detail, int status, string? parameterName = null)
    {
        var problem = new
        {
            type = GetTypeForStatus(status),
            title,
            detail,
            status,
            parameter = parameterName
        };

        return problem;
    }

    private static string GetTypeForStatus(int status)
    {
        return status switch
        {
            400 => ExceptionTypes.InvalidParameter,
            404 => ExceptionTypes.NotFound,
            406 => ExceptionTypes.NotAcceptable,
            409 => ExceptionTypes.Conflict,
            415 => ExceptionTypes.UnsupportedMediaType,
            _ => ExceptionTypes.NoApplicableCode
        };
    }
}
