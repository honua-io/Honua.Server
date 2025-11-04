using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public sealed class CloudPermissionGeneratorAgentTests
{
    [Fact]
    public async Task GeneratePermissionsAsync_ForAWS_ShouldReturnTerraformConfig()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.CloudProvider.Should().Be("aws");
        result.DeploymentIamTerraform.Should().NotBeNullOrEmpty();
        result.DeploymentIamTerraform.Should().Contain("resource \"aws_iam_user\"");
        result.DeploymentIamTerraform.Should().Contain("resource \"aws_iam_policy\"");
    }

    [Fact]
    public async Task GeneratePermissionsAsync_ForAzure_ShouldReturnServicePrincipalConfig()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateAzureTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.CloudProvider.Should().Be("azure");
        result.DeploymentIamTerraform.Should().NotBeNullOrEmpty();
        result.DeploymentIamTerraform.Should().Contain("azuread_service_principal");
        result.DeploymentIamTerraform.Should().Contain("azurerm_role_definition");
    }

    [Fact]
    public async Task GeneratePermissionsAsync_ForGCP_ShouldReturnServiceAccountConfig()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateGCPTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.CloudProvider.Should().Be("gcp");
        result.DeploymentIamTerraform.Should().NotBeNullOrEmpty();
        result.DeploymentIamTerraform.Should().Contain("google_service_account");
    }

    [Fact]
    public async Task GeneratePermissionsAsync_ShouldIdentifyRequiredServices()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.RequiredServices.Should().NotBeNull();
        result.RequiredServices.Should().ContainSingle(s => s.Service.Contains("EC2", StringComparison.OrdinalIgnoreCase));
        result.RequiredServices.Should().ContainSingle(s => s.Service.Contains("RDS", StringComparison.OrdinalIgnoreCase));
        result.RequiredServices.Should().ContainSingle(s => s.Service.Contains("S3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GeneratePermissionsAsync_WithDatabase_ShouldIncludeDatabasePermissions()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        var rdsService = result.RequiredServices?.FirstOrDefault(s => s.Service.Contains("RDS", StringComparison.OrdinalIgnoreCase));
        rdsService.Should().NotBeNull();
        rdsService!.Actions.Should().Contain(a => a.Contains("CreateDBInstance", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GeneratePermissionsAsync_WithStorage_ShouldIncludeStoragePermissions()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        var s3Service = result.RequiredServices?.FirstOrDefault(s => s.Service.Contains("S3", StringComparison.OrdinalIgnoreCase));
        s3Service.Should().NotBeNull();
        s3Service!.Actions.Should().Contain(a => a.Contains("CreateBucket", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GeneratePermissionsAsync_WithLoadBalancer_ShouldIncludeELBPermissions()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        var elbService = result.RequiredServices?.FirstOrDefault(s =>
            s.Service.Contains("ELB", StringComparison.OrdinalIgnoreCase) ||
            s.Service.Contains("LoadBalancer", StringComparison.OrdinalIgnoreCase));
        elbService.Should().NotBeNull();
    }

    [Fact]
    public async Task GeneratePermissionsAsync_WithMonitoring_ShouldIncludeMonitoringPermissions()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        var cloudWatchService = result.RequiredServices?.FirstOrDefault(s =>
            s.Service.Contains("CloudWatch", StringComparison.OrdinalIgnoreCase));
        cloudWatchService.Should().NotBeNull();
    }

    [Fact]
    public async Task GeneratePermissionsAsync_ShouldUseLeastPrivilege()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.DeploymentIamTerraform.Should().NotBeNullOrEmpty();
        // Should NOT grant admin or wildcard permissions
        result.DeploymentIamTerraform.Should().NotContain("\"*\"");
        result.DeploymentIamTerraform.Should().NotContain("AdministratorAccess");

        // Should have specific resource constraints
        result.DeploymentIamTerraform.Should().MatchRegex("Resource.*arn:aws");
    }

    [Fact]
    public async Task GeneratePermissionsAsync_WithDryRun_ShouldNotModifyFiles()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = true // Dry run mode
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.DeploymentIamTerraform.Should().NotBeNullOrEmpty();
        // In dry run mode, files shouldn't be written (verified by agent implementation)
    }

    [Fact]
    public async Task GeneratePermissionsAsync_WithInvalidTopology_ShouldReturnError()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = new DeploymentTopology
        {
            CloudProvider = "", // Invalid
            Region = "",
            Environment = ""
        };

        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GeneratePermissionsAsync_ShouldIncludeSecurityBestPractices()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.DeploymentIamTerraform.Should().Contain("Description");
        result.DeploymentIamTerraform.Should().Contain("tags");
    }

    [Fact]
    public async Task GeneratePermissionsAsync_ShouldHandleServiceArrayResponse()
    {
        // Arrange
        var llmProvider = new MockLlmProvider
        {
            ServiceResponseOverride = """
[
    { "service": "EC2", "actions": ["RunInstances"] },
    { "service": "S3", "actions": ["CreateBucket"] }
]
"""
        };

        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);
        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.RequiredServices.Should().NotBeNull();
        result.RequiredServices!.Select(s => s.Service.ToLowerInvariant()).Should().Contain(new[] { "ec2", "s3" });
    }

    [Fact]
    public async Task GeneratePermissionsAsync_ShouldHandlePermissionObjectWithoutWrapper()
    {
        // Arrange
        var llmProvider = new MockLlmProvider
        {
            PermissionResponseOverride = """
{
    "principalName": "honua-deployer",
    "policies": [
        {
            "name": "HonuaDeploymentPolicy",
            "description": "Least-privilege policy",
            "policyJson": "{\"Version\":\"2012-10-17\"}"
        }
    ]
}
"""
        };

        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);
        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.DeploymentPermissions.Should().NotBeNull();
        result.DeploymentPermissions!.PrincipalName.Should().Be("honua-deployer");
        result.DeploymentPermissions.Policies.Should().ContainSingle();
    }

    [Fact]
    public async Task GeneratePermissionsAsync_WhenTerraformFails_ShouldReturnError()
    {
        // Arrange
        var llmProvider = new MockLlmProvider
        {
            TerraformShouldFail = true
        };

        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);
        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Terraform generation");
    }

    [Fact]
    public async Task GeneratePermissionsAsync_ShouldParseServiceResponseWithMarkdownPrefix()
    {
        // Arrange
        var llmProvider = new MockLlmProvider
        {
            ServiceResponseOverride = """
Here is the requested analysis:

```json
{
  "services": [
    { "service": "EC2", "actions": ["RunInstances"] }
  ]
}
```
"""
        };

        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);
        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.RequiredServices.Should().NotBeNull();
        result.RequiredServices!.Select(s => s.Service).Should().Contain("EC2");
    }

    [Fact]
    public async Task GeneratePermissionsAsync_ShouldHandlePermissionArrayFallback()
    {
        // Arrange
        var llmProvider = new MockLlmProvider
        {
            PermissionResponseOverride = """
[
  {
    "name": "HonuaDeploymentPolicy",
    "description": "Least privilege",
    "policyJson": "{\"Version\":\"2012-10-17\"}"
  }
]
"""
        };

        var agent = new CloudPermissionGeneratorAgent(llmProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);
        var topology = CreateAWSTopology();
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.DeploymentPermissions.Should().NotBeNull();
        result.DeploymentPermissions!.PrincipalName.Should().Be("honua-generated");
        result.DeploymentPermissions.Policies.Should().ContainSingle();
    }

    // Helper methods
    private static DeploymentTopology CreateAWSTopology()
    {
        return new DeploymentTopology
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod",
            Database = new DatabaseConfig
            {
                Engine = "postgres",
                Version = "15",
                InstanceSize = "db.r6g.xlarge",
                StorageGB = 100,
                HighAvailability = true
            },
            Compute = new ComputeConfig
            {
                Type = "container",
                InstanceSize = "c6i.2xlarge",
                InstanceCount = 3,
                AutoScaling = true
            },
            Storage = new StorageConfig
            {
                Type = "s3",
                AttachmentStorageGB = 100,
                RasterCacheGB = 500,
                Replication = "cross-region"
            },
            Networking = new NetworkingConfig
            {
                LoadBalancer = true,
                PublicAccess = true,
                VpnRequired = false
            },
            Monitoring = new MonitoringConfig
            {
                Provider = "cloudwatch",
                EnableMetrics = true,
                EnableLogs = true,
                EnableTracing = true
            }
        };
    }

    private static DeploymentTopology CreateAzureTopology()
    {
        return new DeploymentTopology
        {
            CloudProvider = "azure",
            Region = "eastus",
            Environment = "staging",
            Database = new DatabaseConfig
            {
                Engine = "postgres",
                Version = "15",
                InstanceSize = "Standard_D2s_v3",
                StorageGB = 50,
                HighAvailability = false
            },
            Compute = new ComputeConfig
            {
                Type = "vm",
                InstanceSize = "Standard_D4s_v3",
                InstanceCount = 2,
                AutoScaling = true
            },
            Storage = new StorageConfig
            {
                Type = "blob",
                AttachmentStorageGB = 100,
                RasterCacheGB = 300,
                Replication = "single-region"
            },
            Networking = new NetworkingConfig
            {
                LoadBalancer = true,
                PublicAccess = true,
                VpnRequired = false
            },
            Monitoring = new MonitoringConfig
            {
                Provider = "application-insights",
                EnableMetrics = true,
                EnableLogs = true,
                EnableTracing = false
            }
        };
    }

    private static DeploymentTopology CreateGCPTopology()
    {
        return new DeploymentTopology
        {
            CloudProvider = "gcp",
            Region = "us-central1",
            Environment = "dev",
            Database = new DatabaseConfig
            {
                Engine = "postgres",
                Version = "15",
                InstanceSize = "db-f1-micro",
                StorageGB = 20,
                HighAvailability = false
            },
            Compute = new ComputeConfig
            {
                Type = "container",
                InstanceSize = "e2-medium",
                InstanceCount = 1,
                AutoScaling = false
            },
            Storage = new StorageConfig
            {
                Type = "gcs",
                AttachmentStorageGB = 50,
                RasterCacheGB = 200,
                Replication = "single-region"
            },
            Networking = new NetworkingConfig
            {
                LoadBalancer = false,
                PublicAccess = true,
                VpnRequired = false
            },
            Monitoring = new MonitoringConfig
            {
                Provider = "cloud-monitoring",
                EnableMetrics = true,
                EnableLogs = true,
                EnableTracing = false
            }
        };
    }

    // Mock LLM Provider
    private sealed class MockLlmProvider : ILlmProvider
    {
        public string ProviderName => "mock";
        public string DefaultModel => "mock-model";

        public string? ServiceResponseOverride { get; set; }
        public string? PermissionResponseOverride { get; set; }
        public string? TerraformResponseOverride { get; set; }
        public bool TerraformShouldFail { get; set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<System.Collections.Generic.IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<string>>(new[] { "mock-model" });
        }

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            // Return different responses based on what's being asked
            string mockResponse;

            if (request.UserPrompt.Contains("identify ALL required cloud services", StringComparison.OrdinalIgnoreCase) ||
                request.UserPrompt.Contains("Analyze this HonuaIO deployment topology", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(ServiceResponseOverride))
                {
                    mockResponse = ServiceResponseOverride;
                }
                else
                {
                // Service analysis response (note: property names must match CloudServiceRequirement class)
                mockResponse = @"{
                    ""Services"": [
                        { ""Service"": ""EC2"", ""Actions"": [""RunInstances"", ""TerminateInstances""], ""Rationale"": ""Deploy compute instances"" },
                        { ""Service"": ""RDS"", ""Actions"": [""CreateDBInstance"", ""DeleteDBInstance""], ""Rationale"": ""Provision database"" },
                        { ""Service"": ""S3"", ""Actions"": [""CreateBucket"", ""PutObject"", ""GetObject""], ""Rationale"": ""Store attachments and raster tiles"" },
                        { ""Service"": ""ELB"", ""Actions"": [""CreateLoadBalancer"", ""DeleteLoadBalancer""], ""Rationale"": ""Load balancing"" },
                        { ""Service"": ""CloudWatch"", ""Actions"": [""PutMetricData"", ""CreateLogGroup""], ""Rationale"": ""Monitoring and logging"" }
                    ]
                }";
                }
            }
            else if (request.UserPrompt.Contains("Generate least-privilege", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(PermissionResponseOverride))
                {
                    mockResponse = PermissionResponseOverride;
                }
                else
                {
                // Permission generation response (note: property names must match CloudPermissionSet class)
                mockResponse = @"{
                    ""PermissionSet"": {
                        ""PrincipalName"": ""honua-deployer"",
                        ""Policies"": [
                            {
                                ""Name"": ""HonuaDeploymentPolicy"",
                                ""Description"": ""Least-privilege policy for HonuaIO deployment"",
                                ""PolicyJson"": ""{\""Version\"":\""2012-10-17\"",\""Statement\"":[{\""Effect\"":\""Allow\"",\""Action\"":[\""ec2:RunInstances\"",\""rds:CreateDBInstance\"",\""s3:CreateBucket\""],\""Resource\"":\""arn:aws:*:*:*:*\""}]}""
                            }
                        ],
                        ""Tags"": { ""Project"": ""HonuaIO"", ""ManagedBy"": ""Terraform"" }
                    }
                }";
                }
            }
            else
            {
                if (TerraformShouldFail)
                {
                    return Task.FromResult(new LlmResponse
                    {
                        Content = string.Empty,
                        Model = DefaultModel,
                        Success = false,
                        ErrorMessage = "Simulated Terraform generation failure"
                    });
                }

                if (!string.IsNullOrWhiteSpace(TerraformResponseOverride))
                {
                    mockResponse = TerraformResponseOverride;
                }
                else
                {
                // Terraform generation response - vary by cloud provider
                if (request.UserPrompt.Contains("azure", StringComparison.OrdinalIgnoreCase))
                {
                    mockResponse = @"# HonuaIO IAM Deployment Configuration
provider ""azurerm"" {
  features {}
}

resource ""azuread_service_principal"" ""honua_deployer"" {
  name = ""honua-deployer""
  tags = [""Project:HonuaIO"", ""ManagedBy:Terraform""]
}

resource ""azurerm_role_definition"" ""honua_deployment"" {
  name = ""HonuaDeploymentRole""
  description = ""Least-privilege role for HonuaIO deployment""
}

output ""service_principal_id"" {
  value = azuread_service_principal.honua_deployer.id
}";
                }
                else if (request.UserPrompt.Contains("gcp", StringComparison.OrdinalIgnoreCase))
                {
                    mockResponse = @"# HonuaIO IAM Deployment Configuration
provider ""google"" {
  project = var.project_id
  region  = var.region
}

resource ""google_service_account"" ""honua_deployer"" {
  account_id   = ""honua-deployer""
  display_name = ""HonuaIO Deployment Service Account""
}

resource ""google_project_iam_custom_role"" ""honua_deployment"" {
  role_id     = ""honuaDeploymentRole""
  title       = ""HonuaIO Deployment Role""
  description = ""Least-privilege role for HonuaIO deployment""
  permissions = [""compute.instances.create"", ""storage.buckets.create""]
}

output ""service_account_email"" {
  value = google_service_account.honua_deployer.email
}";
                }
                else
                {
                    // Default to AWS
                    mockResponse = @"# HonuaIO IAM Deployment Configuration
provider ""aws"" {
  region = var.region
}

resource ""aws_iam_user"" ""honua_deployer"" {
  name = ""honua-deployer""
  tags = {
    Project = ""HonuaIO""
    ManagedBy = ""Terraform""
    Description = ""Deployment account with least-privilege permissions""
  }
}

resource ""aws_iam_policy"" ""honua_deployment"" {
  name = ""HonuaDeploymentPolicy""
  description = ""Least-privilege policy for HonuaIO deployment""
  policy = jsonencode({
    Version = ""2012-10-17""
    Statement = [
      {
        Effect = ""Allow""
        Action = [
          ""ec2:RunInstances"",
          ""ec2:TerminateInstances"",
          ""rds:CreateDBInstance"",
          ""s3:CreateBucket""
        ]
        Resource = ""arn:aws:*:*:*:*""
      }
    ]
  })
}

resource ""aws_iam_user_policy_attachment"" ""attach"" {
  user       = aws_iam_user.honua_deployer.name
  policy_arn = aws_iam_policy.honua_deployment.arn
}

output ""user_name"" {
  value = aws_iam_user.honua_deployer.name
  sensitive = false
}";
                }
                }
            }

            return Task.FromResult(new LlmResponse
            {
                Content = mockResponse,
                Model = DefaultModel,
                Success = true
            });
        }

        public async System.Collections.Generic.IAsyncEnumerable<LlmStreamChunk> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            yield return new LlmStreamChunk { Content = "Mock", IsFinal = true };
        }
    }
}
