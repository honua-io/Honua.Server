using Microsoft.Extensions.Configuration;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using System.Reflection;

namespace Honua.Cli.Tests.Support;

/// <summary>
/// Provides test configuration from user secrets and environment variables.
/// </summary>
public static class TestConfiguration
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddUserSecrets(Assembly.GetExecutingAssembly())
        .AddEnvironmentVariables()
        .Build();

    /// <summary>
    /// Gets the OpenAI API key from user secrets or environment variables.
    /// </summary>
    public static string? OpenAiApiKey => Configuration["OpenAI:ApiKey"] ?? Configuration["OPENAI_API_KEY"];

    /// <summary>
    /// Gets the Anthropic API key from user secrets or environment variables.
    /// </summary>
    public static string? AnthropicApiKey => Configuration["Anthropic:ApiKey"] ?? Configuration["ANTHROPIC_API_KEY"];

    /// <summary>
    /// Gets whether a real LLM provider is available for testing.
    /// </summary>
    public static bool HasRealLlmProvider => !string.IsNullOrWhiteSpace(OpenAiApiKey) || !string.IsNullOrWhiteSpace(AnthropicApiKey);

    /// <summary>
    /// Exports API keys to environment variables for child processes (like E2E tests).
    /// Call this at the start of tests that spawn the CLI process.
    /// </summary>
    public static void ExportApiKeysToEnvironment()
    {
        if (!string.IsNullOrWhiteSpace(OpenAiApiKey))
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", OpenAiApiKey);
        }

        if (!string.IsNullOrWhiteSpace(AnthropicApiKey))
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", AnthropicApiKey);
        }
    }

    /// <summary>
    /// Creates an LLM provider for testing.
    /// Uses real OpenAI if API key is configured AND explicitly enabled, otherwise returns mock provider.
    /// To enable real OpenAI: set environment variable USE_REAL_LLM=true
    /// </summary>
    public static ILlmProvider CreateLlmProvider()
    {
        var useRealLlm = Environment.GetEnvironmentVariable("USE_REAL_LLM");
        if (HasRealLlmProvider && string.Equals(useRealLlm, "true", StringComparison.OrdinalIgnoreCase))
        {
            var options = new LlmProviderOptions
            {
                OpenAI = new OpenAIOptions
                {
                    ApiKey = OpenAiApiKey!,
                    DefaultModel = "gpt-4o-mini"
                }
            };
            return new OpenAILlmProvider(options);
        }

        return new MockLlmProvider();
    }

    /// <summary>
    /// Simple mock LLM provider for tests when no real API key is available.
    /// </summary>
    private class MockLlmProvider : ILlmProvider
    {
        public string ProviderName => "mock";
        public string DefaultModel => "mock-model";

        public Task<bool> IsAvailableAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<System.Collections.Generic.IReadOnlyList<string>> ListModelsAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<string>>(new[] { "mock-model" });
        }

        public Task<LlmResponse> CompleteAsync(LlmRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            // Provide context-aware mock responses based on prompt content
            string mockResponse;

            // Combine system and user prompts for matching
            var fullPrompt = $"{request.SystemPrompt} {request.UserPrompt}";

            // Debug logging (enable with DEBUG_MOCK_LLM=1 environment variable)
            var debug = Environment.GetEnvironmentVariable("DEBUG_MOCK_LLM") == "1";
            if (debug)
            {
                Console.WriteLine("=== MockLlmProvider Debug ===");
                var sysLen = request.SystemPrompt?.Length ?? 0;
                var userLen = request.UserPrompt?.Length ?? 0;
                Console.WriteLine($"SystemPrompt (first 200 chars): {request.SystemPrompt?.Substring(0, Math.Min(200, sysLen))}...");
                Console.WriteLine($"UserPrompt (first 200 chars): {request.UserPrompt?.Substring(0, Math.Min(200, userLen))}...");
            }

            // Order checks from most specific to least specific
            // IMPORTANT: Service analysis and IAM generation both contain "least-privilege" but service analysis
            // has "cloud infrastructure architect" while IAM has "security expert"

            // 1. Handle consultant planning requests (most specific - unique system prompt)
            if (request.SystemPrompt?.Contains("world-class geospatial infrastructure consultant", System.StringComparison.OrdinalIgnoreCase) == true)
            {
                if (debug) Console.WriteLine(">>> Matched: Consultant Planning");
                // Generate context-specific plan steps based on user request
                var planSteps = new System.Text.StringBuilder();
                planSteps.Append(@"[");

                // Always include a deployment step
                planSteps.Append(@"{
                    ""title"": ""Deploy HonuaIO"",
                    ""skill"": ""deploy-plan"",
                    ""action"": ""Deploy to cloud infrastructure"",
                    ""category"": ""deployment"",
                    ""rationale"": ""Setup infrastructure deployment"",
                    ""successCriteria"": ""Services deployed successfully"",
                    ""risk"": ""low"",
                    ""dependencies"": [],
                    ""inputs"": {}
                }");

                // Add validation step if topology validation mentioned
                if (fullPrompt.Contains("validat", System.StringComparison.OrdinalIgnoreCase) ||
                    fullPrompt.Contains("verify", System.StringComparison.OrdinalIgnoreCase))
                {
                    planSteps.Append(@",{
                        ""title"": ""Validate topology"",
                        ""skill"": ""deploy-validate"",
                        ""action"": ""Validate deployment topology configuration"",
                        ""category"": ""validation"",
                        ""rationale"": ""Ensure configuration is correct before deployment"",
                        ""successCriteria"": ""Topology validation passed"",
                        ""risk"": ""low"",
                        ""dependencies"": [],
                        ""inputs"": {}
                    }");
                }

                // Add IAM/credentials step if permissions/IAM mentioned
                if (fullPrompt.Contains("IAM", System.StringComparison.OrdinalIgnoreCase) ||
                    fullPrompt.Contains("permission", System.StringComparison.OrdinalIgnoreCase) ||
                    fullPrompt.Contains("credential", System.StringComparison.OrdinalIgnoreCase))
                {
                    planSteps.Append(@",{
                        ""title"": ""Generate IAM permissions"",
                        ""skill"": ""deploy-generate-iam"",
                        ""action"": ""Generate least-privilege IAM credentials"",
                        ""category"": ""security"",
                        ""rationale"": ""Create secure credentials for deployment"",
                        ""successCriteria"": ""IAM policies generated"",
                        ""risk"": ""low"",
                        ""dependencies"": [],
                        ""inputs"": {}
                    }");
                }

                planSteps.Append(@"]");

                // Generate observations based on context
                var observations = @"[{
                    ""id"": ""obs-1"",
                    ""severity"": ""medium"",
                    ""summary"": ""Production deployment requires HA"",
                    ""detail"": ""Use multiple availability zones for high availability"",
                    ""recommendation"": ""Enable high availability""
                }]";

                if (fullPrompt.Contains("production", System.StringComparison.OrdinalIgnoreCase))
                {
                    observations = @"[{
                        ""id"": ""obs-1"",
                        ""severity"": ""high"",
                        ""summary"": ""Production deployment requires high availability"",
                        ""detail"": ""Configure multiple availability zones and load balancing"",
                        ""recommendation"": ""Enable HA, auto-scaling, and multi-AZ deployment""
                    }]";
                }
                else if (fullPrompt.Contains("dev", System.StringComparison.OrdinalIgnoreCase) ||
                         fullPrompt.Contains("development", System.StringComparison.OrdinalIgnoreCase))
                {
                    observations = @"[{
                        ""id"": ""obs-1"",
                        ""severity"": ""low"",
                        ""summary"": ""Development deployment can use minimal resources"",
                        ""detail"": ""Single instance deployment is sufficient for dev environment"",
                        ""recommendation"": ""Use cost-optimized instance types like t3.micro""
                    }]";
                }

                mockResponse = $@"{{
                    ""executiveSummary"": ""Deployment plan for HonuaIO"",
                    ""confidence"": ""high"",
                    ""reinforcedObservations"": {observations},
                    ""plan"": {planSteps}
                }}";
            }
            // 2. Handle service analysis requests (check for cloud infrastructure architect)
            // Must come BEFORE IAM policy generation since both contain "least-privilege"
            // Service analysis UNIQUELY contains "cloud infrastructure architect"
            else if (request.SystemPrompt?.Contains("cloud infrastructure architect", System.StringComparison.OrdinalIgnoreCase) == true)
            {
                if (debug) Console.WriteLine(">>> Matched: Service Analysis");
                mockResponse = @"{
                    ""Services"": [
                        { ""Service"": ""EC2"", ""Actions"": [""RunInstances"", ""TerminateInstances""], ""Rationale"": ""Deploy compute instances"" },
                        { ""Service"": ""RDS"", ""Actions"": [""CreateDBInstance""], ""Rationale"": ""Provision database"" },
                        { ""Service"": ""S3"", ""Actions"": [""CreateBucket"", ""PutObject""], ""Rationale"": ""Store data"" }
                    ]
                }";
            }
            // 3. Handle IAM policy generation requests
            // Check for IAM/RBAC security expert (AWS IAM, Azure RBAC, or GCP IAM security expert)
            // This must come BEFORE the general Terraform check since IAM generation also mentions Terraform
            else if (request.SystemPrompt?.Contains("IAM security expert", System.StringComparison.OrdinalIgnoreCase) == true ||
                     request.SystemPrompt?.Contains("RBAC security expert", System.StringComparison.OrdinalIgnoreCase) == true ||
                     request.SystemPrompt?.Contains("Cloud IAM security expert", System.StringComparison.OrdinalIgnoreCase) == true)
            {
                if (debug) Console.WriteLine(">>> Matched: IAM Policy Generation");
                mockResponse = @"{
                    ""PermissionSet"": {
                        ""PrincipalName"": ""honua-deployer"",
                        ""Policies"": [
                            {
                                ""Name"": ""HonuaDeploymentPolicy"",
                                ""Description"": ""Least-privilege policy"",
                                ""PolicyJson"": ""{\""Version\"":\""2012-10-17\""}""
                            }
                        ]
                    }
                }";
            }
            // 4. Handle Terraform code generation (raw HCL, not JSON-wrapped)
            // Only match when explicitly asking for Terraform code AND ends with "Return ONLY Terraform code"
            else if (request.SystemPrompt?.Contains("Terraform expert", System.StringComparison.OrdinalIgnoreCase) == true ||
                     (fullPrompt.Contains("Terraform", System.StringComparison.OrdinalIgnoreCase) &&
                      fullPrompt.Contains("Return ONLY Terraform code", System.StringComparison.OrdinalIgnoreCase)))
            {
                if (debug) Console.WriteLine(">>> Matched: Terraform Generation");
                // Generate cloud-specific Terraform based on cloud provider mentioned
                // IMPORTANT: Include provider comment header for test assertions
                if (fullPrompt.Contains("azure", System.StringComparison.OrdinalIgnoreCase))
                {
                    mockResponse = @"# Provider: azure
# Generated by HonuaIO CLI for Azure deployment

resource ""azuread_service_principal"" ""honua_deployer"" {
  display_name = ""honua-deployer""
}

resource ""azurerm_role_definition"" ""honua_deployer_role"" {
  name  = ""honua-deployer-role""
  scope = ""/subscriptions/00000000-0000-0000-0000-000000000000""
}";
                }
                else if (fullPrompt.Contains("gcp", System.StringComparison.OrdinalIgnoreCase))
                {
                    mockResponse = @"# Provider: gcp
# Generated by HonuaIO CLI for GCP deployment

resource ""google_service_account"" ""honua_deployer"" {
  account_id = ""honua-deployer""
}";
                }
                else
                {
                    // Default to AWS
                    mockResponse = @"# Provider: aws
# Generated by HonuaIO CLI for AWS deployment

resource ""aws_iam_user"" ""honua_deployer"" {
  name = ""honua-deployer""
}";
                }
            }
            // 5. Default fallback - return basic Terraform with AWS provider
            else
            {
                if (debug) Console.WriteLine(">>> Matched: DEFAULT FALLBACK");
                mockResponse = @"# Provider: aws
# Generated by HonuaIO CLI

resource ""aws_iam_user"" ""honua_deployer"" {
  name = ""honua-deployer""
}";
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
            [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return new LlmStreamChunk { Content = "Mock stream response", IsFinal = true };
        }
    }
}
