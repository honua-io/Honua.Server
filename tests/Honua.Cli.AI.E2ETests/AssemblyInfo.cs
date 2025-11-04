using Xunit;

// E2E tests run sequentially due to resource constraints
[assembly: CollectionBehavior(
    DisableTestParallelization = true,
    MaxParallelThreads = 1)]
