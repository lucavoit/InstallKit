using Xunit;

// WixSharp writes a bundle's source into the current directory, which the tests change.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
