using BenchmarkDotNet.Running;

namespace Honua.Benchmarks;

internal class Program
{
    private static void Main(string[] args)
    {
        // Print environment info
        RasterBenchmarkHelper.PrintEnvironmentInfo();

        // Ensure test data directory exists
        RasterBenchmarkHelper.EnsureTestDataDirectory();

        // Run benchmarks
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
