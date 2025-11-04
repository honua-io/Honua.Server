// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Honua.Server.Intake.Filters;

/// <summary>
/// Document filter to add descriptions to API tags.
/// </summary>
public class TagDescriptionDocumentFilter : IDocumentFilter
{
    /// <summary>
    /// Applies the filter to add tag descriptions.
    /// </summary>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Tags = new List<OpenApiTag>
        {
            new OpenApiTag
            {
                Name = "Intake",
                Description = @"
**AI-Guided Build Configuration**

The Intake API provides an intelligent conversational interface for configuring custom Honua Server deployments.
Through natural language interaction, the AI agent:

- Identifies required protocols and APIs (ESRI REST, WFS, WMS, WMTS, OGC API Features/Tiles/Maps, etc.)
- Determines optimal database integrations (PostgreSQL, BigQuery, Snowflake, etc.)
- Recommends cloud platforms and architectures based on your needs
- Estimates costs with detailed breakdowns
- Generates optimized build manifests

**Typical Workflow:**
1. Start a new conversation
2. Answer the AI's questions about your requirements
3. Review the extracted requirements and cost estimate
4. Trigger the build when ready
5. Monitor build progress and receive container images
",
                ExternalDocs = new OpenApiExternalDocs
                {
                    Description = "Intake API Guide",
                    Url = new Uri("https://docs.honua.io/api/intake")
                }
            },
            new OpenApiTag
            {
                Name = "Build",
                Description = @"
**Container Image Build Management**

The Build API manages the construction and delivery of custom container images. Features include:

- Intelligent build caching (instant delivery for matching configurations)
- Multi-architecture support (ARM64, x64)
- Cloud-specific optimizations
- Real-time build status tracking
- Automated registry deployment
- Build artifact management
",
                ExternalDocs = new OpenApiExternalDocs
                {
                    Description = "Build API Guide",
                    Url = new Uri("https://docs.honua.io/api/build")
                }
            },
            new OpenApiTag
            {
                Name = "License",
                Description = @"
**License and Credential Management**

The License API handles:

- License generation for different tiers (Core, Pro, Enterprise, Enterprise ASP)
- License validation and activation
- Credential provisioning and rotation
- Usage tracking and compliance
- License expiration management
",
                ExternalDocs = new OpenApiExternalDocs
                {
                    Description = "License API Guide",
                    Url = new Uri("https://docs.honua.io/api/license")
                }
            },
            new OpenApiTag
            {
                Name = "Registry",
                Description = @"
**Container Registry Management**

The Registry API provides:

- Multi-cloud registry provisioning (AWS ECR, Azure ACR, GCP Artifact Registry, GitHub Container Registry)
- Automated credential generation and rotation
- Registry namespace management
- Access control and authentication
- Cross-registry synchronization
",
                ExternalDocs = new OpenApiExternalDocs
                {
                    Description = "Registry API Guide",
                    Url = new Uri("https://docs.honua.io/api/registry")
                }
            },
            new OpenApiTag
            {
                Name = "Admin",
                Description = @"
**System Administration**

The Admin API offers:

- Build queue monitoring and management
- System health checks and metrics
- Configuration management
- User and customer management
- Analytics and reporting
- Audit logs
",
                ExternalDocs = new OpenApiExternalDocs
                {
                    Description = "Admin API Guide",
                    Url = new Uri("https://docs.honua.io/api/admin")
                }
            }
        };
    }
}
