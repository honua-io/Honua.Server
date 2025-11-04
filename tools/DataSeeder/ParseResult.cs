namespace Honua.Tools.DataSeeder;

internal readonly record struct ParseResult(bool Success, SeedOptions? Options, string? ErrorMessage)
{
    public static ParseResult Ok(SeedOptions options) => new(true, options, null);
    public static ParseResult Fail(string error) => new(false, null, error);
}
