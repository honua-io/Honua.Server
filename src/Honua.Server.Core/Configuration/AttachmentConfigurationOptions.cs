// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for attachment storage.
/// </summary>
public sealed class AttachmentConfigurationOptions
{
    public const string SectionName = "Honua:Attachments";

    public int DefaultMaxSizeMiB { get; init; } = 25;
    public Dictionary<string, AttachmentStorageProfileOptions> Profiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AttachmentStorageProfileOptions
{
    public string Provider { get; init; } = "filesystem";
    public AttachmentFileSystemStorageOptions? FileSystem { get; init; }
    public AttachmentS3StorageOptions? S3 { get; init; }
    public AttachmentAzureBlobStorageOptions? Azure { get; init; }
    public AttachmentGcsStorageOptions? Gcs { get; init; }
    public AttachmentDatabaseStorageOptions? Database { get; init; }
}

public sealed class AttachmentFileSystemStorageOptions
{
    public string RootPath { get; init; } = "data/attachments";
}

public sealed class AttachmentS3StorageOptions
{
    public string? BucketName { get; init; }
    public string? Prefix { get; init; }
    public string? Region { get; init; }
    public string? ServiceUrl { get; init; }
    public string? AccessKeyId { get; init; }
    public string? SecretAccessKey { get; init; }
    public bool ForcePathStyle { get; init; }
    public bool UseInstanceProfile { get; init; } = true;
    public int PresignExpirySeconds { get; init; } = 900;
}

public sealed class AttachmentAzureBlobStorageOptions
{
    public string? ConnectionString { get; init; }
    public string? ContainerName { get; init; }
    public string? Prefix { get; init; }
    public bool EnsureContainer { get; init; } = true;
}

public sealed class AttachmentGcsStorageOptions
{
    public string? BucketName { get; init; }
    public string? Prefix { get; init; }
    public string? ProjectId { get; init; }
    public string? CredentialsPath { get; init; }
    public bool UseApplicationDefaultCredentials { get; init; } = true;
}

public sealed class AttachmentDatabaseStorageOptions
{
    public string Provider { get; init; } = "sqlite";
    public string? ConnectionString { get; init; }
    public string? Schema { get; init; }
    public string? TableName { get; init; }
    public string AttachmentIdColumn { get; init; } = "attachment_id";
    public string ContentColumn { get; init; } = "content";
    public string? FileNameColumn { get; init; }
}
