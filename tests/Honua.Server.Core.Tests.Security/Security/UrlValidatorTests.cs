// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Security;
using NUnit.Framework;

namespace Honua.Server.Core.Tests.Security.Security;

[TestFixture]
[Category("Unit")]
[Category("Security")]
public class UrlValidatorTests
{
    [Test]
    public void IsUrlSafe_PublicHttpsUrl_ReturnsTrue()
    {
        // Arrange
        var url = "https://example.com/path";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsUrlSafe_PublicHttpUrl_ReturnsTrue()
    {
        // Arrange
        var url = "http://example.com/path";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsUrlSafe_NullUrl_ReturnsFalse()
    {
        // Act
        var result = UrlValidator.IsUrlSafe(null);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_EmptyUrl_ReturnsFalse()
    {
        // Act
        var result = UrlValidator.IsUrlSafe(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_WhitespaceUrl_ReturnsFalse()
    {
        // Act
        var result = UrlValidator.IsUrlSafe("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_InvalidUrl_ReturnsFalse()
    {
        // Arrange
        var url = "not a valid url";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_FtpScheme_ReturnsFalse()
    {
        // Arrange - FTP is not allowed
        var url = "ftp://example.com/file";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_FileScheme_ReturnsFalse()
    {
        // Arrange - file:// URLs could access local files
        var url = "file:///etc/passwd";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_DataScheme_ReturnsFalse()
    {
        // Arrange
        var url = "data:text/html,<h1>Hello</h1>";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_JavascriptScheme_ReturnsFalse()
    {
        // Arrange
        var url = "javascript:alert('xss')";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_LocalhostHttps_ReturnsFalse()
    {
        // Arrange
        var url = "https://localhost/api";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_LoopbackIp_ReturnsFalse()
    {
        // Arrange
        var url = "http://127.0.0.1/admin";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_PrivateIp10_ReturnsFalse()
    {
        // Arrange - 10.0.0.0/8 is private
        var url = "http://10.0.0.1/internal";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_PrivateIp172_ReturnsFalse()
    {
        // Arrange - 172.16.0.0/12 is private
        var url = "http://172.16.0.1/internal";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_PrivateIp192_ReturnsFalse()
    {
        // Arrange - 192.168.0.0/16 is private
        var url = "http://192.168.1.1/router";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_LinkLocalIp_ReturnsFalse()
    {
        // Arrange - 169.254.0.0/16 is link-local
        var url = "http://169.254.1.1/metadata";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_CarrierGradeNat_ReturnsFalse()
    {
        // Arrange - 100.64.0.0/10 is Carrier-grade NAT
        var url = "http://100.64.0.1/internal";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_LocalDomain_ReturnsFalse()
    {
        // Arrange
        var url = "http://printer.local/config";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_InternalDomain_ReturnsFalse()
    {
        // Arrange
        var url = "http://database.internal/admin";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_IPv6Loopback_ReturnsFalse()
    {
        // Arrange
        var url = "http://[::1]/api";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_IPv6LinkLocal_ReturnsFalse()
    {
        // Arrange - fe80::/10 is link-local
        var url = "http://[fe80::1]/api";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_IPv6UniqueLocal_ReturnsFalse()
    {
        // Arrange - fc00::/7 is Unique Local Address
        var url = "http://[fc00::1]/api";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_IPv6Multicast_ReturnsFalse()
    {
        // Arrange - ff00::/8 is multicast
        var url = "http://[ff02::1]/api";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_IPv6Unspecified_ReturnsFalse()
    {
        // Arrange - :: is unspecified
        var url = "http://[::]/api";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_PublicIPv4_ReturnsTrue()
    {
        // Arrange - 8.8.8.8 is Google DNS (public)
        var url = "http://8.8.8.8/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsUrlSafe_PublicIPv6_ReturnsTrue()
    {
        // Arrange - 2001:4860:4860::8888 is Google DNS (public)
        var url = "http://[2001:4860:4860::8888]/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsUrlSafe_DocumentationIp_ReturnsFalse()
    {
        // Arrange - 192.0.2.0/24 is reserved for documentation
        var url = "http://192.0.2.1/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_BenchmarkingIp_ReturnsFalse()
    {
        // Arrange - 198.18.0.0/15 is reserved for benchmarking
        var url = "http://198.18.0.1/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_MulticastIp_ReturnsFalse()
    {
        // Arrange - 224.0.0.0/4 is multicast
        var url = "http://224.0.0.1/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_ReservedIp_ReturnsFalse()
    {
        // Arrange - 240.0.0.0/4 is reserved
        var url = "http://240.0.0.1/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_CurrentNetworkIp_ReturnsFalse()
    {
        // Arrange - 0.0.0.0/8 is current network
        var url = "http://0.0.0.0/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_IetfProtocolAssignments_ReturnsFalse()
    {
        // Arrange - 192.0.0.0/24 is IETF Protocol Assignments
        var url = "http://192.0.0.1/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_TestNet2_ReturnsFalse()
    {
        // Arrange - 198.51.100.0/24 is TEST-NET-2
        var url = "http://198.51.100.1/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_TestNet3_ReturnsFalse()
    {
        // Arrange - 203.0.113.0/24 is TEST-NET-3
        var url = "http://203.0.113.1/";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_PrivateIpEdgeCases_ReturnsFalse()
    {
        // Arrange - Test edge cases of private ranges
        var testCases = new[]
        {
            "http://10.255.255.255/",      // End of 10.0.0.0/8
            "http://172.16.0.0/",          // Start of 172.16.0.0/12
            "http://172.31.255.255/",      // End of 172.16.0.0/12
            "http://192.168.0.0/",         // Start of 192.168.0.0/16
            "http://192.168.255.255/",     // End of 192.168.0.0/16
            "http://127.255.255.255/",     // End of loopback
            "http://169.254.0.0/",         // Start of link-local
            "http://169.254.255.255/"      // End of link-local
        };

        foreach (var url in testCases)
        {
            // Act
            var result = UrlValidator.IsUrlSafe(url);

            // Assert
            result.Should().BeFalse($"because {url} should be blocked");
        }
    }

    [Test]
    public void IsUrlSafe_PublicIpJustOutsidePrivateRanges_ReturnsTrue()
    {
        // Arrange - IPs just outside private ranges should be allowed
        var testCases = new[]
        {
            "http://9.255.255.255/",       // Just before 10.0.0.0/8
            "http://11.0.0.0/",            // Just after 10.0.0.0/8
            "http://172.15.255.255/",      // Just before 172.16.0.0/12
            "http://172.32.0.0/",          // Just after 172.16.0.0/12
            "http://192.167.255.255/",     // Just before 192.168.0.0/16
            "http://192.169.0.0/"          // Just after 192.168.0.0/16
        };

        foreach (var url in testCases)
        {
            // Act
            var result = UrlValidator.IsUrlSafe(url);

            // Assert
            result.Should().BeTrue($"because {url} is a public IP");
        }
    }

    [Test]
    public void IsUrlSafe_RelativeUrl_ReturnsFalse()
    {
        // Arrange
        var url = "/api/endpoint";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_UrlWithPort_ReturnsCorrectly()
    {
        // Arrange
        var publicUrl = "https://example.com:8080/api";
        var privateUrl = "http://192.168.1.1:8080/admin";

        // Act
        var publicResult = UrlValidator.IsUrlSafe(publicUrl);
        var privateResult = UrlValidator.IsUrlSafe(privateUrl);

        // Assert
        publicResult.Should().BeTrue();
        privateResult.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_UrlWithQueryString_ReturnsCorrectly()
    {
        // Arrange
        var publicUrl = "https://example.com/api?param=value";
        var privateUrl = "http://localhost:3000/api?secret=token";

        // Act
        var publicResult = UrlValidator.IsUrlSafe(publicUrl);
        var privateResult = UrlValidator.IsUrlSafe(privateUrl);

        // Assert
        publicResult.Should().BeTrue();
        privateResult.Should().BeFalse();
    }

    [Test]
    public void IsUrlSafe_UrlWithFragment_ReturnsCorrectly()
    {
        // Arrange
        var url = "https://example.com/page#section";

        // Act
        var result = UrlValidator.IsUrlSafe(url);

        // Assert
        result.Should().BeTrue();
    }
}
