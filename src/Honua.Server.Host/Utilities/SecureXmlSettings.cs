// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Xml;
using System.Xml.Linq;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Provides secure XML parsing settings to prevent XXE (XML External Entity) attacks.
/// </summary>
/// <remarks>
/// This class enforces the following security measures:
/// - DTD processing is prohibited to prevent entity expansion attacks
/// - XmlResolver is set to null to prevent external entity resolution
/// - Character limits prevent denial-of-service attacks
/// - Document size limits prevent memory exhaustion
/// </remarks>
internal static class SecureXmlSettings
{
    /// <summary>
    /// Maximum characters allowed from entity expansion (0 = no entity expansion allowed).
    /// </summary>
    public const long MaxCharactersFromEntities = 0;

    /// <summary>
    /// Maximum characters allowed in an XML document (10MB of text).
    /// This prevents denial-of-service attacks via extremely large XML documents.
    /// </summary>
    public const long MaxCharactersInDocument = 10_000_000;

    /// <summary>
    /// Maximum size of XML input stream in bytes (50MB).
    /// This is an additional layer of protection at the stream level.
    /// </summary>
    public const int MaxInputStreamSize = 50 * 1024 * 1024;

    /// <summary>
    /// Creates secure XmlReaderSettings configured to prevent XXE attacks.
    /// </summary>
    /// <returns>Configured XmlReaderSettings with security hardening applied.</returns>
    public static XmlReaderSettings CreateSecureSettings()
    {
        return new XmlReaderSettings
        {
            // Prohibit DTD processing entirely - this is the primary defense against XXE
            DtdProcessing = DtdProcessing.Prohibit,

            // Disable all external resource resolution
            XmlResolver = null,

            // Prevent entity expansion attacks
            MaxCharactersFromEntities = MaxCharactersFromEntities,

            // Prevent DoS via extremely large documents
            MaxCharactersInDocument = MaxCharactersInDocument,

            // Enable validation and other standard settings
            IgnoreWhitespace = false,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,

            // Close input stream when reader is disposed
            CloseInput = false // Let caller manage stream lifecycle
        };
    }

    /// <summary>
    /// Parses XML from a string using secure settings.
    /// </summary>
    /// <param name="xml">The XML string to parse.</param>
    /// <param name="loadOptions">XDocument load options (default: None).</param>
    /// <returns>Parsed XDocument.</returns>
    /// <exception cref="ArgumentException">Thrown when XML is null or whitespace.</exception>
    /// <exception cref="XmlException">Thrown when XML is malformed or contains prohibited constructs.</exception>
    public static XDocument ParseSecure(string xml, LoadOptions loadOptions = LoadOptions.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml, nameof(xml));

        // Check input size before parsing
        if (xml.Length > MaxCharactersInDocument)
        {
            throw new XmlException($"XML document exceeds maximum allowed size of {MaxCharactersInDocument} characters.");
        }

        using var stringReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(stringReader, CreateSecureSettings());
        return XDocument.Load(xmlReader, loadOptions);
    }

    /// <summary>
    /// Loads XML from a stream using secure settings.
    /// </summary>
    /// <param name="stream">The stream containing XML data.</param>
    /// <param name="loadOptions">XDocument load options (default: None).</param>
    /// <returns>Parsed XDocument.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="XmlException">Thrown when XML is malformed or contains prohibited constructs.</exception>
    public static XDocument LoadSecure(Stream stream, LoadOptions loadOptions = LoadOptions.None)
    {
        Guard.NotNull(stream);

        using var xmlReader = XmlReader.Create(stream, CreateSecureSettings());
        return XDocument.Load(xmlReader, loadOptions);
    }

    /// <summary>
    /// Asynchronously loads XML from a stream using secure settings.
    /// </summary>
    /// <param name="stream">The stream containing XML data.</param>
    /// <param name="loadOptions">XDocument load options (default: None).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that resolves to parsed XDocument.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="XmlException">Thrown when XML is malformed or contains prohibited constructs.</exception>
    public static async Task<XDocument> LoadSecureAsync(
        Stream stream,
        LoadOptions loadOptions = LoadOptions.None,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(stream);

        using var xmlReader = XmlReader.Create(stream, CreateSecureSettings());
        return await XDocument.LoadAsync(xmlReader, loadOptions, cancellationToken);
    }

    /// <summary>
    /// Validates that a stream is not too large before processing.
    /// </summary>
    /// <param name="stream">The stream to validate.</param>
    /// <exception cref="XmlException">Thrown when stream exceeds maximum size.</exception>
    public static void ValidateStreamSize(Stream stream)
    {
        Guard.NotNull(stream);

        if (stream.CanSeek && stream.Length > MaxInputStreamSize)
        {
            throw new XmlException($"XML stream exceeds maximum allowed size of {MaxInputStreamSize} bytes ({MaxInputStreamSize / 1024 / 1024}MB).");
        }
    }
}
