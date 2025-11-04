param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

$root = Join-Path $PSScriptRoot ".."
$project = Join-Path $root "src/Honua.Cli/Honua.Cli.csproj"

& dotnet run --project $project -- @RemainingArgs
exit $LASTEXITCODE
