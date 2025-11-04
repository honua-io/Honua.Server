using Xunit;

// Configure xUnit to run tests in parallel
[assembly: CollectionBehavior(
    DisableTestParallelization = false,
    MaxParallelThreads = 4)]
