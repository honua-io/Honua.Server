using System;
using FluentAssertions;
using Honua.Server.Core.Data.Postgres;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Postgres;

[Collection("DatabaseTests")]
[Trait("Category", "Integration")]
public sealed class PostgresDataStoreProviderConnectionTests
{
    [Theory]
    [InlineData("Host=localhost;Database=honua;Username=postgres;Password=secret", "Host=localhost;Database=honua;Username=postgres;Password=secret")]
    [InlineData("postgres://user:pass@db.example.com:5432/honua", "Host=db.example.com;Username=user;Password=pass;Database=honua;Port=5432")]
    [InlineData("SERVER=legacy;DATABASE=old;User Id=foo;Password=bar;", "Host=legacy;Database=old")]
    public void NormalizeConnectionString_ShouldProduceCompatibleString(string input, string expectedFragment)
    {
        var method = typeof(PostgresConnectionManager).GetMethod(
            "NormalizeConnectionString",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic) ?? throw new InvalidOperationException("Method not found");

        var normalized = (string)method.Invoke(null, new object[] { input })!;

        foreach (var segment in expectedFragment.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            normalized.Should().Contain(segment, because: "normalization should keep '{0}'", segment);
        }
    }
}
