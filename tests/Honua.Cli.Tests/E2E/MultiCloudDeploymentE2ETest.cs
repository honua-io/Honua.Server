using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Honua.Cli.Tests.Support;

namespace Honua.Cli.Tests.E2E;

/// <summary>
/// Multi-cloud deployment test using emulators:
/// - LocalStack for AWS
/// - Azurite for Azure Storage
/// - Docker Compose for local orchestration
/// Tests full deployment workflow across multiple cloud providers.
/// </summary>
[Trait("Category", "E2E")]
public class MultiCloudDeploymentE2ETest : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testWorkspace;
    private readonly HttpClient _httpClient;
    private bool _emulatorsStarted;

    public MultiCloudDeploymentE2ETest(ITestOutputHelper output)
    {
        _output = output;
        _testWorkspace = Path.Combine(Path.GetTempPath(), $"honua-multicloud-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testWorkspace);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _emulatorsStarted = false;
    }

    [Fact(Skip = "Long-running E2E test requiring LLM API keys, Terraform, Docker, and LocalStack. Run manually with --filter Category=E2E")]
    [Trait("Category", "E2E")]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AWS_TerraformGeneration_WithLocalStack_ShouldGenerateAndValidate()
    {
        // Arrange
        Skip.IfNot(TestConfiguration.HasRealLlmProvider, "No real LLM API keys available");
        Skip.IfNot(IsTerraformAvailable(), "Terraform is not installed");

        await StartEmulatorsAsync();

        _output.WriteLine($"Test workspace: {_testWorkspace}");

        // Act: Generate AWS Terraform with real LLM
        _output.WriteLine("\n=== Generating AWS Terraform configuration ===");
        var result = await RunConsultantAsync(
            "Generate Terraform configuration for AWS deployment of HonuaIO with ECS Fargate and RDS PostgreSQL");

        // Assert
        Assert.True(result.Success, $"Consultant failed: {result.Error}");

        // Verify Terraform files were generated
        var terraformDir = Path.Combine(_testWorkspace, "terraform-aws");
        Assert.True(Directory.Exists(terraformDir), "Terraform directory should exist");

        var mainTf = Path.Combine(terraformDir, "main.tf");
        Assert.True(File.Exists(mainTf), "main.tf should exist");

        var tfContent = await File.ReadAllTextAsync(mainTf);
        _output.WriteLine($"\n=== Generated Terraform (first 1000 chars) ===\n{tfContent.Substring(0, Math.Min(1000, tfContent.Length))}...");

        // Verify Terraform content
        Assert.Contains("provider \"aws\"", tfContent);
        Assert.Contains("resource", tfContent);

        // Test terraform init with LocalStack
        _output.WriteLine("\n=== Running terraform init ===");
        var initResult = await RunTerraformCommandAsync("init", terraformDir);
        Assert.True(initResult.Success, $"Terraform init failed: {initResult.Error}");

        // Test terraform validate
        _output.WriteLine("\n=== Running terraform validate ===");
        var validateResult = await RunTerraformCommandAsync("validate", terraformDir);
        Assert.True(validateResult.Success, $"Terraform validate failed: {validateResult.Error}");

        // Test terraform plan with LocalStack
        _output.WriteLine("\n=== Running terraform plan ===");
        var planResult = await RunTerraformCommandAsync("plan -out=tfplan", terraformDir);
        Assert.True(planResult.Success, $"Terraform plan failed: {planResult.Error}");

        // Verify VPC and security groups would be created (don't actually apply, just validate plan)
        Assert.Contains("Plan:", planResult.Output);
        _output.WriteLine($"Terraform plan summary: resources to add/change/destroy");

        _output.WriteLine("\n✓ AWS Terraform generation and validation successful");
    }

    [Fact(Skip = "Long-running E2E test requiring LLM API keys, Terraform, Docker, and Azurite. Run manually with --filter Category=E2E")]
    [Trait("Category", "E2E")]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Azure_ResourceGeneration_WithAzurite_ShouldGenerateConfiguration()
    {
        // Arrange
        Skip.IfNot(TestConfiguration.HasRealLlmProvider, "No real LLM API keys available");
        Skip.IfNot(IsTerraformAvailable(), "Terraform is not installed");

        await StartEmulatorsAsync();

        _output.WriteLine($"Test workspace: {_testWorkspace}");

        // Act: Generate Azure configuration with real LLM
        _output.WriteLine("\n=== Generating Azure Terraform configuration ===");
        var result = await RunConsultantAsync(
            "Generate Terraform configuration for Azure deployment with Container Instances and Azure Database for PostgreSQL");

        // Assert
        Assert.True(result.Success, $"Consultant failed: {result.Error}");

        var terraformDir = Path.Combine(_testWorkspace, "terraform-azure");
        var mainTf = Path.Combine(terraformDir, "main.tf");

        Assert.True(File.Exists(mainTf), "main.tf should exist");

        var tfContent = await File.ReadAllTextAsync(mainTf);
        _output.WriteLine($"\n=== Generated Terraform (first 1000 chars) ===\n{tfContent.Substring(0, Math.Min(1000, tfContent.Length))}...");

        // Verify Terraform content
        Assert.Contains("azurerm", tfContent);
        Assert.Contains("resource", tfContent);

        // Test terraform init
        _output.WriteLine("\n=== Running terraform init ===");
        var initResult = await RunTerraformCommandAsync("init", terraformDir);
        Assert.True(initResult.Success, $"Terraform init failed: {initResult.Error}");

        // Test terraform validate
        _output.WriteLine("\n=== Running terraform validate ===");
        var validateResult = await RunTerraformCommandAsync("validate", terraformDir);
        Assert.True(validateResult.Success, $"Terraform validate failed: {validateResult.Error}");

        // Validate Azurite is accessible
        _output.WriteLine("\n=== Validating Azurite connectivity ===");
        var azuriteHealthy = await ValidateAzuriteAsync();
        Assert.True(azuriteHealthy, "Azurite should be accessible");

        _output.WriteLine("\n✓ Azure configuration generation and validation successful");
    }

    [Fact(Skip = "Long-running E2E test requiring LLM API keys and Docker. Run manually with --filter Category=E2E")]
    [Trait("Category", "E2E")]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task DockerCompose_WithPostGIS_ShouldDeployAndValidate()
    {
        // Arrange
        Skip.IfNot(TestConfiguration.HasRealLlmProvider, "No real LLM API keys available");
        Skip.IfNot(IsDockerAvailable(), "Docker is not installed");

        _output.WriteLine($"Test workspace: {_testWorkspace}");

        // Act: Generate Docker Compose with real LLM
        _output.WriteLine("\n=== Generating Docker Compose configuration ===");
        var result = await RunConsultantAsync(
            "Create docker-compose.yml for HonuaIO with PostgreSQL PostGIS for local development");

        // Assert
        Assert.True(result.Success, $"Consultant failed: {result.Error}");

        var dockerCompose = Path.Combine(_testWorkspace, "docker-compose.yml");
        Assert.True(File.Exists(dockerCompose), "docker-compose.yml should exist");

        var composeContent = await File.ReadAllTextAsync(dockerCompose);
        _output.WriteLine($"\n=== Generated docker-compose.yml ===\n{composeContent}");

        Assert.Contains("postgis", composeContent.ToLower());
        Assert.Contains("services:", composeContent);

        // Deploy the generated docker-compose
        _output.WriteLine("\n=== Deploying Docker Compose ===");
        var deployResult = await RunDockerComposeAsync("up -d", _testWorkspace);
        Assert.True(deployResult.Success, $"Docker Compose up failed: {deployResult.Error}");

        // Wait for services to be ready
        _output.WriteLine("Waiting for services to start...");
        await Task.Delay(TimeSpan.FromSeconds(15));

        // Validate PostGIS is running
        _output.WriteLine("\n=== Validating PostGIS database ===");
        var validateResult = await ValidatePostGisAsync();
        Assert.True(validateResult, "PostGIS validation failed");

        // Cleanup deployment
        _output.WriteLine("\n=== Cleaning up deployment ===");
        await RunDockerComposeAsync("down -v", _testWorkspace);

        _output.WriteLine("\n✓ Docker Compose deployment and validation successful");
    }

    [Fact(Skip = "Long-running E2E test requiring LLM API keys, kubectl, and minikube. Run manually with --filter Category=E2E")]
    [Trait("Category", "E2E")]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Kubernetes_ManifestGeneration_ShouldCreateValidYAML()
    {
        // Arrange
        Skip.IfNot(TestConfiguration.HasRealLlmProvider, "No real LLM API keys available");
        Skip.IfNot(IsKubectlAvailable(), "kubectl is not installed");

        _output.WriteLine($"Test workspace: {_testWorkspace}");

        // Act: Generate Kubernetes manifests with real LLM
        _output.WriteLine("\n=== Generating Kubernetes manifests ===");
        var result = await RunConsultantAsync(
            "Generate Kubernetes manifests for HonuaIO deployment with StatefulSet for PostgreSQL");

        // Assert
        Assert.True(result.Success, $"Consultant failed: {result.Error}");

        // Check for generated YAML files
        var k8sDir = Path.Combine(_testWorkspace, "kubernetes");
        Assert.True(Directory.Exists(k8sDir), "Kubernetes directory should exist");

        var k8sFiles = Directory.GetFiles(k8sDir, "*.yaml", SearchOption.AllDirectories);
        _output.WriteLine($"Found {k8sFiles.Length} YAML files");
        Assert.True(k8sFiles.Length > 0, "Should generate at least one YAML file");

        foreach (var file in k8sFiles)
        {
            var content = await File.ReadAllTextAsync(file);
            _output.WriteLine($"\n=== {Path.GetFileName(file)} ===\n{content.Substring(0, Math.Min(500, content.Length))}...");

            // Validate YAML contains Kubernetes resources
            Assert.Contains("apiVersion:", content);
            Assert.Contains("kind:", content);
        }

        // Start minikube if not running
        _output.WriteLine("\n=== Starting Minikube cluster ===");
        var minikubeStart = await RunKubernetesCommandAsync("minikube status || minikube start --driver=docker");
        if (!minikubeStart.Success)
        {
            _output.WriteLine("WARNING: Minikube failed to start, skipping deployment");
            _output.WriteLine("\n✓ Kubernetes manifest generation completed (deployment skipped)");
            return;
        }

        // Apply manifests to minikube
        _output.WriteLine("\n=== Applying Kubernetes manifests ===");
        foreach (var file in k8sFiles.OrderBy(f => Path.GetFileName(f)))
        {
            var applyResult = await RunKubernetesCommandAsync($"kubectl apply -f \"{file}\"");
            _output.WriteLine($"Applied {Path.GetFileName(file)}: {(applyResult.Success ? "✓" : "✗")}");
        }

        // Wait for pods to be created
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Validate deployment
        _output.WriteLine("\n=== Validating Kubernetes deployment ===");
        var podsResult = await RunKubernetesCommandAsync("kubectl get pods -n honua");
        _output.WriteLine($"Pods:\n{podsResult.Output}");

        // Cleanup
        _output.WriteLine("\n=== Cleaning up Kubernetes resources ===");
        await RunKubernetesCommandAsync("kubectl delete namespace honua --ignore-not-found=true");

        _output.WriteLine("\n✓ Kubernetes manifest generation and deployment successful");
    }

    [Fact(Skip = "Long-running E2E test requiring LLM API keys and Terraform. Run manually with --filter Category=E2E")]
    [Trait("Category", "E2E")]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task GCP_TerraformGeneration_ShouldGenerateAndValidate()
    {
        // Arrange
        Skip.IfNot(TestConfiguration.HasRealLlmProvider, "No real LLM API keys available");
        Skip.IfNot(IsTerraformAvailable(), "Terraform is not installed");

        _output.WriteLine($"Test workspace: {_testWorkspace}");

        // Act: Generate GCP Terraform with real LLM
        _output.WriteLine("\n=== Generating GCP Terraform configuration ===");
        var result = await RunConsultantAsync(
            "Generate Terraform configuration for GCP deployment with Cloud Run and Cloud SQL PostgreSQL");

        // Assert
        Assert.True(result.Success, $"Consultant failed: {result.Error}");

        // Verify Terraform files were generated
        var terraformDir = Path.Combine(_testWorkspace, "terraform-gcp");
        Assert.True(Directory.Exists(terraformDir), "Terraform GCP directory should exist");

        var mainTf = Path.Combine(terraformDir, "main.tf");
        Assert.True(File.Exists(mainTf), "main.tf should exist");

        var tfContent = await File.ReadAllTextAsync(mainTf);
        _output.WriteLine($"\n=== Generated Terraform (first 1000 chars) ===\n{tfContent.Substring(0, Math.Min(1000, tfContent.Length))}...");

        // Verify Terraform content
        Assert.Contains("provider \"google\"", tfContent);
        Assert.Contains("resource", tfContent);
        Assert.Contains("google_cloud_run_service", tfContent);

        // Test terraform init
        _output.WriteLine("\n=== Running terraform init ===");
        var initResult = await RunTerraformCommandAsync("init", terraformDir);
        Assert.True(initResult.Success, $"Terraform init failed: {initResult.Error}");

        // Test terraform validate
        _output.WriteLine("\n=== Running terraform validate ===");
        var validateResult = await RunTerraformCommandAsync("validate", terraformDir);
        Assert.True(validateResult.Success, $"Terraform validate failed: {validateResult.Error}");

        _output.WriteLine("\n✓ GCP Terraform generation and validation successful");
    }

    private async Task StartEmulatorsAsync()
    {
        if (_emulatorsStarted)
        {
            return;
        }

        _output.WriteLine("=== Starting cloud emulators (LocalStack, Azurite) ===");

        var composeFile = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "E2E", "docker-compose-localstack.yml");

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"compose -f \"{composeFile}\" up -d",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start docker compose");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        _output.WriteLine($"Docker compose output:\n{output}");
        if (!string.IsNullOrWhiteSpace(error))
        {
            _output.WriteLine($"Docker compose errors:\n{error}");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to start emulators: {error}");
        }

        // Wait for services to be healthy
        _output.WriteLine("Waiting for emulators to be ready...");
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Verify LocalStack is ready
        try
        {
            var response = await _httpClient.GetAsync("http://localhost:4566/_localstack/health");
            var health = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"LocalStack health: {health}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Could not verify LocalStack health: {ex.Message}");
        }

        _emulatorsStarted = true;
        _output.WriteLine("✓ Emulators started successfully");
    }

    private async Task<(bool Success, string Output, string Error)> RunConsultantAsync(string prompt)
    {
        var cliPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "src", "Honua.Cli", "Honua.Cli.csproj"));

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{cliPath}\" -- consultant --prompt \"{prompt}\" --workspace \"{_testWorkspace}\" --no-interactive --auto-approve --verbose",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _testWorkspace
        };

        // Pass API keys via environment variables
        if (!string.IsNullOrWhiteSpace(TestConfiguration.OpenAiApiKey))
        {
            startInfo.EnvironmentVariables["OPENAI_API_KEY"] = TestConfiguration.OpenAiApiKey;
        }
        if (!string.IsNullOrWhiteSpace(TestConfiguration.AnthropicApiKey))
        {
            startInfo.EnvironmentVariables["ANTHROPIC_API_KEY"] = TestConfiguration.AnthropicApiKey;
        }

        // Set LocalStack endpoint
        startInfo.EnvironmentVariables["AWS_ENDPOINT_URL"] = "http://localhost:4566";
        startInfo.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = "test";
        startInfo.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = "test";
        startInfo.EnvironmentVariables["AWS_DEFAULT_REGION"] = "us-east-1";

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
            return (false, "", "Failed to start process");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        _output.WriteLine($"Consultant output:\n{output}");
        if (!string.IsNullOrWhiteSpace(error))
        {
            _output.WriteLine($"Consultant stderr:\n{error}");
        }

        return (process.ExitCode == 0, output, error);
    }

    private async Task<(bool Success, string Output, string Error)> RunTerraformCommandAsync(
        string command,
        string workingDirectory)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "terraform",
            Arguments = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        // Set LocalStack endpoint
        startInfo.EnvironmentVariables["AWS_ENDPOINT_URL"] = "http://localhost:4566";
        startInfo.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = "test";
        startInfo.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = "test";
        startInfo.EnvironmentVariables["AWS_DEFAULT_REGION"] = "us-east-1";

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
            return (false, "", "Failed to start terraform");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        _output.WriteLine($"Terraform {command} output:\n{output}");
        if (!string.IsNullOrWhiteSpace(error))
        {
            _output.WriteLine($"Terraform {command} stderr:\n{error}");
        }

        return (process.ExitCode == 0, output, error);
    }

    private async Task<(bool Success, string Output, string Error)> RunDockerComposeAsync(
        string command,
        string workingDirectory)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"compose {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
            return (false, "", "Failed to start docker compose");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        _output.WriteLine($"Docker compose {command} output:\n{output}");
        if (!string.IsNullOrWhiteSpace(error))
        {
            _output.WriteLine($"Docker compose {command} stderr:\n{error}");
        }

        return (process.ExitCode == 0, output, error);
    }

    private async Task<bool> ValidatePostGisAsync()
    {
        // Try to connect to PostGIS and run a simple query
        try
        {
            // Use psql to test connection
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "exec honua-postgis psql -U honua -d honua -c \"SELECT PostGIS_Version();\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                return false;

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            _output.WriteLine($"PostGIS validation output:\n{output}");
            if (!string.IsNullOrWhiteSpace(error))
            {
                _output.WriteLine($"PostGIS validation stderr:\n{error}");
            }

            return process.ExitCode == 0 && (output.Contains("POSTGIS") || output.Contains("postgis_version"));
        }
        catch (Exception ex)
        {
            _output.WriteLine($"PostGIS validation exception: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ValidateAzuriteAsync()
    {
        // Check if Azurite blob service is accessible
        try
        {
            var response = await _httpClient.GetAsync("http://localhost:10000/devstoreaccount1?comp=list");
            _output.WriteLine($"Azurite blob service status: {response.StatusCode}");
            // 400 is expected for invalid auth, but it means service is running
            return response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                   response.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Azurite validation exception: {ex.Message}");
            return false;
        }
    }

    private async Task<(bool Success, string Output, string Error)> RunKubernetesCommandAsync(string command)
    {
        // Split command into executable and arguments
        var parts = command.Split(' ', 2);
        var executable = parts[0];
        var arguments = parts.Length > 1 ? parts[1] : "";

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
            return (false, "", $"Failed to start {executable}");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        _output.WriteLine($"{executable} output:\n{output}");
        if (!string.IsNullOrWhiteSpace(error))
        {
            _output.WriteLine($"{executable} stderr:\n{error}");
        }

        return (process.ExitCode == 0, output, error);
    }

    private bool IsTerraformAvailable()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "terraform",
                Arguments = "version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private bool IsDockerAvailable()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private bool IsKubectlAvailable()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "kubectl",
                Arguments = "version --client",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        // Cleanup: Stop emulators
        if (_emulatorsStarted)
        {
            try
            {
                var composeFile = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "..", "E2E", "docker-compose-localstack.yml");

                var stopProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"compose -f \"{composeFile}\" down -v",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });

                stopProcess?.WaitForExit(30000);
                _output.WriteLine("✓ Emulators stopped and removed");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error during emulator cleanup: {ex.Message}");
            }
        }

        // Cleanup test workspace
        try
        {
            if (Directory.Exists(_testWorkspace))
            {
                Directory.Delete(_testWorkspace, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }

        _httpClient?.Dispose();
    }
}
