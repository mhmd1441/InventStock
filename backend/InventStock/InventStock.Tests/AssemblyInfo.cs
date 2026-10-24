using Xunit;

// The integration suite deliberately provisions large PostgreSQL schemas and several in-process API
// hosts. Running those collections together can exhaust the single disposable test database/host and
// terminate VSTest before it can write a result. Database concurrency is tested explicitly inside
// the dedicated tests; the suite itself must be deterministic and resource-bounded.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
