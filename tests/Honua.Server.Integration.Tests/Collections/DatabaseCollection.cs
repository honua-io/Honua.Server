// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Integration.Tests.Fixtures;
using Xunit;

namespace Honua.Server.Integration.Tests.Collections;

/// <summary>
/// Defines a test collection that shares a single DatabaseFixture instance across all test classes.
/// This ensures only one set of containers (Postgres, MySQL, Redis) is created for all tests,
/// rather than creating separate containers for each test class.
/// </summary>
[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // This class is never instantiated.
    // It exists only to apply [CollectionDefinition] and define the shared fixture collection.
}
