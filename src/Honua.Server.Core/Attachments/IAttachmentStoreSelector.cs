// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

/// <summary>
/// Resolves an attachment store based on metadata/storage profile identifiers.
/// </summary>
public interface IAttachmentStoreSelector
{
    IAttachmentStore Resolve(string storageProfileId);
}

public sealed class AttachmentStoreNotFoundException : Exception
{
    public AttachmentStoreNotFoundException(string storageProfileId)
        : base($"Attachment storage profile '{storageProfileId}' could not be resolved.")
    {
        StorageProfileId = storageProfileId;
    }

    public string StorageProfileId { get; }
}
