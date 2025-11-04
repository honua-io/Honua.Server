using System;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Execution;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Execution;

/// <summary>
/// Security tests to verify command injection vulnerabilities are prevented
/// </summary>
[Trait("Category", "Integration")]
public class ExecutionPluginSecurityTests
{
    private readonly Mock<IPluginExecutionContext> _mockContext;

    public ExecutionPluginSecurityTests()
    {
        _mockContext = new Mock<IPluginExecutionContext>();
        _mockContext.Setup(x => x.DryRun).Returns(true); // Use dry-run (validation still occurs before dry-run)
        _mockContext.Setup(x => x.RequireApproval).Returns(false);
        _mockContext.Setup(x => x.WorkspacePath).Returns("/tmp/test");
    }

    private static void AssertRejected(string result)
    {
        Assert.Contains("\"success\":false", result);
    }

    #region DatabaseExecutionPlugin Tests

    [Theory]
    [InlineData("; DROP TABLE users--")]
    [InlineData("test; rm -rf /")]
    [InlineData("test && malicious")]
    public async Task DatabaseExecutionPlugin_ExecuteSQL_WithMaliciousSQL_ShouldReject(string maliciousSql)
    {
        // Arrange
        var plugin = new DatabaseExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.ExecuteSQL("testdb", maliciousSql, "test", "postgres");

        // Assert
        AssertRejected(result);
    }

    [Theory]
    [InlineData("; whoami")]
    [InlineData("test && malicious")]
    public async Task DatabaseExecutionPlugin_ExecuteSQL_WithMaliciousContainerName_ShouldReject(string maliciousContainer)
    {
        // Arrange
        var plugin = new DatabaseExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.ExecuteSQL(maliciousContainer, "SELECT 1", "test", "postgres");

        // Assert
        AssertRejected(result);
    }

    [Theory]
    [InlineData("postgres://localhost; whoami")]
    [InlineData("postgres://localhost|malicious")]
    public async Task DatabaseExecutionPlugin_ExecuteSQL_WithMaliciousConnectionString_ShouldReject(string maliciousConnection)
    {
        // Arrange
        var plugin = new DatabaseExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.ExecuteSQL(maliciousConnection, "SELECT 1", "test", "postgres");

        // Assert
        AssertRejected(result);
    }

    [Theory]
    [InlineData("testdb; whoami")]
    [InlineData("testdb && malicious")]
    public async Task DatabaseExecutionPlugin_CreatePostGISDatabase_WithMaliciousContainerName_ShouldReject(string maliciousContainer)
    {
        // Arrange
        var plugin = new DatabaseExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.CreatePostGISDatabase(maliciousContainer, "testdb", "postgres", "postgres");

        // Assert
        AssertRejected(result);
    }

    [Theory]
    [InlineData("testdb; DROP TABLE users--")]
    [InlineData("testdb-with-dash")] // Dashes not allowed
    public async Task DatabaseExecutionPlugin_CreatePostGISDatabase_WithMaliciousDatabaseName_ShouldReject(string maliciousDbName)
    {
        // Arrange
        var plugin = new DatabaseExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.CreatePostGISDatabase("testcontainer", maliciousDbName, "postgres", "postgres");

        // Assert
        AssertRejected(result);
    }

    [Fact]
    public async Task DatabaseExecutionPlugin_ExecuteSQL_WithValidInputs_ShouldNotReject()
    {
        // Arrange
        var plugin = new DatabaseExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.ExecuteSQL("valid_container", "SELECT * FROM users WHERE id = 1", "Test query", "postgres");

        // Assert - Should succeed
        Assert.DoesNotContain("\"success\":false", result);
    }

    #endregion

    #region TerraformExecutionPlugin Tests

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("terraform; whoami")]
    [InlineData("terraform && malicious")]
    public async Task TerraformExecutionPlugin_Init_WithMaliciousPath_ShouldReject(string maliciousPath)
    {
        // Arrange
        var plugin = new TerraformExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.TerraformInit(maliciousPath);

        // Assert
        AssertRejected(result);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("terraform; whoami")]
    public async Task TerraformExecutionPlugin_Plan_WithMaliciousPath_ShouldReject(string maliciousPath)
    {
        // Arrange
        var plugin = new TerraformExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.TerraformPlan(maliciousPath);

        // Assert
        AssertRejected(result);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("terraform; whoami")]
    public async Task TerraformExecutionPlugin_Apply_WithMaliciousPath_ShouldReject(string maliciousPath)
    {
        // Arrange
        var plugin = new TerraformExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.TerraformApply(maliciousPath, autoApprove: true);

        // Assert
        AssertRejected(result);
    }

    [Fact]
    public async Task TerraformExecutionPlugin_Init_WithValidPath_ShouldNotReject()
    {
        // Arrange
        var plugin = new TerraformExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.TerraformInit("terraform/valid-path");

        // Assert
        Assert.DoesNotContain("\"success\":false", result);
    }

    #endregion

    #region DockerExecutionPlugin Tests

    [Theory]
    [InlineData("container; whoami")]
    [InlineData("container && malicious")]
    public async Task DockerExecutionPlugin_RunContainer_WithMaliciousContainerName_ShouldReject(string maliciousContainer)
    {
        // Arrange
        var plugin = new DockerExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.RunDockerContainer("nginx", maliciousContainer);

        // Assert
        AssertRejected(result);
    }

    [Theory]
    [InlineData("container; whoami")]
    [InlineData("container && malicious")]
    public async Task DockerExecutionPlugin_StopContainer_WithMaliciousContainerName_ShouldReject(string maliciousContainer)
    {
        // Arrange
        var plugin = new DockerExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.StopDockerContainer(maliciousContainer);

        // Assert
        AssertRejected(result);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("docker-compose.yml; whoami")]
    public async Task DockerExecutionPlugin_ComposeUp_WithMaliciousPath_ShouldReject(string maliciousPath)
    {
        // Arrange
        var plugin = new DockerExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.DockerComposeUp(maliciousPath);

        // Assert
        AssertRejected(result);
    }

    [Fact]
    public async Task DockerExecutionPlugin_RunContainer_WithValidInputs_ShouldNotReject()
    {
        // Arrange
        var plugin = new DockerExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.RunDockerContainer("nginx:latest", "valid_container_name");

        // Assert
        Assert.DoesNotContain("\"success\":false", result);
    }

    [Fact]
    public async Task DockerExecutionPlugin_ComposeUp_WithValidPath_ShouldNotReject()
    {
        // Arrange
        var plugin = new DockerExecutionPlugin(_mockContext.Object);

        // Act
        var result = await plugin.DockerComposeUp("docker-compose.yml");

        // Assert
        Assert.DoesNotContain("\"success\":false", result);
    }

    #endregion
}
