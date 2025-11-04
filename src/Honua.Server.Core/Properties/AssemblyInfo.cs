using System.Runtime.CompilerServices;

// Make internal types visible to test assemblies
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests")]
[assembly: InternalsVisibleTo("Honua.Server.Core.Tests.DataOperations")]
[assembly: InternalsVisibleTo("Honua.Server.Integration.Tests")]
