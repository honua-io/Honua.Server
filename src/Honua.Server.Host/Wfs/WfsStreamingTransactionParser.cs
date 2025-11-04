// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Results;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Provides streaming XML parsing for WFS Transaction requests to prevent memory exhaustion.
/// Uses XmlReader for incremental parsing instead of loading entire document as XDocument.
/// </summary>
internal static class WfsStreamingTransactionParser
{
    /// <summary>
    /// Represents a parsed WFS Transaction operation (Insert, Update, or Delete).
    /// </summary>
    internal sealed record TransactionOperation(
        WfsTransactionOperationType Type,
        string? TypeName,
        string? LockId,
        XElement Element);

    /// <summary>
    /// Type of WFS Transaction operation.
    /// </summary>
    internal enum WfsTransactionOperationType
    {
        Insert,
        Update,
        Delete,
        Replace
    }

    /// <summary>
    /// Metadata about a WFS Transaction request.
    /// </summary>
    internal sealed record TransactionMetadata(
        string? LockId,
        string? ReleaseAction,
        string? Handle);

    /// <summary>
    /// Parses WFS Transaction XML incrementally using streaming XML reader.
    /// This prevents loading the entire document into memory.
    /// </summary>
    /// <param name="stream">The input stream containing WFS Transaction XML.</param>
    /// <param name="maxOperations">Maximum number of operations allowed in the transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing transaction metadata and enumerable of operations.</returns>
    /// <exception cref="XmlException">Thrown when XML is malformed or exceeds limits.</exception>
    public static async Task<Result<(TransactionMetadata Metadata, IAsyncEnumerable<TransactionOperation> Operations)>>
        ParseTransactionStreamAsync(
            Stream stream,
            int maxOperations,
            CancellationToken cancellationToken)
    {
        Guard.NotNull(stream);

        try
        {
            // Validate stream size before parsing
            SecureXmlSettings.ValidateStreamSize(stream);

            // Create secure XML reader settings
            var settings = SecureXmlSettings.CreateSecureSettings();
            settings.Async = true;

            using var reader = XmlReader.Create(stream, settings);

            // Parse transaction root element and extract metadata
            var metadataResult = await ParseTransactionRootAsync(reader, cancellationToken).ConfigureAwait(false);
            if (metadataResult.IsFailure)
            {
                return Result<(TransactionMetadata, IAsyncEnumerable<TransactionOperation>)>.Failure(metadataResult.Error!);
            }

            var metadata = metadataResult.Value;

            // Create async enumerable for operations
            var operations = ParseOperationsAsync(reader, maxOperations, cancellationToken);

            return Result<(TransactionMetadata, IAsyncEnumerable<TransactionOperation>)>.Success((metadata, operations));
        }
        catch (XmlException ex)
        {
            return Result<(TransactionMetadata, IAsyncEnumerable<TransactionOperation>)>.Failure(
                Error.Invalid($"Transaction payload is not valid XML: {ex.Message}"));
        }
        catch (Exception ex) when (ex.Message.Contains("maximum allowed size"))
        {
            return Result<(TransactionMetadata, IAsyncEnumerable<TransactionOperation>)>.Failure(
                Error.Invalid($"Transaction payload too large: {ex.Message}"));
        }
    }

    /// <summary>
    /// Parses the Transaction root element and extracts metadata attributes.
    /// </summary>
    private static async Task<Result<TransactionMetadata>> ParseTransactionRootAsync(
        XmlReader reader,
        CancellationToken cancellationToken)
    {
        // Navigate to Transaction element
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Result<TransactionMetadata>.Failure(Error.Invalid("Transaction parsing was cancelled."));
            }

            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.LocalName == "Transaction" &&
                    reader.NamespaceURI == WfsConstants.Wfs.NamespaceName)
                {
                    var lockId = reader.GetAttribute("lockId");
                    var releaseAction = reader.GetAttribute("releaseAction");
                    var handle = reader.GetAttribute("handle");

                    if (releaseAction.HasValue() &&
                        !releaseAction.EqualsIgnoreCase("ALL") &&
                        !releaseAction.EqualsIgnoreCase("SOME"))
                    {
                        return Result<TransactionMetadata>.Failure(
                            Error.Invalid("releaseAction must be ALL or SOME."));
                    }

                    return Result<TransactionMetadata>.Success(
                        new TransactionMetadata(lockId, releaseAction, handle));
                }
                else
                {
                    return Result<TransactionMetadata>.Failure(
                        Error.Invalid("Transaction payload must contain a wfs:Transaction element."));
                }
            }
        }

        return Result<TransactionMetadata>.Failure(
            Error.Invalid("Transaction payload must contain a wfs:Transaction element."));
    }

    /// <summary>
    /// Parses transaction operations (Insert, Update, Delete) as an async enumerable.
    /// This allows processing features incrementally without loading entire payload.
    /// </summary>
    private static async IAsyncEnumerable<TransactionOperation> ParseOperationsAsync(
        XmlReader reader,
        int maxOperations,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var operationCount = 0;
        var depth = reader.Depth;

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Stop if we've exited the Transaction element
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth <= depth)
            {
                break;
            }

            if (reader.NodeType == XmlNodeType.Element &&
                reader.NamespaceURI == WfsConstants.Wfs.NamespaceName)
            {
                var localName = reader.LocalName;

                if (localName == "Insert" || localName == "Update" || localName == "Delete" || localName == "Replace")
                {
                    operationCount++;

                    if (operationCount > maxOperations)
                    {
                        throw new XmlException(
                            $"Transaction exceeds maximum allowed operations ({maxOperations}). " +
                            $"Configure honua:wfs:MaxTransactionFeatures to increase this limit.");
                    }

                    // Read the operation element as XElement (still memory efficient for single operation)
                    var element = (XElement)XNode.ReadFrom(reader);

                    var operationType = localName switch
                    {
                        "Insert" => WfsTransactionOperationType.Insert,
                        "Update" => WfsTransactionOperationType.Update,
                        "Delete" => WfsTransactionOperationType.Delete,
                        "Replace" => WfsTransactionOperationType.Replace,
                        _ => throw new InvalidOperationException($"Unknown operation type: {localName}")
                    };

                    // For Update, Delete, and Replace, extract typeName directly
                    string? typeName = null;
                    if (operationType == WfsTransactionOperationType.Update ||
                        operationType == WfsTransactionOperationType.Delete ||
                        operationType == WfsTransactionOperationType.Replace)
                    {
                        typeName = element.Attribute("typeName")?.Value ??
                                   element.Attribute(XName.Get("typeName"))?.Value;
                    }

                    yield return new TransactionOperation(operationType, typeName, null, element);
                }
            }
        }
    }

    /// <summary>
    /// Extracts feature elements from an Insert operation element.
    /// For Insert operations, each child element is a feature to insert.
    /// </summary>
    public static IEnumerable<XElement> ExtractInsertFeatures(XElement insertElement)
    {
        Guard.NotNull(insertElement);

        foreach (var featureElement in insertElement.Elements())
        {
            // Skip non-feature elements (e.g., metadata)
            if (featureElement.Name.NamespaceName != WfsConstants.Wfs.NamespaceName)
            {
                yield return featureElement;
            }
        }
    }

    /// <summary>
    /// Counts the total number of features/operations in a parsed transaction.
    /// Used for validation before processing.
    /// </summary>
    public static async Task<int> CountOperationsAsync(
        IAsyncEnumerable<TransactionOperation> operations,
        CancellationToken cancellationToken)
    {
        var count = 0;

        await foreach (var operation in operations.WithCancellation(cancellationToken))
        {
            count++;

            // For Insert operations, count each feature separately
            if (operation.Type == WfsTransactionOperationType.Insert)
            {
                foreach (var _ in ExtractInsertFeatures(operation.Element))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
