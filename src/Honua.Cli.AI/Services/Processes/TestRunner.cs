// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Simple test runner entry point for Process Framework tests.
/// Can be invoked from external test harness or CLI.
/// </summary>
public static class ProcessTestRunner
{
    public static void Main(string[] args)
    {
        ProcessFrameworkTest.RunTests();
    }

    public static void Run()
    {
        ProcessFrameworkTest.RunTests();
    }
}
