using Honua.Server.Core.Import.Validation;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Import.Validation;

public sealed class CustomFieldValidatorsTests
{
    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user.name+tag@example.co.uk", true)]
    [InlineData("invalid.email", false)]
    [InlineData("@example.com", false)]
    [InlineData("user@", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidEmail_ValidatesCorrectly(string? email, bool expected)
    {
        // Act
        var result = CustomFieldValidators.IsValidEmail(email);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com", true)]
    [InlineData("https://www.example.com/path?query=value", true)]
    [InlineData("https://example.com:8080/path", true)]
    [InlineData("not-a-url", false)]
    [InlineData("ftp://example.com", false)] // Only http/https
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidUrl_ValidatesCorrectly(string? url, bool expected)
    {
        // Act
        var result = CustomFieldValidators.IsValidUrl(url);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("+14155552671", true)]
    [InlineData("+442071838750", true)]
    [InlineData("555-123-4567", true)]
    [InlineData("(555) 123-4567", true)]
    [InlineData("123", false)] // Too short
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPhone_ValidatesCorrectly(string? phone, bool expected)
    {
        // Act
        var result = CustomFieldValidators.IsValidPhone(phone);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("+14155552671", true)]
    [InlineData("+442071838750", true)]
    [InlineData("555-123-4567", false)] // Not E.164 format
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPhoneE164_ValidatesCorrectly(string? phone, bool expected)
    {
        // Act
        var result = CustomFieldValidators.IsValidPhoneE164(phone);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("12345", true)]
    [InlineData("12345-6789", true)]
    [InlineData("1234", false)] // Too short
    [InlineData("123456", false)] // Too long
    [InlineData("12345-678", false)] // Wrong format
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidUsPostalCode_ValidatesCorrectly(string? postalCode, bool expected)
    {
        // Act
        var result = CustomFieldValidators.IsValidUsPostalCode(postalCode);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("256.1.1.1", false)] // Out of range
    [InlineData("192.168.1", false)] // Incomplete
    [InlineData("not-an-ip", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidIPv4_ValidatesCorrectly(string? ip, bool expected)
    {
        // Act
        var result = CustomFieldValidators.IsValidIPv4(ip);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", true)]
    [InlineData("2001:db8:85a3::8a2e:370:7334", true)]
    [InlineData("::1", true)]
    [InlineData("::", true)]
    [InlineData("gggg::1", false)] // Invalid hex
    [InlineData("192.168.1.1", false)] // IPv4, not IPv6
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidIPv6_ValidatesCorrectly(string? ip, bool expected)
    {
        // Act
        var result = CustomFieldValidators.IsValidIPv6(ip);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(45.0, true)]
    [InlineData(90.0, true)]
    [InlineData(-90.0, true)]
    [InlineData(90.1, false)]
    [InlineData(-90.1, false)]
    [InlineData(180.0, false)]
    public void IsValidLatitude_ValidatesCorrectly(double latitude, bool expected)
    {
        // Act
        var result = CustomFieldValidators.IsValidLatitude(latitude);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(90.0, true)]
    [InlineData(180.0, true)]
    [InlineData(-180.0, true)]
    [InlineData(180.1, false)]
    [InlineData(-180.1, false)]
    [InlineData(360.0, false)]
    public void IsValidLongitude_ValidatesCorrectly(double longitude, bool expected)
    {
        // Act
        var result = CustomFieldValidators.IsValidLongitude(longitude);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("test123", "^[a-z0-9]+$", true)]
    [InlineData("TEST123", "^[a-z0-9]+$", false)] // Case sensitive
    [InlineData("abc-def", "^[a-z]+-[a-z]+$", true)]
    [InlineData("abc_def", "^[a-z]+-[a-z]+$", false)] // Underscore not allowed
    public void MatchesPattern_ValidatesCorrectly(string value, string pattern, bool expected)
    {
        // Act
        var result = CustomFieldValidators.MatchesPattern(value, pattern);

        // Assert
        Assert.Equal(expected, result);
    }
}
