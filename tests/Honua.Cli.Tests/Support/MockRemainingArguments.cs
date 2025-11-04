using Spectre.Console.Cli;

namespace Honua.Cli.Tests.Support;

public sealed class MockRemainingArguments : IRemainingArguments
{
    public IReadOnlyList<string> Raw { get; init; }
    public ILookup<string, string?> Parsed { get; init; }

    public MockRemainingArguments()
    {
        Raw = Array.Empty<string>();
        Parsed = Enumerable.Empty<KeyValuePair<string, string?>>()
            .ToLookup(kvp => kvp.Key, kvp => kvp.Value);
    }
}
