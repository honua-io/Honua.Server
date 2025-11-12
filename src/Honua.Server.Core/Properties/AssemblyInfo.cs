// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;

// Make internal types visible to test assemblies
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests")]
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.DataOperations")]
[assembly: InternalsVisibleTo("Honua.Server.Integration.Tests")]
