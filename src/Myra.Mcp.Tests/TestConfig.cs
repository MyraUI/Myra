// Myra's environment (MyraEnvironment.Platform, Stylesheet.Current) is process-global mutable
// state, so tests must not run in parallel over it.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
