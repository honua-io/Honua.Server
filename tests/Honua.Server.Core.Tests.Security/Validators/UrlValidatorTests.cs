// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Security;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Validators;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("https://www.google.com")]
    [InlineData("https://api.github.com/repos")]
    [InlineData("http://subdomain.example.com:8080/path")]
    public void IsUrlSafe_WithValidPublicUrls_ReturnsTrue(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("https://localhost:3000")]
    [InlineData("http://localhost:8080/api")]
    public void IsUrlSafe_WithLocalhostUrls_ReturnsFalse(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("https://127.0.0.1")]
    public void IsUrlSafe_WithLoopbackIpv4_ReturnsFalse(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://10.255.255.255")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://172.31.255.255")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://192.168.255.255")]
    public void IsUrlSafe_WithPrivateIpv4Ranges_ReturnsFalse(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://169.254.1.1")] // Link-local
    [InlineData("http://0.0.0.0")] // Current network
    [InlineData("http://100.64.0.1")] // Carrier-grade NAT
    [InlineData("http://192.0.0.1")] // IETF Protocol Assignments
    [InlineData("http://192.0.2.1")] // TEST-NET-1
    [InlineData("http://198.18.0.1")] // Benchmarking
    [InlineData("http://198.51.100.1")] // TEST-NET-2
    [InlineData("http://203.0.113.1")] // TEST-NET-3
    [InlineData("http://224.0.0.1")] // Multicast
    [InlineData("http://240.0.0.1")] // Reserved
    public void IsUrlSafe_WithReservedIpv4Ranges_ReturnsFalse(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://[::1]")] // IPv6 loopback
    [InlineData("http://[fe80::1]")] // IPv6 link-local
    [InlineData("http://[fc00::1]")] // IPv6 unique local
    [InlineData("http://[ff00::1]")] // IPv6 multicast
    public void IsUrlSafe_WithPrivateIpv6Ranges_ReturnsFalse(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://server.local")]
    [InlineData("http://myserver.local")]
    [InlineData("http://api.internal")]
    [InlineData("http://service.internal")]
    public void IsUrlSafe_WithInternalDomains_ReturnsFalse(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    public void IsUrlSafe_WithNonHttpSchemes_ReturnsFalse(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void IsUrlSafe_WithNullOrEmptyUrl_ReturnsFalse(string? url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("htp://malformed")]
    [InlineData("//example.com")]
    public void IsUrlSafe_WithMalformedUrls_ReturnsFalse(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://8.8.8.8")] // Google DNS - public IP
    [InlineData("http://1.1.1.1")] // Cloudflare DNS - public IP
    [InlineData("http://44.123.45.67")] // Random public IP
    public void IsUrlSafe_WithPublicIpAddresses_ReturnsTrue(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://example.com:443")]
    [InlineData("http://example.com:80")]
    [InlineData("https://api.example.com:8443")]
    public void IsUrlSafe_WithCustomPorts_ValidatesCorrectly(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://192.168.1.1:8080")] // Private IP with port
    [InlineData("http://localhost:3000")] // Localhost with port
    public void IsUrlSafe_WithPrivateIpAndPort_ReturnsFalse(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsUrlSafe_WithAwsMetadataEndpoint_ReturnsFalse()
    {
        // Arrange - AWS metadata service
        var url = "http://169.254.169.254/latest/meta-data/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsUrlSafe_WithGoogleMetadataEndpoint_ReturnsFalse()
    {
        // Arrange - GCP metadata service
        var url = "http://169.254.169.254/computeMetadata/v1/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://example.com/path/to/resource")]
    [InlineData("https://example.com/path?query=value")]
    [InlineData("https://example.com/path#fragment")]
    [InlineData("https://user:pass@example.com")]
    public void IsUrlSafe_WithComplexValidUrls_ReturnsTrue(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUrlSafe_IsCaseInsensitiveForScheme()
    {
        // Arrange
        var urlLower = "https://example.com";
        var urlUpper = "HTTPS://example.com";
        var urlMixed = "HtTpS://example.com";

        // Act
        var resultLower = UrlValidator.IsUrlSafe(urlLower);
        var resultUpper = UrlValidator.IsUrlSafe(urlUpper);
        var resultMixed = UrlValidator.IsUrlSafe(urlMixed);

        // Assert
        resultLower.Should().BeTrue();
        resultUpper.Should().BeTrue();
        resultMixed.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://100.64.50.1")] // Carrier-grade NAT range
    [InlineData("http://100.127.255.255")] // Carrier-grade NAT range (end)
    public void IsUrlSafe_WithCarrierGradeNatRange_ReturnsFalse(string url)
    {
        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }
}
