# xunit-v3-parallel-all-demo

Companion repo for the post **When to turn on xUnit v3's "all" parallel mode, and which
fixtures it breaks** on [gdhami.net](https://gdhami.net).

The same test files are compiled into two assemblies. The only difference between them is one
file, `mode-all/ParallelAll.cs`:

```csharp
using Xunit.Sdk;
using Xunit.v3;

[assembly: Parallelization(Mode = ParallelMode.All)]
```

A check program runs each workload several times in each mode and asserts what the post says.

## Run it

```
dotnet run --project check/Check.csproj -c Release
```

It takes about two minutes and exits non-zero if a claim fails. To run the tests on their own:

```
dotnet test --project mode-all/ModeAll.csproj -c Release --filter-class Demo.Overlap.MeetTests
```

Some classes under `Demo.Broken` are meant to fail in `all` mode, so an unfiltered run of
`ModeAll` is expected to go red.

## Versions

| Thing | Version |
|---|---|
| .NET SDK | 10.0.201 (runtime 10.0.5) |
| xunit.v3 | 4.0.2 |
| Machine the numbers came from | Intel Core Ultra 9 285HX, 24 logical CPUs, Windows 11 |

`global.json` sets `"test": {"runner": "Microsoft.Testing.Platform"}`, so `dotnet test` on a .NET 10
SDK runs these projects with Microsoft Testing Platform, as the
[xUnit docs](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) describe.

## What it proves

| Claim | Where |
|---|---|
| The attribute compiles with `Xunit.Sdk` and `Xunit.v3`, and not with `using Xunit` alone | `mode-all/ParallelAll.cs` |
| 40 awaits of 100 ms in one class: about 4.4 s under `collections`, about 0.24 s under `all` | `AwaitTests`, timed by the check |
| `Thread.Sleep` tests gain the same; so do CPU-bound tests when cores are free | `SleepTests`, `CpuTests` |
| Under the default algorithm the peak equals the thread limit, for awaiting tests too | peak printed by the check; `-maxThreads 2` and `4` runs |
| 40 classes of one await gain nothing, because classes already run side by side | `Demo.ManyClasses` |
| One class fixture per class; one test object per test | `fixtures` and `testObjects` counters |
| Two tests of a class overlap under `all`, and never under `collections` | `MeetTests`, using a `Barrier` so it doesn't depend on timing |
| Two classes in one collection overlap under `all` | `Demo.Collections` |
| A collection marked `DisableParallelization = true` goes back to one at a time | `Demo.SerialCollection` |
| A plain `List<int>` in a class fixture loses entries or throws under `all` | `PlainListTests` (printed, never asserted, because it's a race) |
| A reader and a writer forced to overlap make the reader throw, every run | `ReaderWriterTests` |
| `lock`, `ConcurrentBag<int>` and a per-test list all keep every entry | `Demo.Safe` |
| `DisableParallelism` is a compile error on five attributes; `DisableParallelization` compiles on all of them | `opt-out-probe/Names.cs`, built by the check and meant to fail |
| `-parallelMode all` on the test executable turns the mode on with no code change | the check runs the assembly without the attribute |

`Demo.PostSnippets` holds the fixture exactly as the post prints it, without the recording the
other classes carry. `PlainListTests` under `Demo.Broken` is the same code plus that recording.

## Edge cases the post leaves out

- **The numbers depend on the machine.** The `all` figures above are from 24 logical CPUs. The
  check passes `-maxThreads 2` and `-maxThreads 4` to show what a small build machine would get.
  Timing assertions use wide margins, and the ones that need several cores are skipped below 4 CPUs.
- **The aggressive algorithm** (`-parallelAlgorithm aggressive`) ignores the thread limit for
  awaiting tests: 40 tests ran at once with `-maxThreads 4`, in about 120 ms. The
  [docs](https://xunit.net/docs/running-tests-in-parallel) warn that `Timeout` on `[Fact]` becomes
  less reliable under it, and recommend staying with the default unless tests run much slower.
- **`Thread.Sleep` under the aggressive algorithm** peaked at 24, while awaiting tests reached 40.
- **Theory rows** also run in parallel under `all`. `PlainListTests` is one `[Theory]` with 40 rows.
- **SHA256 did not scale** in this repo on Windows, inside xUnit or in a plain `Parallel.For`. The
  check prints it and asserts nothing. I don't know why, and other platforms may differ.
- **Opted-out tests and other tests.** The docs say an opted-out test is guaranteed not to run in
  parallel with any other test. The check only asserts the peak inside the opted-out class, not
  against tests in other classes.
- **Method and data opt-outs.** `[Fact(DisableParallelization = true)]` is asserted. The same
  property on `[Theory]`, `[InlineData]` and `[CollectionDefinition]` is compile-checked in
  `opt-out-probe`, and only `[CollectionDefinition]` has a run-time check here.
- **`opt-out-probe` is meant not to build.** Don't add it to a solution.
