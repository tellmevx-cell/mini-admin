using Xunit;

// WebApplicationFactory hosts initialize fixed seed IDs; serialize integration tests so
// independent in-memory stores cannot race through EF's shared service provider.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
