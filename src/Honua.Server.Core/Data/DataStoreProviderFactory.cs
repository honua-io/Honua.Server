// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Data;

public sealed class DataStoreProviderFactory : DependencyInjectionProviderFactoryBase<IDataStoreProvider>, IDataStoreProviderFactory
{
    public DataStoreProviderFactory(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    public IDataStoreProvider Create(string providerName)
    {
        return CreateProvider(providerName);
    }
}
