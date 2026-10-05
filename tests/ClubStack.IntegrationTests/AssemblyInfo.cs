using Xunit;

// The D0 integration suite shares one real PostgreSQL database created by the
// collection fixture. Disabling parallelization keeps per-test data in
// deterministic, isolated, ordered shape.
[assembly: CollectionBehavior(DisableTestParallelization = true)]