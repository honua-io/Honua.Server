// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.SemanticKernel;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Simplest possible process - just kernel functions without state inheritance.
/// Testing if basic ProcessBuilder works.
/// </summary>
public class SimpleStep
{
    [KernelFunction("Execute")]
    public async Task<string> ExecuteAsync(string input)
    {
        await Task.Delay(100).ConfigureAwait(false);
        return $"Processed: {input}";
    }
}
