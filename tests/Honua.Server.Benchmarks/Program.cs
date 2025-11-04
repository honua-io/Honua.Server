using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using Honua.Server.Benchmarks;

// Run all benchmarks in the assembly
// Usage: dotnet run -c Release --project tests/Honua.Server.Benchmarks
//
// To run specific benchmarks:
// dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*CsvWkt*"
// dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*Shapefile*"
// dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*Database*"

// Print environment info
PrintEnvironmentInfo();

// Use custom configuration if no args provided
if (args.Length == 0)
{
    var config = DefaultConfig.Instance
        .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance))
        .AddExporter(MarkdownExporter.GitHub)
        .AddExporter(JsonExporter.FullCompressed)
        .AddLogger(ConsoleLogger.Default);

    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}

static void PrintEnvironmentInfo()
{
    Console.WriteLine("=================================================");
    Console.WriteLine("Honua Server Performance Benchmarks");
    Console.WriteLine("=================================================");
    Console.WriteLine($"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    Console.WriteLine($"OS: {Environment.OSVersion}");
    Console.WriteLine($"Runtime: {Environment.Version}");
    Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
    Console.WriteLine($"Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
    Console.WriteLine("=================================================");
    Console.WriteLine();
}
