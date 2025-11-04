using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Guards;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Guards;

/// <summary>
/// Enhanced tests for LlmOutputGuard covering new dangerous operation patterns
/// and edge cases identified in deep dive review.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LlmOutputGuardEnhancedTests
{
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<LlmOutputGuard>> _mockLogger;
    private readonly LlmOutputGuard _guard;

    public LlmOutputGuardEnhancedTests()
    {
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<LlmOutputGuard>>();
        _guard = new LlmOutputGuard(_mockLlmProvider.Object, _mockLogger.Object);
    }

    [Theory]
    [InlineData("aws ec2 terminate-instances --instance-ids i-1234567890abcdef0")]
    [InlineData("aws s3 rb s3://my-bucket --force")]
    [InlineData("aws rds delete-db-instance --db-instance-identifier mydb")]
    [InlineData("aws dynamodb delete-table --table-name MyTable")]
    [InlineData("aws cloudformation delete-stack --stack-name my-stack")]
    public async Task ValidateOutputAsync_WithAwsDestructiveCommands_DetectsDanger(string dangerousCommand)
    {
        // Arrange
        var output = $"To clean up resources, run: {dangerousCommand}";
        SetupMockLlmUnsafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("AWS destructive commands should be flagged as unsafe");
        result.ContainsDangerousOperations.Should().BeTrue();
        result.DetectedIssues.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("az vm delete --name myvm --resource-group mygroup")]
    [InlineData("az group delete --name mygroup")]
    [InlineData("az storage account delete --name mystorageaccount")]
    [InlineData("az sql server delete --name myserver --resource-group mygroup")]
    [InlineData("az aks delete --name myaks --resource-group mygroup")]
    public async Task ValidateOutputAsync_WithAzureDestructiveCommands_DetectsDanger(string dangerousCommand)
    {
        // Arrange
        var output = $"Execute: {dangerousCommand}";
        SetupMockLlmUnsafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("Azure destructive commands should be flagged");
        result.ContainsDangerousOperations.Should().BeTrue();
    }

    [Theory]
    [InlineData("gcloud compute instances delete my-instance")]
    [InlineData("gcloud projects delete my-project")]
    [InlineData("gcloud sql instances delete my-db-instance")]
    [InlineData("gcloud container clusters delete my-cluster")]
    [InlineData("gcloud storage buckets delete gs://my-bucket")]
    public async Task ValidateOutputAsync_WithGcpDestructiveCommands_DetectsDanger(string dangerousCommand)
    {
        // Arrange
        var output = $"Run this command: {dangerousCommand}";
        SetupMockLlmUnsafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("GCP destructive commands should be flagged");
        result.ContainsDangerousOperations.Should().BeTrue();
    }

    [Theory]
    [InlineData("kubectl delete namespace production")]
    [InlineData("kubectl delete pvc --all")]
    [InlineData("helm delete my-release --purge")]
    [InlineData("kubectl drain node-1")]
    public async Task ValidateOutputAsync_WithK8sDestructiveCommands_DetectsDanger(string dangerousCommand)
    {
        // Arrange
        var output = $"Kubernetes cleanup: {dangerousCommand}";
        SetupMockLlmUnsafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("Kubernetes destructive commands should be flagged");
    }

    [Theory]
    [InlineData("cat ~/.ssh/id_rsa")]
    [InlineData("cat ~/.aws/credentials")]
    [InlineData("printenv | grep TOKEN")]
    [InlineData("printenv | grep SECRET")]
    [InlineData("echo password=mysecret123")]
    public async Task ValidateOutputAsync_WithCredentialExposure_DetectsDanger(string dangerousCommand)
    {
        // Arrange
        var output = $"Debug with: {dangerousCommand}";
        SetupMockLlmUnsafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("Credential exposure should be flagged");
        result.DetectedIssues.Should().Contain(issue => issue.Contains("Dangerous operation"));
    }

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("dd if=/dev/zero of=/dev/sda")]
    [InlineData("mkfs.ext4 /dev/sda1")]
    [InlineData("shred -vzn 3 important-file.txt")]
    public async Task ValidateOutputAsync_WithFileSystemDestruction_DetectsDanger(string dangerousCommand)
    {
        // Arrange
        var output = $"Clean disk: {dangerousCommand}";
        SetupMockLlmUnsafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("File system destruction should be flagged");
        result.ContainsDangerousOperations.Should().BeTrue();
    }

    [Theory]
    [InlineData("nmap -sS target.com")]
    [InlineData("masscan 0.0.0.0/0 -p80")]
    [InlineData("sqlmap -u http://target.com")]
    [InlineData("hydra -l admin -P passwords.txt target.com")]
    public async Task ValidateOutputAsync_WithSecurityTools_DetectsDanger(string dangerousCommand)
    {
        // Arrange
        var output = $"Security scan: {dangerousCommand}";
        SetupMockLlmUnsafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("Security tool usage should be flagged for review");
    }

    [Fact]
    public async Task ValidateOutputAsync_WithSafeCommandInCodeBlock_AllowsOutput()
    {
        // Arrange - dangerous command but clearly in documentation/code block
        var output = @"Here's an example of what NOT to do:

```bash
# Example: Don't run this!
rm -rf /
```

Instead, use targeted cleanup commands.";

        SetupMockLlmSafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        // Note: Current implementation doesn't parse code blocks yet (P1 feature)
        // This test documents expected future behavior
        // For now, it will still flag as dangerous
        result.ContainsDangerousOperations.Should().BeTrue("Code block parsing not yet implemented");
    }

    [Fact]
    public async Task ValidateOutputAsync_WithEmptyOutput_ReturnsSafe()
    {
        // Act
        var result = await _guard.ValidateOutputAsync(
            string.Empty,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeTrue();
        result.DetectedIssues.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateOutputAsync_WithLlmFailureAndDangerousOps_FailsClosed()
    {
        // Arrange
        var output = "aws ec2 terminate-instances --instance-ids i-123";

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = false,
                ErrorMessage = "LLM service unavailable",
                Content = string.Empty,
                Model = "test-model"
            });

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("Should fail closed when LLM unavailable and dangerous ops detected");
        result.ContainsDangerousOperations.Should().BeTrue();
    }

    [Theory]
    [InlineData("git reset --hard HEAD~10")]
    [InlineData("git clean -fdx")]
    [InlineData("git push origin main --force")]
    [InlineData("git filter-branch --force")]
    public async Task ValidateOutputAsync_WithGitDestructiveCommands_DetectsDanger(string dangerousCommand)
    {
        // Arrange
        var output = $"Git cleanup: {dangerousCommand}";
        SetupMockLlmUnsafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("Destructive git commands should be flagged");
    }

    [Theory]
    [InlineData("docker system prune -a")]
    [InlineData("docker rm -f $(docker ps -aq)")]
    [InlineData("docker volume rm $(docker volume ls -q)")]
    public async Task ValidateOutputAsync_WithDockerDestructiveCommands_DetectsDanger(string dangerousCommand)
    {
        // Arrange
        var output = $"Docker cleanup: {dangerousCommand}";
        SetupMockLlmUnsafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("Docker destructive commands should be flagged");
    }

    [Theory]
    [InlineData("shutdown -h now")]
    [InlineData("systemctl stop postgresql")]
    [InlineData("systemctl disable nginx")]
    [InlineData("kill -9 1")]  // Kill init
    public async Task ValidateOutputAsync_WithSystemCommands_DetectsDanger(string dangerousCommand)
    {
        // Arrange
        var output = $"System command: {dangerousCommand}";
        SetupMockLlmUnsafeResponse();

        // Act
        var result = await _guard.ValidateOutputAsync(
            output,
            "TestAgent",
            "User request",
            CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse("System management commands should be flagged");
    }

    private void SetupMockLlmUnsafeResponse()
    {
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = @"{
                    ""isSafe"": false,
                    ""confidence"": 0.9,
                    ""hallucinationRisk"": 0.1,
                    ""issues"": [""Contains potentially dangerous command""],
                    ""explanation"": ""Output contains destructive operations that could harm infrastructure""
                }",
                Model = "test-model"
            });
    }

    private void SetupMockLlmSafeResponse()
    {
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = @"{
                    ""isSafe"": true,
                    ""confidence"": 0.95,
                    ""hallucinationRisk"": 0.05,
                    ""issues"": [],
                    ""explanation"": ""Output contains safe educational content""
                }",
                Model = "test-model"
            });
    }
}
