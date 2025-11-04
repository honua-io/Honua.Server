// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Exception thrown when an attachment operation fails.
/// </summary>
public class AttachmentException : HonuaException
{
    public AttachmentException(string message) : base(message)
    {
    }

    public AttachmentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when an attachment is not found.
/// </summary>
public sealed class AttachmentNotFoundException : AttachmentException
{
    public int? AttachmentId { get; }
    public string? FeatureId { get; }

    public AttachmentNotFoundException(int attachmentId, string? featureId = null)
        : base(featureId is null
            ? $"Attachment {attachmentId} was not found."
            : $"Attachment {attachmentId} was not found for feature '{featureId}'.")
    {
        AttachmentId = attachmentId;
        FeatureId = featureId;
    }

    public AttachmentNotFoundException(string message) : base(message)
    {
    }
}

/// <summary>
/// Exception thrown when an attachment configuration is invalid.
/// </summary>
public sealed class AttachmentConfigurationException : AttachmentException
{
    public string? ProfileId { get; }

    public AttachmentConfigurationException(string message) : base(message)
    {
    }

    public AttachmentConfigurationException(string profileId, string message)
        : base(message)
    {
        ProfileId = profileId;
    }
}

/// <summary>
/// Exception thrown when an attachment store validation fails.
/// </summary>
public sealed class AttachmentValidationException : AttachmentException
{
    public AttachmentValidationException(string message) : base(message)
    {
    }

    public AttachmentValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
