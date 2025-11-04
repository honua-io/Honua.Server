using FluentAssertions;
using Honua.Server.AlertReceiver.Configuration;

namespace Honua.Server.AlertReceiver.Tests.Configuration;

/// <summary>
/// Tests for WebhookSecurityOptions configuration validation.
/// </summary>
[Trait("Category", "Unit")]
public class WebhookSecurityOptionsTests
{
    [Fact]
    public void IsValid_WithValidConfiguration_ReturnsTrue()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SharedSecret = "valid-secret-key-minimum-64-chars-for-hmac-sha256-security-minimum",
            SignatureHeaderName = "X-Hub-Signature-256",
            MaxPayloadSize = 1_048_576
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_WithSignatureDisabled_AllowsMissingSecret()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = false,
            SharedSecret = null,
            SignatureHeaderName = "X-Hub-Signature-256",
            MaxPayloadSize = 1_048_576
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_WithSignatureRequiredButNoSecret_ReturnsFalse()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SharedSecret = null,
            AdditionalSecrets = new List<string>(),
            SignatureHeaderName = "X-Hub-Signature-256",
            MaxPayloadSize = 1_048_576
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("SharedSecret is required"));
    }

    [Fact]
    public void IsValid_WithSecretTooShort_ReturnsFalse()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SharedSecret = "short", // Less than 64 characters
            SignatureHeaderName = "X-Hub-Signature-256",
            MaxPayloadSize = 1_048_576
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("at least 64 characters"));
    }

    [Fact]
    public void IsValid_WithEmptySignatureHeaderName_ReturnsFalse()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SharedSecret = "valid-secret-key-minimum-16-chars",
            SignatureHeaderName = string.Empty,
            MaxPayloadSize = 1_048_576
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("SignatureHeaderName"));
    }

    [Fact]
    public void IsValid_WithZeroMaxPayloadSize_ReturnsFalse()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SharedSecret = "valid-secret-key-minimum-16-chars",
            SignatureHeaderName = "X-Hub-Signature-256",
            MaxPayloadSize = 0
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxPayloadSize must be greater than 0"));
    }

    [Fact]
    public void IsValid_WithExcessiveMaxPayloadSize_ReturnsFalse()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SharedSecret = "valid-secret-key-minimum-16-chars",
            SignatureHeaderName = "X-Hub-Signature-256",
            MaxPayloadSize = 20_000_000 // 20 MB, exceeds 10 MB limit
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("should not exceed 10 MB"));
    }

    [Fact]
    public void IsValid_WithNegativeMaxWebhookAge_ReturnsFalse()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SharedSecret = "valid-secret-key-minimum-16-chars",
            SignatureHeaderName = "X-Hub-Signature-256",
            MaxPayloadSize = 1_048_576,
            MaxWebhookAge = -1
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxWebhookAge cannot be negative"));
    }

    [Fact]
    public void IsValid_WithAdditionalSecretsButTooShort_ReturnsFalse()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SharedSecret = "valid-secret-key-minimum-64-chars-for-hmac-sha256-security-minimum",
            AdditionalSecrets = new List<string> { "short" },
            SignatureHeaderName = "X-Hub-Signature-256",
            MaxPayloadSize = 1_048_576
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("AdditionalSecrets") && e.Contains("shorter than 64"));
    }

    [Fact]
    public void GetAllSecrets_WithOnlyPrimarySecret_ReturnsSingleSecret()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            SharedSecret = "primary-secret-key",
            AdditionalSecrets = new List<string>()
        };

        // Act
        var secrets = options.GetAllSecrets().ToList();

        // Assert
        secrets.Should().HaveCount(1);
        secrets[0].Should().Be("primary-secret-key");
    }

    [Fact]
    public void GetAllSecrets_WithAdditionalSecrets_ReturnsAllSecrets()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            SharedSecret = "primary-secret-key",
            AdditionalSecrets = new List<string> { "additional-secret-1", "additional-secret-2" }
        };

        // Act
        var secrets = options.GetAllSecrets().ToList();

        // Assert
        secrets.Should().HaveCount(3);
        secrets.Should().Contain("primary-secret-key");
        secrets.Should().Contain("additional-secret-1");
        secrets.Should().Contain("additional-secret-2");
    }

    [Fact]
    public void GetAllSecrets_WithNullPrimarySecret_ReturnsOnlyAdditionalSecrets()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            SharedSecret = null,
            AdditionalSecrets = new List<string> { "additional-secret-1" }
        };

        // Act
        var secrets = options.GetAllSecrets().ToList();

        // Assert
        secrets.Should().HaveCount(1);
        secrets[0].Should().Be("additional-secret-1");
    }

    [Fact]
    public void GetAllSecrets_SkipsEmptyAdditionalSecrets()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            SharedSecret = "primary-secret-key",
            AdditionalSecrets = new List<string> { "valid-secret", "", null!, "  " }
        };

        // Act
        var secrets = options.GetAllSecrets().ToList();

        // Assert
        secrets.Should().HaveCount(2);
        secrets.Should().Contain("primary-secret-key");
        secrets.Should().Contain("valid-secret");
    }

    [Fact]
    public void DefaultValues_AreSecure()
    {
        // Arrange & Act
        var options = new WebhookSecurityOptions();

        // Assert
        options.RequireSignature.Should().BeTrue("signature validation should be required by default");
        options.AllowInsecureHttp.Should().BeFalse("HTTPS should be required by default");
        options.MaxPayloadSize.Should().Be(1_048_576, "default should be 1 MB");
        options.MaxWebhookAge.Should().Be(300, "default should be 5 minutes");
        options.SignatureHeaderName.Should().Be("X-Hub-Signature-256");
        options.TimestampHeaderName.Should().Be("X-Webhook-Timestamp");
    }

    [Fact]
    public void IsValid_WithValidAdditionalSecretsOnly_ReturnsTrue()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SharedSecret = null,
            AdditionalSecrets = new List<string> { "valid-additional-secret-key-minimum-64-chars-for-hmac-sha256-sec" },
            SignatureHeaderName = "X-Hub-Signature-256",
            MaxPayloadSize = 1_048_576
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_AccumulatesMultipleErrors()
    {
        // Arrange
        var options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SharedSecret = null,
            SignatureHeaderName = "",
            MaxPayloadSize = 0,
            MaxWebhookAge = -1
        };

        // Act
        var isValid = options.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().HaveCountGreaterThan(1);
    }
}
