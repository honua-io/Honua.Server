// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Xml.Linq;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Provides unified error response builders for all API protocols.
/// Consolidates error response creation across JSON (GeoservicesREST, Admin, OGC JSON),
/// XML (WMS, WFS), and RFC 7807 Problem Details (OGC API).
/// </summary>
public static class ApiErrorResponse
{
    /// <summary>
    /// JSON error response builders for REST APIs (GeoservicesREST, Admin endpoints, etc.).
    /// Returns simple { error: "message" } format.
    /// </summary>
    public static class Json
    {
        /// <summary>
        /// Creates a 400 BadRequest response with a standardized JSON error message.
        /// Response format: { "error": "message" }
        /// </summary>
        /// <param name="message">The error message to include in the response.</param>
        /// <returns>A BadRequestObjectResult with the error message.</returns>
        public static BadRequestObjectResult BadRequest(string message)
        {
            return new BadRequestObjectResult(new { error = message });
        }

        /// <summary>
        /// Creates a 400 BadRequest IResult response with a standardized JSON error message.
        /// Response format: { "error": "message" }
        /// </summary>
        /// <param name="message">The error message to include in the response.</param>
        /// <returns>An IResult with 400 status and the error message.</returns>
        public static IResult BadRequestResult(string message)
        {
            return Results.BadRequest(new { error = message });
        }

        /// <summary>
        /// Creates a 404 NotFound response with a standardized JSON error message.
        /// Response format: { "error": "message" }
        /// </summary>
        /// <param name="message">The error message to include in the response.</param>
        /// <returns>A NotFound IResult with the error message.</returns>
        public static IResult NotFound(string message)
        {
            return Results.NotFound(new { error = message });
        }

        /// <summary>
        /// Creates a 404 NotFound response with a formatted message for a specific resource.
        /// Response format: { "error": "Resource 'identifier' not found." }
        /// </summary>
        /// <param name="resource">The type of resource (e.g., "Service", "Dataset", "Layer").</param>
        /// <param name="identifier">The identifier of the resource that was not found.</param>
        /// <returns>A NotFound IResult with a formatted error message.</returns>
        public static IResult NotFound(string resource, string identifier)
        {
            return Results.NotFound(new { error = $"{resource} '{identifier}' not found." });
        }

        /// <summary>
        /// Creates a 500 InternalServerError response with a standardized JSON error message.
        /// Response format: { "error": "message" }
        /// </summary>
        /// <param name="message">The error message to include in the response.</param>
        /// <returns>An IResult with 500 status and the error message.</returns>
        public static IResult InternalServerError(string message)
        {
            return Results.Json(
                new { error = message },
                statusCode: StatusCodes.Status500InternalServerError);
        }

        /// <summary>
        /// Creates a 409 Conflict response with a standardized JSON error message.
        /// Response format: { "error": "message" }
        /// </summary>
        /// <param name="message">The error message to include in the response.</param>
        /// <returns>An IResult with 409 status and the error message.</returns>
        public static IResult Conflict(string message)
        {
            return Results.Json(
                new { error = message },
                statusCode: StatusCodes.Status409Conflict);
        }

        /// <summary>
        /// Creates a 422 UnprocessableEntity response with a standardized JSON error message.
        /// Response format: { "error": "message" }
        /// </summary>
        /// <param name="message">The error message to include in the response.</param>
        /// <returns>An IResult with 422 status and the error message.</returns>
        public static IResult UnprocessableEntity(string message)
        {
            return Results.Json(
                new { error = message },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        /// <summary>
        /// Creates a 403 Forbidden response with a standardized JSON error message.
        /// Response format: { "error": "message" }
        /// </summary>
        /// <param name="message">The error message to include in the response.</param>
        /// <returns>An IResult with 403 status and the error message.</returns>
        public static IResult Forbidden(string message)
        {
            return Results.Json(
                new { error = message },
                statusCode: StatusCodes.Status403Forbidden);
        }
    }

    /// <summary>
    /// OGC XML exception response builders for WMS and WFS services.
    /// Generates standards-compliant ServiceExceptionReport (WMS) and ExceptionReport (WFS) XML.
    /// </summary>
    public static class OgcXml
    {
        /// <summary>
        /// Creates a WMS ServiceExceptionReport XML response.
        /// Conforms to OGC WMS 1.3.0 specification for error reporting.
        /// Uses 'ogc' namespace prefix as per WMS 1.3.0 specification.
        /// </summary>
        /// <param name="code">The exception code (e.g., "InvalidParameterValue", "MissingParameterValue").</param>
        /// <param name="message">The human-readable error message.</param>
        /// <param name="version">The WMS version (default: "1.3.0").</param>
        /// <returns>An IResult containing the XML exception response with 400 status.</returns>
        public static IResult WmsException(string code, string message, string version = "1.3.0")
        {
            // WMS 1.3.0 Compliance: Use 'ogc' namespace for ServiceExceptionReport
            var ogcNs = XNamespace.Get("http://www.opengis.net/ogc");
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(ogcNs + "ServiceExceptionReport",
                    new XAttribute("version", version),
                    new XAttribute(XNamespace.Xmlns + "ogc", ogcNs),
                    new XElement(ogcNs + "ServiceException",
                        new XAttribute("code", code),
                        message)));

            var xml = document.ToString(SaveOptions.DisableFormatting);
            return Results.Content(xml, "application/vnd.ogc.se_xml", statusCode: StatusCodes.Status400BadRequest);
        }

        /// <summary>
        /// Creates a WFS ExceptionReport XML response (OWS Common format).
        /// Conforms to OGC Web Feature Service and OWS Common specifications.
        /// </summary>
        /// <param name="code">The exception code (e.g., "InvalidParameterValue", "OperationParsingFailed").</param>
        /// <param name="locator">The parameter or location that caused the error (optional).</param>
        /// <param name="message">The human-readable error message.</param>
        /// <param name="version">The exception report version (default: "2.0.0").</param>
        /// <returns>An IResult containing the XML exception response with 400 status.</returns>
        public static IResult WfsException(string code, string? locator, string message, string version = "2.0.0")
        {
            var owsNs = XNamespace.Get("http://www.opengis.net/ows/1.1");
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(owsNs + "ExceptionReport",
                    new XAttribute("version", version),
                    new XAttribute(XNamespace.Xmlns + "ows", owsNs),
                    new XElement(owsNs + "Exception",
                        new XAttribute("exceptionCode", code),
                        locator.IsNullOrWhiteSpace() ? null : new XAttribute("locator", locator),
                        new XElement(owsNs + "ExceptionText", message))));

            var xml = document.ToString(SaveOptions.DisableFormatting);
            return Results.Content(xml, "application/xml", statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// RFC 7807 Problem Details response builders for OGC API and modern REST APIs.
    /// Provides structured error information with type URIs and extensions.
    /// </summary>
    public static class ProblemDetails
    {
        /// <summary>
        /// OGC exception type URIs as defined in OGC API specifications.
        /// Based on http://www.opengis.net/def/exceptions/ogcapi-* pattern.
        /// </summary>
        public static class Types
        {
            /// <summary>
            /// Base URI for OGC API Features exception types.
            /// </summary>
            private const string OgcApiFeaturesBase = "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0";

            /// <summary>
            /// Base URI for OGC Common exception types.
            /// </summary>
            private const string OgcCommonBase = "http://www.opengis.net/def/exceptions/ogcapi-common/1.0";

            public const string InvalidParameter = $"{OgcApiFeaturesBase}/invalid-parameter";
            public const string NotFound = $"{OgcApiFeaturesBase}/not-found";
            public const string Conflict = $"{OgcApiFeaturesBase}/conflict";
            public const string OperationNotSupported = $"{OgcApiFeaturesBase}/operation-not-supported";
            public const string InvalidValue = $"{OgcApiFeaturesBase}/invalid-value";
            public const string ServerError = $"{OgcCommonBase}/server-error";
            public const string NoApplicableCode = $"{OgcCommonBase}/no-applicable-code";
            public const string NotAcceptable = $"{OgcApiFeaturesBase}/not-acceptable";
            public const string UnsupportedMediaType = $"{OgcApiFeaturesBase}/unsupported-media-type";
            public const string InvalidCrs = $"{OgcApiFeaturesBase}/invalid-crs";
            public const string InvalidBbox = $"{OgcApiFeaturesBase}/invalid-bbox";
            public const string InvalidDatetime = $"{OgcApiFeaturesBase}/invalid-datetime";
            public const string LimitOutOfRange = $"{OgcApiFeaturesBase}/limit-out-of-range";
            public const string Forbidden = $"{OgcApiFeaturesBase}/forbidden";
            public const string Unauthorized = $"{OgcCommonBase}/unauthorized";
        }

        /// <summary>
        /// Creates an RFC 7807 validation problem response (400 BadRequest).
        /// </summary>
        /// <param name="detail">Detailed explanation of the validation error.</param>
        /// <param name="parameterName">The name of the parameter that failed validation (optional).</param>
        /// <param name="type">The problem type URI (defaults to InvalidParameter).</param>
        /// <param name="title">The problem title (defaults to "Invalid Parameter").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult InvalidParameter(
            string detail,
            string? parameterName = null,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.InvalidParameter,
                Title = title ?? "Invalid Parameter",
                Status = StatusCodes.Status400BadRequest,
                Detail = detail
            };

            if (parameterName.HasValue())
            {
                problem.Extensions["parameter"] = parameterName;
            }

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 not found problem response (404 NotFound).
        /// </summary>
        /// <param name="detail">Detailed explanation of what was not found.</param>
        /// <param name="type">The problem type URI (defaults to NotFound).</param>
        /// <param name="title">The problem title (defaults to "Not Found").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult NotFound(
            string detail,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.NotFound,
                Title = title ?? "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = detail
            };

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 conflict problem response (409 Conflict).
        /// </summary>
        /// <param name="detail">Detailed explanation of the conflict.</param>
        /// <param name="type">The problem type URI (defaults to Conflict).</param>
        /// <param name="title">The problem title (defaults to "Conflict").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult Conflict(
            string detail,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.Conflict,
                Title = title ?? "Conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = detail
            };

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 server error problem response (500 InternalServerError).
        /// </summary>
        /// <param name="detail">Detailed explanation of the server error.</param>
        /// <param name="type">The problem type URI (defaults to ServerError).</param>
        /// <param name="title">The problem title (defaults to "Server Error").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult ServerError(
            string detail,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.ServerError,
                Title = title ?? "Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = detail
            };

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 not acceptable problem response (406 NotAcceptable).
        /// </summary>
        /// <param name="detail">Detailed explanation of why the request is not acceptable.</param>
        /// <param name="type">The problem type URI (defaults to NotAcceptable).</param>
        /// <param name="title">The problem title (defaults to "Not Acceptable").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult NotAcceptable(
            string detail,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.NotAcceptable,
                Title = title ?? "Not Acceptable",
                Status = StatusCodes.Status406NotAcceptable,
                Detail = detail
            };

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 unsupported media type problem response (415 UnsupportedMediaType).
        /// </summary>
        /// <param name="detail">Detailed explanation of the unsupported media type.</param>
        /// <param name="type">The problem type URI (defaults to UnsupportedMediaType).</param>
        /// <param name="title">The problem title (defaults to "Unsupported Media Type").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult UnsupportedMediaType(
            string detail,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.UnsupportedMediaType,
                Title = title ?? "Unsupported Media Type",
                Status = StatusCodes.Status415UnsupportedMediaType,
                Detail = detail
            };

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 operation not supported problem response (501 NotImplemented).
        /// </summary>
        /// <param name="detail">Detailed explanation of the unsupported operation.</param>
        /// <param name="type">The problem type URI (defaults to OperationNotSupported).</param>
        /// <param name="title">The problem title (defaults to "Operation Not Supported").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult OperationNotSupported(
            string detail,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.OperationNotSupported,
                Title = title ?? "Operation Not Supported",
                Status = StatusCodes.Status501NotImplemented,
                Detail = detail
            };

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 invalid value problem response (400 BadRequest).
        /// </summary>
        /// <param name="detail">Detailed explanation of the invalid value.</param>
        /// <param name="parameterName">The name of the parameter with an invalid value (optional).</param>
        /// <param name="type">The problem type URI (defaults to InvalidValue).</param>
        /// <param name="title">The problem title (defaults to "Invalid Value").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult InvalidValue(
            string detail,
            string? parameterName = null,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.InvalidValue,
                Title = title ?? "Invalid Value",
                Status = StatusCodes.Status400BadRequest,
                Detail = detail
            };

            if (parameterName.HasValue())
            {
                problem.Extensions["parameter"] = parameterName;
            }

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 forbidden problem response (403 Forbidden).
        /// </summary>
        /// <param name="detail">Detailed explanation of why access is forbidden.</param>
        /// <param name="type">The problem type URI (defaults to Forbidden).</param>
        /// <param name="title">The problem title (defaults to "Forbidden").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult Forbidden(
            string detail,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.Forbidden,
                Title = title ?? "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = detail
            };

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 unauthorized problem response (401 Unauthorized).
        /// </summary>
        /// <param name="detail">Detailed explanation of the authentication requirement.</param>
        /// <param name="type">The problem type URI (defaults to Unauthorized).</param>
        /// <param name="title">The problem title (defaults to "Unauthorized").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult Unauthorized(
            string detail,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.Unauthorized,
                Title = title ?? "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = detail
            };

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 invalid CRS problem response (400 BadRequest).
        /// </summary>
        /// <param name="detail">Detailed explanation of the invalid CRS.</param>
        /// <param name="parameterName">The name of the CRS parameter (optional).</param>
        /// <param name="type">The problem type URI (defaults to InvalidCrs).</param>
        /// <param name="title">The problem title (defaults to "Invalid CRS").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult InvalidCrs(
            string detail,
            string? parameterName = null,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.InvalidCrs,
                Title = title ?? "Invalid CRS",
                Status = StatusCodes.Status400BadRequest,
                Detail = detail
            };

            if (parameterName.HasValue())
            {
                problem.Extensions["parameter"] = parameterName;
            }

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 invalid bbox problem response (400 BadRequest).
        /// </summary>
        /// <param name="detail">Detailed explanation of the invalid bounding box.</param>
        /// <param name="parameterName">The name of the bbox parameter (optional).</param>
        /// <param name="type">The problem type URI (defaults to InvalidBbox).</param>
        /// <param name="title">The problem title (defaults to "Invalid Bbox").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult InvalidBbox(
            string detail,
            string? parameterName = null,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.InvalidBbox,
                Title = title ?? "Invalid Bbox",
                Status = StatusCodes.Status400BadRequest,
                Detail = detail
            };

            if (parameterName.HasValue())
            {
                problem.Extensions["parameter"] = parameterName;
            }

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 invalid datetime problem response (400 BadRequest).
        /// </summary>
        /// <param name="detail">Detailed explanation of the invalid datetime.</param>
        /// <param name="parameterName">The name of the datetime parameter (optional).</param>
        /// <param name="type">The problem type URI (defaults to InvalidDatetime).</param>
        /// <param name="title">The problem title (defaults to "Invalid Datetime").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult InvalidDatetime(
            string detail,
            string? parameterName = null,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.InvalidDatetime,
                Title = title ?? "Invalid Datetime",
                Status = StatusCodes.Status400BadRequest,
                Detail = detail
            };

            if (parameterName.HasValue())
            {
                problem.Extensions["parameter"] = parameterName;
            }

            return Results.Problem(problem);
        }

        /// <summary>
        /// Creates an RFC 7807 limit out of range problem response (400 BadRequest).
        /// </summary>
        /// <param name="detail">Detailed explanation of the limit violation.</param>
        /// <param name="parameterName">The name of the limit parameter (optional).</param>
        /// <param name="type">The problem type URI (defaults to LimitOutOfRange).</param>
        /// <param name="title">The problem title (defaults to "Limit Out Of Range").</param>
        /// <returns>An IResult with RFC 7807 problem details.</returns>
        public static IResult LimitOutOfRange(
            string detail,
            string? parameterName = null,
            string? type = null,
            string? title = null)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type ?? Types.LimitOutOfRange,
                Title = title ?? "Limit Out Of Range",
                Status = StatusCodes.Status400BadRequest,
                Detail = detail
            };

            if (parameterName.HasValue())
            {
                problem.Extensions["parameter"] = parameterName;
            }

            return Results.Problem(problem);
        }
    }

    /// <summary>
    /// Carto API error response builders.
    /// Provides Carto-compatible error responses with error/detail structure.
    /// </summary>
    public static class Carto
    {
        /// <summary>
        /// Creates a 400 BadRequest response with Carto-compatible error structure.
        /// Response format: { "error": "message", "detail": "detailMessage" }
        /// </summary>
        /// <param name="error">The error message.</param>
        /// <param name="detail">Optional detailed error information.</param>
        /// <returns>An IResult with 400 status and Carto error structure.</returns>
        public static IResult BadRequest(string error, string? detail = null)
        {
            return Results.BadRequest(new { error, detail });
        }

        /// <summary>
        /// Creates an error response with custom status code and Carto-compatible error structure.
        /// Response format: { "error": "message", "detail": "detailMessage" }
        /// </summary>
        /// <param name="error">The error message.</param>
        /// <param name="detail">Optional detailed error information.</param>
        /// <param name="statusCode">The HTTP status code to return.</param>
        /// <returns>An IResult with the specified status and Carto error structure.</returns>
        public static IResult Error(string error, string? detail, int statusCode)
        {
            var status = statusCode >= 400 ? statusCode : StatusCodes.Status400BadRequest;
            return Results.Json(new { error, detail }, statusCode: status);
        }
    }
}
