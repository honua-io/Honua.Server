// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Diagnostics.Metrics;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

public sealed class FileSystemAttachmentStoreProvider : IAttachmentStoreProvider
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMeterFactory? _meterFactory;

    public FileSystemAttachmentStoreProvider(ILoggerFactory loggerFactory, IMeterFactory? meterFactory = null)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _meterFactory = meterFactory;
    }

    public string ProviderKey => AttachmentStoreProviderKeys.FileSystem;

    public IAttachmentStore Create(string profileId, AttachmentStorageProfileConfiguration profileConfiguration)
    {
        Guard.NotNull(profileConfiguration);
        var rootPath = profileConfiguration.FileSystem?.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException($"File system attachment profile '{profileId}' must specify fileSystem.rootPath.");
        }

        return new FileSystemAttachmentStore(rootPath, _loggerFactory.CreateLogger<FileSystemAttachmentStore>(), _meterFactory);
    }
}
