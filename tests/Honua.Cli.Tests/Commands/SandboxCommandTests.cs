using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.Commands;
using Spectre.Console.Testing;

namespace Honua.Cli.Tests.Commands;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class SandboxCommandTests
{
    [Fact]
    public async Task Up_ShouldFailWhenComposeFileMissing()
    {
        var console = new TestConsole();
        var command = new SandboxUpCommand(console);
        var settings = new SandboxUpCommand.Settings
        {
            ComposeFile = "does-not-exist.yml"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("Unable to locate docker compose file");
    }
}
