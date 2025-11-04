// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Intake.Models;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Honua.Server.Intake.Documentation;

/// <summary>
/// Provides example values for API documentation.
/// </summary>
public static class ApiExamples
{
    #region Intake API Examples

    /// <summary>
    /// Gets example for StartConversation request.
    /// </summary>
    public static IOpenApiAny GetStartConversationRequestExample()
    {
        return new OpenApiObject
        {
            ["customerId"] = new OpenApiString("cust_abc123def456")
        };
    }

    /// <summary>
    /// Gets example for ConversationResponse.
    /// </summary>
    public static IOpenApiAny GetConversationResponseExample()
    {
        return new OpenApiObject
        {
            ["conversationId"] = new OpenApiString("conv_xyz789abc123"),
            ["initialMessage"] = new OpenApiString(
                "Hi! I'm here to help you build a custom Honua Server tailored to your needs. " +
                "To get started, could you tell me what kind of geospatial services you're looking to deploy? " +
                "For example, are you:\n" +
                "- Migrating from ArcGIS Server?\n" +
                "- Starting fresh with OGC standards (WFS, WMS, WMTS, OGC API)?\n" +
                "- Need STAC catalog support?\n" +
                "- Something else?"
            ),
            ["startedAt"] = new OpenApiString("2025-10-29T10:30:00Z"),
            ["customerId"] = new OpenApiString("cust_abc123def456")
        };
    }

    /// <summary>
    /// Gets example for SendMessage request.
    /// </summary>
    public static IOpenApiAny GetSendMessageRequestExample()
    {
        return new OpenApiObject
        {
            ["conversationId"] = new OpenApiString("conv_xyz789abc123"),
            ["message"] = new OpenApiString("I need to deploy an ESRI-compatible server on AWS with PostgreSQL")
        };
    }

    /// <summary>
    /// Gets example for IntakeResponse (in-progress).
    /// </summary>
    public static IOpenApiAny GetIntakeResponseExample()
    {
        return new OpenApiObject
        {
            ["conversationId"] = new OpenApiString("conv_xyz789abc123"),
            ["message"] = new OpenApiString(
                "Great choice! ESRI REST API compatibility with PostgreSQL on AWS is a popular combination. " +
                "A few more questions:\n\n" +
                "1. How many concurrent users do you expect?\n" +
                "2. What's the approximate size of your spatial data?\n" +
                "3. Do you need real-time features or primarily read-only access?\n" +
                "4. Any preference for instance architecture? (ARM64 is 40% cheaper but slightly less compatible)"
            ),
            ["intakeComplete"] = new OpenApiBoolean(false),
            ["requirements"] = new OpenApiNull(),
            ["estimatedMonthlyCost"] = new OpenApiNull(),
            ["costBreakdown"] = new OpenApiNull(),
            ["timestamp"] = new OpenApiString("2025-10-29T10:31:15Z")
        };
    }

    /// <summary>
    /// Gets example for IntakeResponse (completed).
    /// </summary>
    public static IOpenApiAny GetIntakeResponseCompleteExample()
    {
        return new OpenApiObject
        {
            ["conversationId"] = new OpenApiString("conv_xyz789abc123"),
            ["message"] = new OpenApiString(
                "Perfect! I have everything I need. Based on our conversation, I recommend:\n\n" +
                "**Architecture:** ARM64 on AWS Graviton3 (40% cost savings)\n" +
                "**Protocols:** ESRI REST API, WFS 2.0, WMS 1.3.0\n" +
                "**Database:** PostgreSQL with PostGIS\n" +
                "**Resources:** 4 vCPU, 8GB RAM (t4g.large)\n" +
                "**Estimated Cost:** $95/month\n\n" +
                "Ready to proceed with the build?"
            ),
            ["intakeComplete"] = new OpenApiBoolean(true),
            ["requirements"] = new OpenApiObject
            {
                ["protocols"] = new OpenApiArray
                {
                    new OpenApiString("ESRI-REST"),
                    new OpenApiString("WFS-2.0"),
                    new OpenApiString("WMS-1.3.0")
                },
                ["databases"] = new OpenApiArray
                {
                    new OpenApiString("PostgreSQL-PostGIS")
                },
                ["cloudProvider"] = new OpenApiString("aws"),
                ["architecture"] = new OpenApiString("linux-arm64"),
                ["load"] = new OpenApiObject
                {
                    ["concurrentUsers"] = new OpenApiInteger(50),
                    ["requestsPerSecond"] = new OpenApiDouble(100),
                    ["dataVolumeGb"] = new OpenApiDouble(50),
                    ["classification"] = new OpenApiString("moderate")
                },
                ["tier"] = new OpenApiString("Pro"),
                ["advancedFeatures"] = new OpenApiArray(),
                ["notes"] = new OpenApiString("Customer prefers cost optimization with ARM64")
            },
            ["estimatedMonthlyCost"] = new OpenApiDouble(95.00),
            ["costBreakdown"] = new OpenApiObject
            {
                ["compute"] = new OpenApiDouble(50.00),
                ["storage"] = new OpenApiDouble(10.00),
                ["database"] = new OpenApiDouble(25.00),
                ["networking"] = new OpenApiDouble(10.00)
            },
            ["timestamp"] = new OpenApiString("2025-10-29T10:35:22Z")
        };
    }

    /// <summary>
    /// Gets example for ConversationRecord.
    /// </summary>
    public static IOpenApiAny GetConversationRecordExample()
    {
        return new OpenApiObject
        {
            ["conversationId"] = new OpenApiString("conv_xyz789abc123"),
            ["customerId"] = new OpenApiString("cust_abc123def456"),
            ["messagesJson"] = new OpenApiString(@"[
  {""role"":""assistant"",""content"":""Hi! I'm here to help you build a custom Honua Server...""},
  {""role"":""user"",""content"":""I need to deploy an ESRI-compatible server on AWS with PostgreSQL""},
  {""role"":""assistant"",""content"":""Great choice! A few more questions...""}
]"),
            ["status"] = new OpenApiString("active"),
            ["requirementsJson"] = new OpenApiNull(),
            ["createdAt"] = new OpenApiString("2025-10-29T10:30:00Z"),
            ["updatedAt"] = new OpenApiString("2025-10-29T10:35:22Z"),
            ["completedAt"] = new OpenApiNull()
        };
    }

    /// <summary>
    /// Gets example for TriggerBuild request.
    /// </summary>
    public static IOpenApiAny GetTriggerBuildRequestExample()
    {
        return new OpenApiObject
        {
            ["conversationId"] = new OpenApiString("conv_xyz789abc123"),
            ["customerId"] = new OpenApiString("cust_abc123def456"),
            ["buildName"] = new OpenApiString("production-geospatial-server"),
            ["tags"] = new OpenApiArray
            {
                new OpenApiString("production"),
                new OpenApiString("aws"),
                new OpenApiString("esri-compatible")
            }
        };
    }

    /// <summary>
    /// Gets example for TriggerBuild response.
    /// </summary>
    public static IOpenApiAny GetTriggerBuildResponseExample()
    {
        return new OpenApiObject
        {
            ["success"] = new OpenApiBoolean(true),
            ["buildId"] = new OpenApiString("build_def456ghi789"),
            ["manifest"] = new OpenApiObject
            {
                ["version"] = new OpenApiString("1.0"),
                ["name"] = new OpenApiString("production-geospatial-server"),
                ["architecture"] = new OpenApiString("linux-arm64"),
                ["modules"] = new OpenApiArray
                {
                    new OpenApiString("ESRI-REST"),
                    new OpenApiString("WFS-2.0"),
                    new OpenApiString("WMS-1.3.0")
                },
                ["databaseConnectors"] = new OpenApiArray
                {
                    new OpenApiString("PostgreSQL-PostGIS")
                },
                ["cloudTargets"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["provider"] = new OpenApiString("aws"),
                        ["region"] = new OpenApiString("us-west-2"),
                        ["instanceType"] = new OpenApiString("t4g.large"),
                        ["registryUrl"] = new OpenApiString("123456789012.dkr.ecr.us-west-2.amazonaws.com")
                    }
                },
                ["resources"] = new OpenApiObject
                {
                    ["minCpu"] = new OpenApiDouble(2),
                    ["minMemoryGb"] = new OpenApiDouble(4),
                    ["recommendedCpu"] = new OpenApiDouble(4),
                    ["recommendedMemoryGb"] = new OpenApiDouble(8),
                    ["storageGb"] = new OpenApiDouble(50)
                },
                ["tier"] = new OpenApiString("Pro"),
                ["generatedAt"] = new OpenApiString("2025-10-29T10:40:00Z")
            },
            ["registryResult"] = new OpenApiObject
            {
                ["success"] = new OpenApiBoolean(true),
                ["registryType"] = new OpenApiString("AwsEcr"),
                ["customerId"] = new OpenApiString("cust_abc123def456"),
                ["namespace"] = new OpenApiString("honua/cust_abc123def456"),
                ["credential"] = new OpenApiObject
                {
                    ["registryUrl"] = new OpenApiString("123456789012.dkr.ecr.us-west-2.amazonaws.com"),
                    ["username"] = new OpenApiString("AWS"),
                    ["password"] = new OpenApiString("eyJwYXlsb2FkIjoiZXlKMGIydGxiaUk2..."),
                    ["expiresAt"] = new OpenApiString("2025-10-30T10:40:00Z")
                },
                ["provisionedAt"] = new OpenApiString("2025-10-29T10:40:00Z")
            },
            ["triggeredAt"] = new OpenApiString("2025-10-29T10:40:00Z")
        };
    }

    /// <summary>
    /// Gets example for BuildStatus response.
    /// </summary>
    public static IOpenApiAny GetBuildStatusResponseExample()
    {
        return new OpenApiObject
        {
            ["buildId"] = new OpenApiString("build_def456ghi789"),
            ["status"] = new OpenApiString("building"),
            ["progress"] = new OpenApiInteger(45),
            ["currentStage"] = new OpenApiString("Installing database connectors"),
            ["imageReference"] = new OpenApiNull(),
            ["errorMessage"] = new OpenApiNull(),
            ["logsUrl"] = new OpenApiString("https://api.honua.io/api/builds/build_def456ghi789/logs"),
            ["startedAt"] = new OpenApiString("2025-10-29T10:40:00Z"),
            ["completedAt"] = new OpenApiNull()
        };
    }

    /// <summary>
    /// Gets example for BuildStatus response (completed).
    /// </summary>
    public static IOpenApiAny GetBuildStatusCompleteExample()
    {
        return new OpenApiObject
        {
            ["buildId"] = new OpenApiString("build_def456ghi789"),
            ["status"] = new OpenApiString("completed"),
            ["progress"] = new OpenApiInteger(100),
            ["currentStage"] = new OpenApiString("Build completed successfully"),
            ["imageReference"] = new OpenApiString(
                "123456789012.dkr.ecr.us-west-2.amazonaws.com/honua/cust_abc123def456/production-geospatial-server:latest-arm64"
            ),
            ["errorMessage"] = new OpenApiNull(),
            ["logsUrl"] = new OpenApiString("https://api.honua.io/api/builds/build_def456ghi789/logs"),
            ["startedAt"] = new OpenApiString("2025-10-29T10:40:00Z"),
            ["completedAt"] = new OpenApiString("2025-10-29T10:55:30Z")
        };
    }

    #endregion

    #region Registry API Examples

    /// <summary>
    /// Gets example for registry provisioning request.
    /// </summary>
    public static IOpenApiAny GetRegistryProvisionRequestExample()
    {
        return new OpenApiObject
        {
            ["customerId"] = new OpenApiString("cust_abc123def456"),
            ["registryType"] = new OpenApiString("AwsEcr"),
            ["region"] = new OpenApiString("us-west-2")
        };
    }

    /// <summary>
    /// Gets example for registry access validation.
    /// </summary>
    public static IOpenApiAny GetRegistryAccessExample()
    {
        return new OpenApiObject
        {
            ["accessGranted"] = new OpenApiBoolean(true),
            ["customerId"] = new OpenApiString("cust_abc123def456"),
            ["registryType"] = new OpenApiString("AwsEcr"),
            ["accessToken"] = new OpenApiString("eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."),
            ["tokenExpiresAt"] = new OpenApiString("2025-10-29T22:40:00Z"),
            ["denialReason"] = new OpenApiNull(),
            ["licenseTier"] = new OpenApiString("Pro")
        };
    }

    #endregion

    #region License API Examples

    /// <summary>
    /// Gets example for license generation request.
    /// </summary>
    public static IOpenApiAny GetLicenseGenerateRequestExample()
    {
        return new OpenApiObject
        {
            ["customerId"] = new OpenApiString("cust_abc123def456"),
            ["tier"] = new OpenApiString("Pro"),
            ["expirationMonths"] = new OpenApiInteger(12),
            ["features"] = new OpenApiArray
            {
                new OpenApiString("ESRI-REST"),
                new OpenApiString("WFS"),
                new OpenApiString("WMS"),
                new OpenApiString("WMTS"),
                new OpenApiString("OGC-API-Features")
            }
        };
    }

    /// <summary>
    /// Gets example for license response.
    /// </summary>
    public static IOpenApiAny GetLicenseResponseExample()
    {
        return new OpenApiObject
        {
            ["licenseId"] = new OpenApiString("lic_jkl012mno345"),
            ["customerId"] = new OpenApiString("cust_abc123def456"),
            ["tier"] = new OpenApiString("Pro"),
            ["licenseKey"] = new OpenApiString("HONUA-PRO-ABC123-DEF456-GHI789-JKL012"),
            ["features"] = new OpenApiArray
            {
                new OpenApiString("ESRI-REST"),
                new OpenApiString("WFS"),
                new OpenApiString("WMS"),
                new OpenApiString("WMTS"),
                new OpenApiString("OGC-API-Features")
            },
            ["issuedAt"] = new OpenApiString("2025-10-29T10:40:00Z"),
            ["expiresAt"] = new OpenApiString("2026-10-29T10:40:00Z"),
            ["status"] = new OpenApiString("active")
        };
    }

    #endregion

    #region Error Examples

    /// <summary>
    /// Gets example for validation error.
    /// </summary>
    public static IOpenApiAny GetValidationErrorExample()
    {
        return new OpenApiObject
        {
            ["error"] = new OpenApiString("Validation failed"),
            ["status"] = new OpenApiInteger(400),
            ["title"] = new OpenApiString("Bad Request"),
            ["errors"] = new OpenApiObject
            {
                ["ConversationId"] = new OpenApiArray
                {
                    new OpenApiString("The ConversationId field is required.")
                },
                ["Message"] = new OpenApiArray
                {
                    new OpenApiString("The Message field is required.")
                }
            },
            ["timestamp"] = new OpenApiString("2025-10-29T10:40:00Z")
        };
    }

    /// <summary>
    /// Gets example for resource not found error.
    /// </summary>
    public static IOpenApiAny GetNotFoundErrorExample()
    {
        return new OpenApiObject
        {
            ["error"] = new OpenApiString("Conversation conv_xyz789abc123 not found"),
            ["status"] = new OpenApiInteger(404),
            ["title"] = new OpenApiString("Not Found"),
            ["timestamp"] = new OpenApiString("2025-10-29T10:40:00Z")
        };
    }

    /// <summary>
    /// Gets example for rate limit error.
    /// </summary>
    public static IOpenApiAny GetRateLimitErrorExample()
    {
        return new OpenApiObject
        {
            ["error"] = new OpenApiString("Rate limit exceeded. Please try again later."),
            ["status"] = new OpenApiInteger(429),
            ["title"] = new OpenApiString("Too Many Requests"),
            ["retryAfter"] = new OpenApiInteger(60),
            ["limit"] = new OpenApiInteger(100),
            ["remaining"] = new OpenApiInteger(0),
            ["resetAt"] = new OpenApiString("2025-10-29T11:00:00Z"),
            ["timestamp"] = new OpenApiString("2025-10-29T10:40:00Z")
        };
    }

    #endregion
}
