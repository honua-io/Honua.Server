// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Intake.Documentation;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Honua.Server.Intake.Filters;

/// <summary>
/// Operation filter to add example request and response values to Swagger documentation.
/// </summary>
public class ExampleValuesOperationFilter : IOperationFilter
{
    /// <summary>
    /// Applies the filter to add example values to operations.
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var actionName = context.MethodInfo.Name;

        // Add examples based on operation
        AddRequestExamples(operation, actionName);
        AddResponseExamples(operation, actionName);
        AddCommonErrorExamples(operation);
    }

    private static void AddRequestExamples(OpenApiOperation operation, string actionName)
    {
        if (operation.RequestBody?.Content == null)
            return;

        foreach (var content in operation.RequestBody.Content)
        {
            var example = actionName switch
            {
                "StartConversation" => ApiExamples.GetStartConversationRequestExample(),
                "SendMessage" => ApiExamples.GetSendMessageRequestExample(),
                "TriggerBuild" => ApiExamples.GetTriggerBuildRequestExample(),
                _ => null
            };

            if (example != null)
            {
                content.Value.Example = example;
            }
        }
    }

    private static void AddResponseExamples(OpenApiOperation operation, string actionName)
    {
        if (operation.Responses == null)
            return;

        foreach (var response in operation.Responses)
        {
            if (response.Value.Content == null)
                continue;

            foreach (var content in response.Value.Content)
            {
                var example = (actionName, response.Key) switch
                {
                    ("StartConversation", "200") => ApiExamples.GetConversationResponseExample(),
                    ("SendMessage", "200") => ApiExamples.GetIntakeResponseExample(),
                    ("GetConversation", "200") => ApiExamples.GetConversationRecordExample(),
                    ("TriggerBuild", "200") => ApiExamples.GetTriggerBuildResponseExample(),
                    ("GetBuildStatus", "200") => ApiExamples.GetBuildStatusResponseExample(),
                    _ => null
                };

                if (example != null)
                {
                    content.Value.Example = example;
                }
            }
        }
    }

    private static void AddCommonErrorExamples(OpenApiOperation operation)
    {
        // Add standard error examples
        AddErrorExample(operation, "400", "Bad Request", "Invalid request parameters");
        AddErrorExample(operation, "401", "Unauthorized", "Missing or invalid authentication token");
        AddErrorExample(operation, "403", "Forbidden", "Insufficient permissions");
        AddErrorExample(operation, "404", "Not Found", "Resource not found");
        AddErrorExample(operation, "500", "Internal Server Error", "An unexpected error occurred");
    }

    private static void AddErrorExample(OpenApiOperation operation, string statusCode, string title, string detail)
    {
        if (!operation.Responses.TryGetValue(statusCode, out var response))
            return;

        if (response.Content == null || !response.Content.Any())
        {
            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType()
            };
        }

        foreach (var content in response.Content)
        {
            content.Value.Example = new OpenApiObject
            {
                ["error"] = new OpenApiString(detail),
                ["status"] = new OpenApiInteger(int.Parse(statusCode)),
                ["title"] = new OpenApiString(title),
                ["timestamp"] = new OpenApiString(DateTimeOffset.UtcNow.ToString("O"))
            };
        }
    }
}
