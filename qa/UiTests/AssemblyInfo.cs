using Xunit;

// The tests share one machine and one product (the QA demo), so they run one after another.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
