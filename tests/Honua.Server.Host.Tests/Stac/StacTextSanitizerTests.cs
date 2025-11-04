using FluentAssertions;
using Honua.Server.Host.Stac;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

[Collection("HostTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "STAC")]
[Trait("Feature", "Security")]
[Trait("Speed", "Fast")]
public sealed class StacTextSanitizerTests
{
    #region Sanitize Tests

    [Fact]
    public void Sanitize_WithNull_ReturnsNull()
    {
        // Act
        var result = StacTextSanitizer.Sanitize(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Sanitize_WithEmptyString_ReturnsEmpty()
    {
        // Act
        var result = StacTextSanitizer.Sanitize(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_WithEmptyStringAndAllowEmptyFalse_ThrowsException()
    {
        // Act
        var action = () => StacTextSanitizer.Sanitize(string.Empty, allowEmpty: false);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Sanitize_WithPlainText_ReturnsUnchanged()
    {
        // Arrange
        const string text = "This is plain text with no special characters";

        // Act
        var result = StacTextSanitizer.Sanitize(text);

        // Assert
        result.Should().Be(text);
    }

    [Fact]
    public void Sanitize_WithHtmlTags_EncodesHtml()
    {
        // Arrange
        const string text = "<script>alert('xss')</script>";

        // Act
        var result = StacTextSanitizer.Sanitize(text);

        // Assert
        result.Should().Be("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;");
        result.Should().NotContain("<script>");
    }

    [Fact]
    public void Sanitize_WithHtmlEntities_EncodesCorrectly()
    {
        // Arrange
        const string text = "A & B < C > D";

        // Act
        var result = StacTextSanitizer.Sanitize(text);

        // Assert
        result.Should().Be("A &amp; B &lt; C &gt; D");
    }

    [Fact]
    public void Sanitize_WithImgTag_EncodesHtml()
    {
        // Arrange
        const string text = "<img src=x onerror=alert(1)>";

        // Act
        var result = StacTextSanitizer.Sanitize(text);

        // Assert
        result.Should().NotContain("<img");
        result.Should().Contain("&lt;img");
    }

    [Fact]
    public void Sanitize_WithSvgXss_EncodesHtml()
    {
        // Arrange
        const string text = "<svg onload=alert(1)>";

        // Act
        var result = StacTextSanitizer.Sanitize(text);

        // Assert
        result.Should().NotContain("<svg");
        result.Should().Contain("&lt;svg");
    }

    [Fact]
    public void Sanitize_WithIframeTag_EncodesHtml()
    {
        // Arrange
        const string text = "<iframe src=javascript:alert(1)></iframe>";

        // Act
        var result = StacTextSanitizer.Sanitize(text);

        // Assert
        result.Should().NotContain("<iframe");
        result.Should().Contain("&lt;iframe");
    }

    [Fact]
    public void Sanitize_WithOnEventHandler_ThrowsException()
    {
        // Arrange
        const string text = "onclick=alert(1)";

        // Act
        var action = () => StacTextSanitizer.Sanitize(text);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous patterns*");
    }

    [Fact]
    public void Sanitize_WithMultipleOnEventHandlers_ThrowsException()
    {
        // Arrange
        var testCases = new[]
        {
            "onload=malicious()",
            "onerror=attack()",
            "onmouseover=steal()",
            "onfocus=exploit()"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var action = () => StacTextSanitizer.Sanitize(testCase);

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*dangerous patterns*");
        }
    }

    [Fact]
    public void Sanitize_WithQuotesAndApostrophes_EncodesCorrectly()
    {
        // Arrange
        const string text = "It's a \"quoted\" string";

        // Act
        var result = StacTextSanitizer.Sanitize(text);

        // Assert
        result.Should().Be("It&#39;s a &quot;quoted&quot; string");
    }

    #endregion

    #region SanitizeUrl Tests

    [Fact]
    public void SanitizeUrl_WithNull_ReturnsEmpty()
    {
        // Act
        var result = StacTextSanitizer.SanitizeUrl(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeUrl_WithEmptyString_ReturnsEmpty()
    {
        // Act
        var result = StacTextSanitizer.SanitizeUrl(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeUrl_WithValidHttpUrl_ReturnsUrl()
    {
        // Arrange
        const string url = "http://example.com/path";

        // Act
        var result = StacTextSanitizer.SanitizeUrl(url);

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public void SanitizeUrl_WithValidHttpsUrl_ReturnsUrl()
    {
        // Arrange
        const string url = "https://example.com/path";

        // Act
        var result = StacTextSanitizer.SanitizeUrl(url);

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public void SanitizeUrl_WithJavaScriptProtocol_ThrowsException()
    {
        // Arrange
        const string url = "javascript:alert(1)";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous protocol*");
    }

    [Fact]
    public void SanitizeUrl_WithJavaScriptProtocolMixedCase_ThrowsException()
    {
        // Arrange
        const string url = "JaVaScRiPt:void(0)";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous protocol*");
    }

    [Fact]
    public void SanitizeUrl_WithDataHtmlProtocol_ThrowsException()
    {
        // Arrange
        const string url = "data:text/html,<script>alert(1)</script>";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous protocol*");
    }

    [Fact]
    public void SanitizeUrl_WithVbScriptProtocol_ThrowsException()
    {
        // Arrange
        const string url = "vbscript:msgbox(1)";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous protocol*");
    }

    [Fact]
    public void SanitizeUrl_WithFtpUrl_ReturnsUrl()
    {
        // Arrange
        const string url = "ftp://example.com/file.txt";

        // Act
        var result = StacTextSanitizer.SanitizeUrl(url);

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public void SanitizeUrl_WithS3Url_ReturnsUrl()
    {
        // Arrange
        const string url = "s3://bucket/key/path";

        // Act
        var result = StacTextSanitizer.SanitizeUrl(url);

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public void SanitizeUrl_WithGsUrl_ReturnsUrl()
    {
        // Arrange
        const string url = "gs://bucket/object";

        // Act
        var result = StacTextSanitizer.SanitizeUrl(url);

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public void SanitizeUrl_WithRelativeUrl_ReturnsUrl()
    {
        // Arrange
        const string url = "/path/to/resource";

        // Act
        var result = StacTextSanitizer.SanitizeUrl(url);

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public void SanitizeUrl_WithInvalidUrl_ThrowsException()
    {
        // Arrange
        const string url = "not a valid url\\\\\\";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid URL format*");
    }

    [Fact]
    public void SanitizeUrl_WithFileProtocol_ThrowsException()
    {
        // Arrange
        const string url = "file:///etc/passwd";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*scheme not allowed*");
    }

    #endregion

    #region Path Traversal Security Tests

    [Fact]
    public void SanitizeUrl_WithUnixPathTraversal_ThrowsException()
    {
        // Arrange
        const string url = "https://example.com/data/../../etc/passwd";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void SanitizeUrl_WithWindowsPathTraversal_ThrowsException()
    {
        // Arrange
        const string url = "https://example.com/data/..\\..\\windows\\system32";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void SanitizeUrl_WithUrlEncodedPathTraversal_ThrowsException()
    {
        // Arrange - %2e%2e/ is URL-encoded ../
        const string url = "https://example.com/data/%2e%2e/sensitive";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void SanitizeUrl_WithUrlEncodedWindowsPathTraversal_ThrowsException()
    {
        // Arrange - %2e%2e\ is URL-encoded ..\
        const string url = "https://example.com/data/%2e%2e\\config";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void SanitizeUrl_WithDoubleEncodedPathTraversal_ThrowsException()
    {
        // Arrange - %2e%2e%2f is URL-encoded ../
        const string url = "https://example.com/data/%2e%2e%2fsensitive";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void SanitizeUrl_WithDoubleEncodedWindowsPathTraversal_ThrowsException()
    {
        // Arrange - %2e%2e%5c is URL-encoded ..\
        const string url = "https://example.com/data/%2e%2e%5csensitive";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void SanitizeUrl_WithFileProtocolAndPathTraversal_ThrowsException()
    {
        // Arrange
        const string url = "file:///data/../../../etc/passwd";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        // Should throw for either dangerous protocol OR path traversal
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SanitizeUrl_WithS3PathTraversal_ThrowsException()
    {
        // Arrange
        const string url = "s3://bucket/../../../sensitive-bucket/key";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void SanitizeUrl_WithRelativePathTraversal_ThrowsException()
    {
        // Arrange
        const string url = "../../../etc/passwd";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void SanitizeUrl_WithMixedCasePathTraversal_ThrowsException()
    {
        // Arrange - Test case insensitivity
        const string url = "https://example.com/data/../Sensitive";

        // Act
        var action = () => StacTextSanitizer.SanitizeUrl(url);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void SanitizeUrl_WithValidDotsInFilename_DoesNotThrow()
    {
        // Arrange - Legitimate filenames with dots should work
        var validUrls = new[]
        {
            "https://example.com/data/file.v1.2.3.tif",
            "https://cdn.example.com/tiles/file..name.png",
            "s3://bucket/path/image.2024.01.15.tif",
            "https://example.com/my.folder/my.file.json"
        };

        foreach (var url in validUrls)
        {
            // Act
            var action = () => StacTextSanitizer.SanitizeUrl(url);

            // Assert
            action.Should().NotThrow($"URL '{url}' should be valid");
        }
    }

    [Fact]
    public void SanitizeUrl_WithVariousPathTraversalPatterns_AllThrow()
    {
        // Arrange - Test comprehensive list of path traversal patterns
        var traversalUrls = new[]
        {
            "https://example.com/data/../../etc/passwd",
            "https://example.com/data/..\\..\\windows\\system32",
            "https://example.com/data/%2e%2e/sensitive",
            "https://example.com/data/%2e%2e\\config",
            "https://example.com/data/%2e%2e%2fsecret",
            "https://example.com/data/%2e%2e%5cwin",
            "file:///data/../../../etc/passwd",
            "s3://bucket/path/../../../other-bucket/file",
            "../../../etc/passwd",
            "..\\..\\windows\\system32\\config"
        };

        foreach (var url in traversalUrls)
        {
            // Act
            var action = () => StacTextSanitizer.SanitizeUrl(url);

            // Assert
            action.Should().Throw<InvalidOperationException>(
                $"URL '{url}' should be rejected as a path traversal attempt");
        }
    }

    [Fact]
    public void SanitizeUrl_WithValidLegitimateUrls_AllPass()
    {
        // Arrange - Ensure legitimate URLs still work
        var validUrls = new[]
        {
            "https://example.com/data/file.tif",
            "s3://bucket/prefix/file.tif",
            "https://cdn.example.com/tiles/14/8192/5461.png",
            "http://example.com/path/to/resource",
            "ftp://ftp.example.com/public/data.zip",
            "gs://my-bucket/datasets/2024/data.json",
            "/relative/path/to/file.json",
            "https://example.com/data/2024.01.15/file.tif"
        };

        foreach (var url in validUrls)
        {
            // Act
            var result = StacTextSanitizer.SanitizeUrl(url);

            // Assert
            result.Should().Be(url, $"Valid URL '{url}' should pass through unchanged");
        }
    }

    #endregion

    #region ValidateAdditionalProperties Tests

    [Fact]
    public void ValidateAdditionalProperties_WithNull_ThrowsException()
    {
        // Act
        var action = () => StacTextSanitizer.ValidateAdditionalProperties(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateAdditionalProperties_WithEmptyDictionary_ReturnsEmpty()
    {
        // Arrange
        var props = new Dictionary<string, object>();

        // Act
        var result = StacTextSanitizer.ValidateAdditionalProperties(props);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAdditionalProperties_WithValidProperties_ReturnsSanitized()
    {
        // Arrange
        var props = new Dictionary<string, object>
        {
            ["custom_field"] = "value",
            ["another_field"] = 42
        };

        // Act
        var result = StacTextSanitizer.ValidateAdditionalProperties(props);

        // Assert
        result.Should().HaveCount(2);
        result["custom_field"].Should().Be("value");
        result["another_field"].Should().Be(42);
    }

    [Fact]
    public void ValidateAdditionalProperties_WithReservedKey_ThrowsException()
    {
        // Arrange
        var reservedKeys = new[]
        {
            "type", "stac_version", "id", "title", "description",
            "license", "extent", "links", "assets"
        };

        foreach (var key in reservedKeys)
        {
            var props = new Dictionary<string, object>
            {
                [key] = "malicious-value"
            };

            // Act
            var action = () => StacTextSanitizer.ValidateAdditionalProperties(props);

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .WithMessage($"*reserved STAC property '{key}'*");
        }
    }

    [Fact]
    public void ValidateAdditionalProperties_WithReservedKeyCaseInsensitive_ThrowsException()
    {
        // Arrange
        var props = new Dictionary<string, object>
        {
            ["TYPE"] = "malicious-value"
        };

        // Act
        var action = () => StacTextSanitizer.ValidateAdditionalProperties(props);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reserved STAC property*");
    }

    [Fact]
    public void ValidateAdditionalProperties_WithXssInStringValue_SanitizesValue()
    {
        // Arrange
        var props = new Dictionary<string, object>
        {
            ["custom_field"] = "<script>alert('xss')</script>"
        };

        // Act
        var result = StacTextSanitizer.ValidateAdditionalProperties(props);

        // Assert
        result["custom_field"].Should().Be("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;");
    }

    [Fact]
    public void ValidateAdditionalProperties_WithXssInStringArray_SanitizesValues()
    {
        // Arrange
        var props = new Dictionary<string, object>
        {
            ["tags"] = new List<string> { "safe", "<script>alert(1)</script>", "normal" }
        };

        // Act
        var result = StacTextSanitizer.ValidateAdditionalProperties(props);

        // Assert
        var tags = result["tags"] as List<string>;
        tags.Should().NotBeNull();
        tags![0].Should().Be("safe");
        tags[1].Should().Contain("&lt;script&gt;");
        tags[2].Should().Be("normal");
    }

    [Fact]
    public void ValidateAdditionalProperties_WithVeryLongKey_ThrowsException()
    {
        // Arrange
        var longKey = new string('a', 300);
        var props = new Dictionary<string, object>
        {
            [longKey] = "value"
        };

        // Act
        var action = () => StacTextSanitizer.ValidateAdditionalProperties(props);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid additional property key*");
    }

    [Fact]
    public void ValidateAdditionalProperties_WithEmptyKey_ThrowsException()
    {
        // Arrange
        var props = new Dictionary<string, object>
        {
            [string.Empty] = "value"
        };

        // Act
        var action = () => StacTextSanitizer.ValidateAdditionalProperties(props);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid additional property key*");
    }

    [Fact]
    public void ValidateAdditionalProperties_WithWhitespaceKey_ThrowsException()
    {
        // Arrange
        var props = new Dictionary<string, object>
        {
            ["   "] = "value"
        };

        // Act
        var action = () => StacTextSanitizer.ValidateAdditionalProperties(props);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid additional property key*");
    }

    [Fact]
    public void ValidateAdditionalProperties_WithNumberValue_PassesThrough()
    {
        // Arrange
        var props = new Dictionary<string, object>
        {
            ["count"] = 42,
            ["rate"] = 3.14
        };

        // Act
        var result = StacTextSanitizer.ValidateAdditionalProperties(props);

        // Assert
        result["count"].Should().Be(42);
        result["rate"].Should().Be(3.14);
    }

    [Fact]
    public void ValidateAdditionalProperties_WithBooleanValue_PassesThrough()
    {
        // Arrange
        var props = new Dictionary<string, object>
        {
            ["enabled"] = true,
            ["disabled"] = false
        };

        // Act
        var result = StacTextSanitizer.ValidateAdditionalProperties(props);

        // Assert
        result["enabled"].Should().Be(true);
        result["disabled"].Should().Be(false);
    }

    #endregion
}
