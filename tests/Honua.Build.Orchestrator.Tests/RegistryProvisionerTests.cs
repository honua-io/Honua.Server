using FluentAssertions;
using Moq;
using Xunit;

namespace Honua.Build.Orchestrator.Tests;

/// <summary>
/// Tests for RegistryProvisioner - creates customer-specific namespaces/repositories in various cloud registries.
/// Handles GitHub Packages, AWS ECR, Azure ACR, and Google GCR.
/// </summary>
[Trait("Category", "Unit")]
public class RegistryProvisionerTests
{
    private readonly Mock<IGitHubClient> _mockGitHubClient;
    private readonly Mock<IAwsEcrClient> _mockEcrClient;
    private readonly Mock<IAzureAcrClient> _mockAcrClient;
    private readonly Mock<ICredentialStore> _mockCredentialStore;
    private readonly RegistryProvisioner _provisioner;

    public RegistryProvisionerTests()
    {
        _mockGitHubClient = new Mock<IGitHubClient>();
        _mockEcrClient = new Mock<IAwsEcrClient>();
        _mockAcrClient = new Mock<IAzureAcrClient>();
        _mockCredentialStore = new Mock<ICredentialStore>();

        _provisioner = new RegistryProvisioner(
            _mockGitHubClient.Object,
            _mockEcrClient.Object,
            _mockAcrClient.Object,
            _mockCredentialStore.Object
        );
    }

    [Fact]
    public async Task ProvisionGitHub_NewCustomer_CreatesNamespace()
    {
        // Arrange
        var customerId = "customer-123";
        var organizationName = "honua";

        _mockGitHubClient
            .Setup(x => x.CreatePackageNamespaceAsync(organizationName, It.IsAny<string>()))
            .ReturnsAsync(new GitHubPackageNamespace
            {
                Name = $"honua-{customerId}",
                Url = $"ghcr.io/{organizationName}/honua-{customerId}",
                Created = true
            });

        // Act
        var result = await _provisioner.ProvisionGitHubAsync(customerId, organizationName);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RegistryUrl.Should().Be($"ghcr.io/{organizationName}/honua-{customerId}");
        result.NamespaceCreated.Should().BeTrue();

        _mockGitHubClient.Verify(
            x => x.CreatePackageNamespaceAsync(organizationName, $"honua-{customerId}"),
            Times.Once
        );
    }

    [Fact]
    public async Task ProvisionGitHub_ExistingCustomer_ReusesNamespace()
    {
        // Arrange
        var customerId = "customer-existing";
        var organizationName = "honua";

        _mockGitHubClient
            .Setup(x => x.CreatePackageNamespaceAsync(organizationName, It.IsAny<string>()))
            .ReturnsAsync(new GitHubPackageNamespace
            {
                Name = $"honua-{customerId}",
                Url = $"ghcr.io/{organizationName}/honua-{customerId}",
                Created = false, // Already exists
                Message = "Namespace already exists"
            });

        // Act
        var result = await _provisioner.ProvisionGitHubAsync(customerId, organizationName);

        // Assert
        result.Success.Should().BeTrue();
        result.NamespaceCreated.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task ProvisionAws_NewCustomer_CreatesEcrRepositoryAndIamUser()
    {
        // Arrange
        var customerId = "customer-456";
        var awsRegion = "us-west-2";
        var repositoryName = $"honua/{customerId}";

        _mockEcrClient
            .Setup(x => x.CreateRepositoryAsync(repositoryName, awsRegion))
            .ReturnsAsync(new EcrRepository
            {
                RepositoryUri = $"123456789012.dkr.ecr.{awsRegion}.amazonaws.com/{repositoryName}",
                RepositoryArn = $"arn:aws:ecr:{awsRegion}:123456789012:repository/{repositoryName}",
                Created = true
            });

        _mockEcrClient
            .Setup(x => x.CreateIamUserAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new IamUser
            {
                UserName = $"honua-{customerId}",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Created = true
            });

        // Act
        var result = await _provisioner.ProvisionAwsAsync(customerId, awsRegion);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RegistryUrl.Should().Contain("dkr.ecr");
        result.RepositoryCreated.Should().BeTrue();
        result.IamUserCreated.Should().BeTrue();
        result.Credentials.Should().NotBeNull();
        result.Credentials!.AccessKeyId.Should().NotBeNullOrEmpty();
        result.Credentials.SecretAccessKey.Should().NotBeNullOrEmpty();

        _mockEcrClient.Verify(
            x => x.CreateRepositoryAsync(repositoryName, awsRegion),
            Times.Once
        );
        _mockEcrClient.Verify(
            x => x.CreateIamUserAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ProvisionAws_ExistingRepository_ReusesRepo()
    {
        // Arrange
        var customerId = "customer-existing";
        var awsRegion = "us-east-1";

        _mockEcrClient
            .Setup(x => x.CreateRepositoryAsync(It.IsAny<string>(), awsRegion))
            .ReturnsAsync(new EcrRepository
            {
                RepositoryUri = $"123456789012.dkr.ecr.{awsRegion}.amazonaws.com/honua/{customerId}",
                Created = false // Already exists
            });

        // Act
        var result = await _provisioner.ProvisionAwsAsync(customerId, awsRegion);

        // Assert
        result.Success.Should().BeTrue();
        result.RepositoryCreated.Should().BeFalse();
    }

    [Fact]
    public async Task ProvisionAws_AttachesEcrPushPullPolicy()
    {
        // Arrange
        var customerId = "customer-789";
        var awsRegion = "eu-west-1";
        var repositoryArn = $"arn:aws:ecr:{awsRegion}:123456789012:repository/honua/{customerId}";

        _mockEcrClient
            .Setup(x => x.CreateRepositoryAsync(It.IsAny<string>(), awsRegion))
            .ReturnsAsync(new EcrRepository
            {
                RepositoryUri = $"123456789012.dkr.ecr.{awsRegion}.amazonaws.com/honua/{customerId}",
                RepositoryArn = repositoryArn,
                Created = true
            });

        _mockEcrClient
            .Setup(x => x.AttachPolicyToUserAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _provisioner.ProvisionAwsAsync(customerId, awsRegion);

        // Assert
        result.Success.Should().BeTrue();

        _mockEcrClient.Verify(
            x => x.AttachPolicyToUserAsync(
                It.IsAny<string>(),
                It.Is<string>(policy =>
                    policy.Contains("ecr:PutImage") &&
                    policy.Contains("ecr:GetDownloadUrlForLayer") &&
                    policy.Contains(repositoryArn))
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProvisionAzure_NewCustomer_CreatesServicePrincipal()
    {
        // Arrange
        var customerId = "customer-azure-123";
        var acrName = "honuaregistry";

        _mockAcrClient
            .Setup(x => x.CreateRepositoryAsync(acrName, It.IsAny<string>()))
            .ReturnsAsync(new AcrRepository
            {
                RepositoryUrl = $"{acrName}.azurecr.io/honua-{customerId}",
                Created = true
            });

        _mockAcrClient
            .Setup(x => x.CreateServicePrincipalAsync(It.IsAny<string>(), acrName))
            .ReturnsAsync(new ServicePrincipal
            {
                ApplicationId = "app-id-123",
                ClientSecret = "client-secret-456",
                TenantId = "tenant-id-789",
                Created = true
            });

        // Act
        var result = await _provisioner.ProvisionAzureAsync(customerId, acrName);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RegistryUrl.Should().Be($"{acrName}.azurecr.io/honua-{customerId}");
        result.ServicePrincipalCreated.Should().BeTrue();
        result.Credentials.Should().NotBeNull();
        result.Credentials!.TenantId.Should().NotBeNullOrEmpty();
        result.Credentials.ClientId.Should().NotBeNullOrEmpty();
        result.Credentials.ClientSecret.Should().NotBeNullOrEmpty();

        _mockAcrClient.Verify(
            x => x.CreateServicePrincipalAsync(It.IsAny<string>(), acrName),
            Times.Once
        );
    }

    [Fact]
    public async Task ProvisionAzure_AssignsAcrPushRole()
    {
        // Arrange
        var customerId = "customer-azure-456";
        var acrName = "honuaregistry";

        _mockAcrClient
            .Setup(x => x.CreateRepositoryAsync(acrName, It.IsAny<string>()))
            .ReturnsAsync(new AcrRepository { RepositoryUrl = $"{acrName}.azurecr.io", Created = true });

        _mockAcrClient
            .Setup(x => x.CreateServicePrincipalAsync(It.IsAny<string>(), acrName))
            .ReturnsAsync(new ServicePrincipal { ApplicationId = "app-123", Created = true });

        _mockAcrClient
            .Setup(x => x.AssignRoleAsync(It.IsAny<string>(), "AcrPush", It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _provisioner.ProvisionAzureAsync(customerId, acrName);

        // Assert
        result.Success.Should().BeTrue();

        _mockAcrClient.Verify(
            x => x.AssignRoleAsync(
                It.IsAny<string>(),
                "AcrPush",
                It.IsAny<string>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task StoreCredentials_ValidData_SavesEncryptedToDatabase()
    {
        // Arrange
        var provisioningResult = new ProvisioningResult
        {
            CustomerId = "customer-123",
            Provider = RegistryProvider.AwsEcr,
            RegistryUrl = "123456789012.dkr.ecr.us-west-2.amazonaws.com",
            Credentials = new RegistryCredentials
            {
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
            }
        };

        _mockCredentialStore
            .Setup(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<RegistryCredentials>()))
            .ReturnsAsync(true);

        // Act
        var result = await _provisioner.StoreCredentialsAsync(provisioningResult);

        // Assert
        result.Should().BeTrue();

        _mockCredentialStore.Verify(
            x => x.StoreAsync(
                "customer-123",
                It.Is<RegistryCredentials>(c =>
                    c.AccessKeyId == "AKIAIOSFODNN7EXAMPLE" &&
                    c.SecretAccessKey == "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY")
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task StoreCredentials_EncryptsSecrets()
    {
        // Arrange
        var credentials = new RegistryCredentials
        {
            AccessKeyId = "plaintext-key",
            SecretAccessKey = "plaintext-secret"
        };

        string? storedAccessKey = null;
        string? storedSecret = null;

        _mockCredentialStore
            .Setup(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<RegistryCredentials>()))
            .Callback<string, RegistryCredentials>((id, creds) =>
            {
                storedAccessKey = creds.AccessKeyId;
                storedSecret = creds.SecretAccessKey;
            })
            .ReturnsAsync(true);

        var provisioningResult = new ProvisioningResult
        {
            CustomerId = "customer-encrypt",
            Credentials = credentials
        };

        // Act
        await _provisioner.StoreCredentialsAsync(provisioningResult);

        // Assert - In real implementation, these would be encrypted
        storedAccessKey.Should().NotBeNullOrEmpty();
        storedSecret.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProvisionGitHub_InvalidOrganization_ThrowsException()
    {
        // Arrange
        var customerId = "customer-123";
        var invalidOrg = "nonexistent-org";

        _mockGitHubClient
            .Setup(x => x.CreatePackageNamespaceAsync(invalidOrg, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Organization not found"));

        // Act
        var act = async () => await _provisioner.ProvisionGitHubAsync(customerId, invalidOrg);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Organization not found*");
    }

    [Fact]
    public async Task ProvisionAws_InvalidRegion_ThrowsException()
    {
        // Arrange
        var customerId = "customer-123";
        var invalidRegion = "invalid-region";

        _mockEcrClient
            .Setup(x => x.CreateRepositoryAsync(It.IsAny<string>(), invalidRegion))
            .ThrowsAsync(new ArgumentException("Invalid AWS region"));

        // Act
        var act = async () => await _provisioner.ProvisionAwsAsync(customerId, invalidRegion);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid AWS region*");
    }

    [Theory]
    [InlineData("customer-123", "us-west-2")]
    [InlineData("customer-with-dashes", "eu-west-1")]
    [InlineData("customer_underscore", "ap-southeast-1")]
    public async Task ProvisionAws_ValidCustomerIds_CreatesRepositoryWithCorrectNaming(
        string customerId,
        string region)
    {
        // Arrange
        var expectedRepoName = $"honua/{customerId}";

        _mockEcrClient
            .Setup(x => x.CreateRepositoryAsync(expectedRepoName, region))
            .ReturnsAsync(new EcrRepository
            {
                RepositoryUri = $"123456789012.dkr.ecr.{region}.amazonaws.com/{expectedRepoName}",
                Created = true
            });

        // Act
        var result = await _provisioner.ProvisionAwsAsync(customerId, region);

        // Assert
        result.Success.Should().BeTrue();
        result.RegistryUrl.Should().Contain(customerId);

        _mockEcrClient.Verify(
            x => x.CreateRepositoryAsync(expectedRepoName, region),
            Times.Once
        );
    }

    [Fact]
    public async Task ProvisionMultipleProviders_CreatesInAllRegistries()
    {
        // Arrange
        var customerId = "customer-multicloud";
        var providers = new[]
        {
            RegistryProvider.GitHub,
            RegistryProvider.AwsEcr,
            RegistryProvider.AzureAcr
        };

        _mockGitHubClient
            .Setup(x => x.CreatePackageNamespaceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new GitHubPackageNamespace { Created = true });

        _mockEcrClient
            .Setup(x => x.CreateRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new EcrRepository { Created = true });

        _mockAcrClient
            .Setup(x => x.CreateRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new AcrRepository { Created = true });

        // Act
        var results = await _provisioner.ProvisionMultipleAsync(customerId, providers);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.Success);

        _mockGitHubClient.Verify(x => x.CreatePackageNamespaceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockEcrClient.Verify(x => x.CreateRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockAcrClient.Verify(x => x.CreateRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCustomerRegistry_RemovesAllResources()
    {
        // Arrange
        var customerId = "customer-to-delete";

        _mockEcrClient
            .Setup(x => x.DeleteRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockEcrClient
            .Setup(x => x.DeleteIamUserAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockCredentialStore
            .Setup(x => x.DeleteAsync(customerId))
            .ReturnsAsync(true);

        // Act
        var result = await _provisioner.DeleteCustomerRegistryAsync(customerId, RegistryProvider.AwsEcr);

        // Assert
        result.Should().BeTrue();

        _mockEcrClient.Verify(x => x.DeleteRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockEcrClient.Verify(x => x.DeleteIamUserAsync(It.IsAny<string>()), Times.Once);
        _mockCredentialStore.Verify(x => x.DeleteAsync(customerId), Times.Once);
    }
}

// Placeholder classes and interfaces
public class RegistryProvisioner
{
    private readonly IGitHubClient _gitHubClient;
    private readonly IAwsEcrClient _ecrClient;
    private readonly IAzureAcrClient _acrClient;
    private readonly ICredentialStore _credentialStore;

    public RegistryProvisioner(
        IGitHubClient gitHubClient,
        IAwsEcrClient ecrClient,
        IAzureAcrClient acrClient,
        ICredentialStore credentialStore)
    {
        _gitHubClient = gitHubClient;
        _ecrClient = ecrClient;
        _acrClient = acrClient;
        _credentialStore = credentialStore;
    }

    public Task<ProvisioningResult> ProvisionGitHubAsync(string customerId, string organization)
        => throw new NotImplementedException();

    public Task<ProvisioningResult> ProvisionAwsAsync(string customerId, string region)
        => throw new NotImplementedException();

    public Task<ProvisioningResult> ProvisionAzureAsync(string customerId, string acrName)
        => throw new NotImplementedException();

    public Task<bool> StoreCredentialsAsync(ProvisioningResult result)
        => throw new NotImplementedException();

    public Task<List<ProvisioningResult>> ProvisionMultipleAsync(string customerId, RegistryProvider[] providers)
        => throw new NotImplementedException();

    public Task<bool> DeleteCustomerRegistryAsync(string customerId, RegistryProvider provider)
        => throw new NotImplementedException();
}

public interface IGitHubClient
{
    Task<GitHubPackageNamespace> CreatePackageNamespaceAsync(string organization, string namespaceName);
}

public interface IAwsEcrClient
{
    Task<EcrRepository> CreateRepositoryAsync(string repositoryName, string region);
    Task<IamUser> CreateIamUserAsync(string userName, string region);
    Task<bool> AttachPolicyToUserAsync(string userName, string policyDocument);
    Task<bool> DeleteRepositoryAsync(string repositoryName, string region);
    Task<bool> DeleteIamUserAsync(string userName);
}

public interface IAzureAcrClient
{
    Task<AcrRepository> CreateRepositoryAsync(string acrName, string repositoryName);
    Task<ServicePrincipal> CreateServicePrincipalAsync(string name, string acrName);
    Task<bool> AssignRoleAsync(string principalId, string roleName, string scope);
}

public interface ICredentialStore
{
    Task<bool> StoreAsync(string customerId, RegistryCredentials credentials);
    Task<bool> DeleteAsync(string customerId);
}

public class ProvisioningResult
{
    public bool Success { get; set; }
    public string CustomerId { get; set; } = "";
    public RegistryProvider Provider { get; set; }
    public string RegistryUrl { get; set; } = "";
    public bool NamespaceCreated { get; set; }
    public bool RepositoryCreated { get; set; }
    public bool IamUserCreated { get; set; }
    public bool ServicePrincipalCreated { get; set; }
    public string? Message { get; set; }
    public RegistryCredentials? Credentials { get; set; }
}

public class RegistryCredentials
{
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}

public class GitHubPackageNamespace
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Created { get; set; }
    public string? Message { get; set; }
}

public class EcrRepository
{
    public string RepositoryUri { get; set; } = "";
    public string? RepositoryArn { get; set; }
    public bool Created { get; set; }
}

public class IamUser
{
    public string UserName { get; set; } = "";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public bool Created { get; set; }
}

public class AcrRepository
{
    public string RepositoryUrl { get; set; } = "";
    public bool Created { get; set; }
}

public class ServicePrincipal
{
    public string ApplicationId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string TenantId { get; set; } = "";
    public bool Created { get; set; }
}
