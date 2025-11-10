// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Xunit;

namespace Honua.Cli.Tests.Commands;

public class CommandParserTests
{
    [Fact]
    public void ParseCommand_WithValidCommand_ReturnsCommandName()
    {
        // Arrange
        var args = new[] { "server", "start", "--port", "8080" };
        var parser = new CommandParser();

        // Act
        var result = parser.Parse(args);

        // Assert
        result.CommandName.Should().Be("server");
        result.SubCommand.Should().Be("start");
    }

    [Fact]
    public void ParseCommand_WithOptions_ExtractsOptions()
    {
        // Arrange
        var args = new[] { "config", "set", "--key", "apiUrl", "--value", "https://api.example.com" };
        var parser = new CommandParser();

        // Act
        var result = parser.Parse(args);

        // Assert
        result.Options.Should().ContainKey("key");
        result.Options["key"].Should().Be("apiUrl");
        result.Options.Should().ContainKey("value");
        result.Options["value"].Should().Be("https://api.example.com");
    }

    [Fact]
    public void ParseCommand_WithFlags_ExtractsFlags()
    {
        // Arrange
        var args = new[] { "deploy", "--verbose", "--dry-run" };
        var parser = new CommandParser();

        // Act
        var result = parser.Parse(args);

        // Assert
        result.Flags.Should().Contain("verbose");
        result.Flags.Should().Contain("dry-run");
    }

    [Fact]
    public void ParseCommand_WithNoArgs_ReturnsEmptyCommand()
    {
        // Arrange
        var args = Array.Empty<string>();
        var parser = new CommandParser();

        // Act
        var result = parser.Parse(args);

        // Assert
        result.CommandName.Should().BeNullOrEmpty();
    }

    [Theory]
    [InlineData("help")]
    [InlineData("version")]
    [InlineData("list")]
    public void ParseCommand_WithSingleCommand_ParsesCorrectly(string command)
    {
        // Arrange
        var args = new[] { command };
        var parser = new CommandParser();

        // Act
        var result = parser.Parse(args);

        // Assert
        result.CommandName.Should().Be(command);
    }
}

// Mock implementation for testing
public class CommandParser
{
    public CommandResult Parse(string[] args)
    {
        var result = new CommandResult();

        if (args.Length == 0) return result;

        result.CommandName = args[0];

        if (args.Length > 1 && !args[1].StartsWith("--"))
        {
            result.SubCommand = args[1];
        }

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i].TrimStart('-');
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    result.Options[key] = args[i + 1];
                    i++;
                }
                else
                {
                    result.Flags.Add(key);
                }
            }
        }

        return result;
    }
}

public class CommandResult
{
    public string CommandName { get; set; } = string.Empty;
    public string SubCommand { get; set; } = string.Empty;
    public Dictionary<string, string> Options { get; set; } = new();
    public List<string> Flags { get; set; } = new();
}
