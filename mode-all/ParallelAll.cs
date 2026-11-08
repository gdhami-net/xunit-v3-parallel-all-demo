using Xunit.Sdk;
using Xunit.v3;

// The only line that differs between the two test assemblies.
[assembly: Parallelization(Mode = ParallelMode.All)]
