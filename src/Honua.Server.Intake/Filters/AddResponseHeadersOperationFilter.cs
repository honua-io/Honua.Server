// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Honua.Server.Intake.Filters;

/// <summary>
/// Operation filter to document common response headers.
/// </summary>
public class AddResponseHeadersOperationFilter : IOperationFilter
{
    /// <summary>
    /// Applies the filter to add response headers to operations.
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add common response headers
        foreach (var response in operation.Responses.Values)
        {
            response.Headers ??= new Dictionary<string, OpenApiHeader>();

            // Rate limiting headers
            if (!response.Headers.ContainsKey("X-RateLimit-Limit"))
            {
                response.Headers.Add("X-RateLimit-Limit", new OpenApiHeader
                {
                    Description = "The maximum number of requests allowed in the current time window",
                    Schema = new OpenApiSchema { Type = "integer" }
                });
            }

            if (!response.Headers.ContainsKey("X-RateLimit-Remaining"))
            {
                response.Headers.Add("X-RateLimit-Remaining", new OpenApiHeader
                {
                    Description = "The number of requests remaining in the current time window",
                    Schema = new OpenApiSchema { Type = "integer" }
                });
            }

            if (!response.Headers.ContainsKey("X-RateLimit-Reset"))
            {
                response.Headers.Add("X-RateLimit-Reset", new OpenApiHeader
                {
                    Description = "The time when the rate limit will reset (Unix timestamp)",
                    Schema = new OpenApiSchema { Type = "integer" }
                });
            }

            // Request tracking
            if (!response.Headers.ContainsKey("X-Request-Id"))
            {
                response.Headers.Add("X-Request-Id", new OpenApiHeader
                {
                    Description = "Unique identifier for the request (for support/debugging)",
                    Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
                });
            }

            // API version
            if (!response.Headers.ContainsKey("X-API-Version"))
            {
                response.Headers.Add("X-API-Version", new OpenApiHeader
                {
                    Description = "The API version that processed this request",
                    Schema = new OpenApiSchema { Type = "string" }
                });
            }
        }
    }
}
