// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Tests.Data.Data.SqlServer;
using Honua.Server.Core.Tests.Shared;
using Xunit;

namespace Honua.Server.Core.Tests.Data.TestInfrastructure;

/// <summary>
/// xUnit collection definitions for test fixtures.
/// Collection definitions must be in the same assembly as the tests that use them.
/// </summary>

[CollectionDefinition("SharedPostgres")]
public class SharedPostgresCollection : ICollectionFixture<SharedPostgresFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

[CollectionDefinition("DatabaseTests")]
public class DatabaseTestsCollection : ICollectionFixture<SqlServerDataStoreProviderFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
