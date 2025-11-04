using Honua.Cli.AI.Services.Processes;

Console.WriteLine("Starting Process Framework Integration Test...\n");

try
{
    ProcessFrameworkTest.RunTests();
    Console.WriteLine("\nTest completed successfully!");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\nTest failed with exception: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}
