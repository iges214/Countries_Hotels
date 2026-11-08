using Xunit;

// WebApplicationFactory instances share Program's static Serilog bootstrap logger.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
