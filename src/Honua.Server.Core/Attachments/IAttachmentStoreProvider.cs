// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using Honua.Server.Core.Configuration;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

/// <summary>
/// Factory abstraction that builds attachment stores for a given provider type.
/// </summary>
public interface IAttachmentStoreProvider
{
    string ProviderKey { get; }
    IAttachmentStore Create(string profileId, AttachmentStorageProfileOptions profileConfiguration);
}
