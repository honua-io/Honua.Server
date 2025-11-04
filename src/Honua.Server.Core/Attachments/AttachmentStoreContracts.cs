// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

/// <summary>
/// Represents a pointer to binary content persisted by an attachment store.
/// </summary>
public sealed record AttachmentPointer
{
    private static readonly IReadOnlyDictionary<string, string?> EmptyProperties = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>());

    public AttachmentPointer(string storageProvider, string storageKey, IReadOnlyDictionary<string, string?>? properties = null)
    {
        Guard.NotNullOrWhiteSpace(storageProvider);
        Guard.NotNullOrWhiteSpace(storageKey);

        StorageProvider = storageProvider;
        StorageKey = storageKey;
        Properties = properties ?? EmptyProperties;
    }

    public string StorageProvider { get; }
    public string StorageKey { get; }
    public IReadOnlyDictionary<string, string?> Properties { get; }
}

/// <summary>
/// Describes a request to persist attachment content in a store.
/// </summary>
public sealed record AttachmentStorePutRequest
{
    public required string AttachmentId { get; init; }
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public long SizeBytes { get; init; }
    public required string ChecksumSha256 { get; init; }
    public IReadOnlyDictionary<string, string?> Metadata { get; init; } = AttachmentPointerExtensions.EmptyProperties;
}

/// <summary>
/// Describes the outcome of storing an attachment.
/// </summary>
public sealed record AttachmentStoreWriteResult
{
    public required AttachmentPointer Pointer { get; init; }
    public DateTimeOffset StoredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents the binary content retrieved from an attachment store.
/// </summary>
public sealed record AttachmentReadResult
{
    public required Stream Content { get; init; }
    public string? MimeType { get; init; }
    public long? SizeBytes { get; init; }
    public string? FileName { get; init; }
    public string? ChecksumSha256 { get; init; }
}

/// <summary>
/// Attachment storage abstraction that allows Honua to plug different backing stores.
/// </summary>
public interface IAttachmentStore
{
    Task<AttachmentStoreWriteResult> PutAsync(Stream content, AttachmentStorePutRequest request, CancellationToken cancellationToken = default);
    Task<AttachmentReadResult?> TryGetAsync(AttachmentPointer pointer, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(AttachmentPointer pointer, CancellationToken cancellationToken = default);
    IAsyncEnumerable<AttachmentPointer> ListAsync(string? prefix = null, CancellationToken cancellationToken = default);
}

internal static class AttachmentPointerExtensions
{
    internal static readonly IReadOnlyDictionary<string, string?> EmptyProperties = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>());
}
