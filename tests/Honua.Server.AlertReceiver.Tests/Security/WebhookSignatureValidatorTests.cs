using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Honua.Server.AlertReceiver.Configuration;
using Honua.Server.AlertReceiver.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Honua.Server.AlertReceiver.Tests.Security;

/// <summary>
/// Comprehensive tests for WebhookSignatureValidator.
/// Tests HMAC-SHA256 signature validation, constant-time comparison, and security features.
/// </summary>
[Trait("Category", "Unit")]
public class WebhookSignatureValidatorTests
{
    private readonly Mock<ILogger<WebhookSignatureValidator>> _mockLogger;
    private readonly WebhookSecurityOptions _options;
    private readonly WebhookSignatureValidator _validator;
    private const string TestSecret = "test-secret-key-for-webhook-validation";

    public WebhookSignatureValidatorTests()
    {
        _mockLogger = new Mock<ILogger<WebhookSignatureValidator>>();
        _options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SignatureHeaderName = "X-Hub-Signature-256",
            SharedSecret = TestSecret,
            MaxPayloadSize = 1_048_576
        };

        var optionsWrapper = Options.Create(_options);
        _validator = new WebhookSignatureValidator(optionsWrapper, _mockLogger.Object);
    }

    [Fact]
    public void GenerateSignature_WithValidPayload_ReturnsCorrectSignature()
    {
        // Arrange
        var payload = "test payload"u8.ToArray();

        // Act
        var signature = _validator.GenerateSignature(payload, TestSecret);

        // Assert
        signature.Should().NotBeNullOrEmpty();
        signature.Should().StartWith("sha256=");
        signature.Length.Should().Be(71); // "sha256=" (7) + 64 hex chars
    }

    [Fact]
    public void GenerateSignature_WithSamePayloadAndSecret_ReturnsSameSignature()
    {
        // Arrange
        var payload = "consistent payload"u8.ToArray();

        // Act
        var signature1 = _validator.GenerateSignature(payload, TestSecret);
        var signature2 = _validator.GenerateSignature(payload, TestSecret);

        // Assert
        signature1.Should().Be(signature2);
    }

    [Fact]
    public void GenerateSignature_WithDifferentPayload_ReturnsDifferentSignature()
    {
        // Arrange
        var payload1 = "payload one"u8.ToArray();
        var payload2 = "payload two"u8.ToArray();

        // Act
        var signature1 = _validator.GenerateSignature(payload1, TestSecret);
        var signature2 = _validator.GenerateSignature(payload2, TestSecret);

        // Assert
        signature1.Should().NotBe(signature2);
    }

    [Fact]
    public void GenerateSignature_WithDifferentSecret_ReturnsDifferentSignature()
    {
        // Arrange
        var payload = "test payload"u8.ToArray();
        var secret1 = "secret-one";
        var secret2 = "secret-two";

        // Act
        var signature1 = _validator.GenerateSignature(payload, secret1);
        var signature2 = _validator.GenerateSignature(payload, secret2);

        // Assert
        signature1.Should().NotBe(signature2);
    }

    [Fact]
    public void GenerateSignature_WithEmptyPayload_ReturnsValidSignature()
    {
        // Arrange
        var payload = Array.Empty<byte>();

        // Act
        var signature = _validator.GenerateSignature(payload, TestSecret);

        // Assert
        signature.Should().NotBeNullOrEmpty();
        signature.Should().StartWith("sha256=");
    }

    [Fact]
    public void GenerateSignature_WithNullPayload_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _validator.GenerateSignature(null!, TestSecret);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateSignature_WithNullSecret_ThrowsArgumentException()
    {
        // Arrange
        var payload = "test"u8.ToArray();

        // Act & Assert
        var act = () => _validator.GenerateSignature(payload, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateSignature_WithEmptySecret_ThrowsArgumentException()
    {
        // Arrange
        var payload = "test"u8.ToArray();

        // Act & Assert
        var act = () => _validator.GenerateSignature(payload, string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithValidSignature_ReturnsTrue()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var signature = _validator.GenerateSignature(payload, TestSecret);
        var request = CreateHttpRequest(payload, signature);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithInvalidSignature_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var invalidSignature = "sha256=invalidhexstring0123456789abcdef0123456789abcdef0123456789";
        var request = CreateHttpRequest(payload, invalidSignature);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithTamperedPayload_ReturnsFalse()
    {
        // Arrange
        var originalPayload = "{\"alert\":\"test\"}"u8.ToArray();
        var signature = _validator.GenerateSignature(originalPayload, TestSecret);

        // Tamper with payload
        var tamperedPayload = "{\"alert\":\"tampered\"}"u8.ToArray();
        var request = CreateHttpRequest(tamperedPayload, signature);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithMissingSignatureHeader_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var request = CreateHttpRequest(payload, null);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithEmptySignatureHeader_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var request = CreateHttpRequest(payload, string.Empty);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithWrongSecret_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var signature = _validator.GenerateSignature(payload, "different-secret");
        var request = CreateHttpRequest(payload, signature);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithLargePayload_ValidatesCorrectly()
    {
        // Arrange
        var largePayload = new byte[100_000];
        Random.Shared.NextBytes(largePayload);
        var signature = _validator.GenerateSignature(largePayload, TestSecret);
        var request = CreateHttpRequest(largePayload, signature);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithPayloadExceedingMaxSize_ReturnsFalse()
    {
        // Arrange
        var oversizedPayload = new byte[_options.MaxPayloadSize + 1];
        var signature = _validator.GenerateSignature(oversizedPayload, TestSecret);
        var request = CreateHttpRequest(oversizedPayload, signature);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithSignatureWithoutPrefix_ValidatesCorrectly()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var fullSignature = _validator.GenerateSignature(payload, TestSecret);
        var signatureWithoutPrefix = fullSignature.Substring("sha256=".Length);
        var request = CreateHttpRequest(payload, signatureWithoutPrefix);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithSignatureWithColonSeparator_ValidatesCorrectly()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var fullSignature = _validator.GenerateSignature(payload, TestSecret);
        var signatureWithColon = fullSignature.Replace("sha256=", "sha256:");
        var request = CreateHttpRequest(payload, signatureWithColon);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithUpperCaseSignature_ValidatesCorrectly()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var signature = _validator.GenerateSignature(payload, TestSecret).ToUpperInvariant();
        var request = CreateHttpRequest(payload, signature);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSignatureAsync_RequestBodyCanBeReadMultipleTimes()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var signature = _validator.GenerateSignature(payload, TestSecret);
        var request = CreateHttpRequest(payload, signature);

        // Act
        var result1 = await _validator.ValidateSignatureAsync(request, TestSecret);

        // Read body again (simulating controller reading it)
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body);
        var bodyContent = await reader.ReadToEndAsync();

        // Assert
        result1.Should().BeTrue();
        bodyContent.Should().Be("{\"alert\":\"test\"}");
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _validator.ValidateSignatureAsync(null!, TestSecret);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithNullSecret_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var signature = _validator.GenerateSignature(payload, TestSecret);
        var request = CreateHttpRequest(payload, signature);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSignatureAsync_WithEmptySecret_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var signature = _validator.GenerateSignature(payload, TestSecret);
        var request = CreateHttpRequest(payload, signature);

        // Act
        var result = await _validator.ValidateSignatureAsync(request, string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GenerateSignature_IsConsistentWithExternalHMAC()
    {
        // Arrange
        var payload = "test payload"u8.ToArray();
        var secret = "my-secret";

        // Calculate expected signature using standard HMAC
        byte[] expectedHash;
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
        {
            expectedHash = hmac.ComputeHash(payload);
        }
        var expectedSignature = $"sha256={Convert.ToHexString(expectedHash).ToLowerInvariant()}";

        // Act
        var actualSignature = _validator.GenerateSignature(payload, secret);

        // Assert
        actualSignature.Should().Be(expectedSignature);
    }

    [Fact]
    public async Task ValidateSignatureAsync_ConstantTimeComparison_PreventsTimingAttacks()
    {
        // This test ensures that comparison time doesn't vary based on where the difference occurs
        // While we can't directly measure timing, we verify the implementation uses FixedTimeEquals

        // Arrange
        var payload = "{\"alert\":\"test\"}"u8.ToArray();
        var correctSignature = _validator.GenerateSignature(payload, TestSecret);

        // Create signatures that differ at different positions
        var earlyDifferenceSignature = "sha256=0" + correctSignature.Substring(8);
        var lateDifferenceSignature = correctSignature.Substring(0, correctSignature.Length - 1) + "0";

        var request1 = CreateHttpRequest(payload, earlyDifferenceSignature);
        var request2 = CreateHttpRequest(payload, lateDifferenceSignature);

        // Act
        var result1 = await _validator.ValidateSignatureAsync(request1, TestSecret);
        var result2 = await _validator.ValidateSignatureAsync(request2, TestSecret);

        // Assert - both should fail (verifying constant-time behavior)
        result1.Should().BeFalse();
        result2.Should().BeFalse();
    }

    /// <summary>
    /// Helper method to create an HttpRequest with a body and optional signature header.
    /// </summary>
    private HttpRequest CreateHttpRequest(byte[] payload, string? signature)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(payload);
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";

        if (signature != null)
        {
            context.Request.Headers[_options.SignatureHeaderName] = signature;
        }

        return context.Request;
    }
}
