using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

// Builds both test assemblies, runs each workload several times in each mode and asserts what
// the post says. Timing checks use wide margins. A lost-entries count is a race, so it is printed
// and never asserted; overlap is proven with a meeting point that doesn't depend on timing.

var root = FindRoot();
var outDir = Path.Combine(root, ".results");
Directory.CreateDirectory(outDir);
var failures = 0;

void Pass(bool ok, string claim, string detail = "")
{
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {claim}");
    if (detail.Length > 0) Console.WriteLine($"      {detail}");
}

void Info(string line) => Console.WriteLine($"INFO  {line}");

var cpus = Environment.ProcessorCount;
Info($"logical CPUs: {cpus}, runtime {Environment.Version}");

foreach (var project in new[] { "mode-collections/ModeCollections.csproj", "mode-all/ModeAll.csproj" })
{
    var build = Exec("dotnet", $"build \"{Path.Combine(root, project)}\" -c Release -v q --nologo", root);
    if (build.Code != 0)
    {
        Console.WriteLine(build.Output);
        Pass(false, $"{project} builds");
        return 1;
    }
}

Pass(true, "the attribute compiles against xunit.v3 4.0.2 and both assemblies build",
    "using Xunit.Sdk; using Xunit.v3; [assembly: Parallelization(Mode = ParallelMode.All)]");

// ---- timing ---------------------------------------------------------------------------

var awaitOld = Series("collections", "-class Demo.Timing.AwaitTests", "await", 5);
var awaitNew = Series("all", "-class Demo.Timing.AwaitTests", "await", 5);
Report("40 awaits of 100 ms, one class", awaitOld, awaitNew);
Pass(awaitOld.Median >= 3900, "collections: the 40 awaits run one after another, 4 seconds or more", $"median {awaitOld.Median:F0} ms");
Pass(awaitOld.Peak == 1, "collections: never more than 1 test of the class at once", $"peak {awaitOld.Peak}");
Pass(awaitNew.Peak >= 2, "all: tests of one class run at the same time", $"peak {awaitNew.Peak}");
Pass(awaitNew.Peak <= cpus, "all: under the default algorithm the peak stays at or below the logical CPU count", $"peak {awaitNew.Peak}, CPUs {cpus}");
if (cpus >= 4)
    Pass(awaitNew.Median < awaitOld.Median / 3, "all: the same 40 awaits take less than a third of the time", $"{awaitOld.Median:F0} ms -> {awaitNew.Median:F0} ms");

var sleepOld = Series("collections", "-class Demo.Timing.SleepTests", "sleep", 3);
var sleepNew = Series("all", "-class Demo.Timing.SleepTests", "sleep", 3);
Report("40 Thread.Sleep(100), one class", sleepOld, sleepNew);
if (cpus >= 4)
    Pass(sleepNew.Median < sleepOld.Median / 3, "all: Thread.Sleep tests gain too", $"{sleepOld.Median:F0} ms -> {sleepNew.Median:F0} ms");

var cpuOld = Series("collections", "-class Demo.Timing.CpuTests", "cpu", 3);
var cpuNew = Series("all", "-class Demo.Timing.CpuTests", "cpu", 3);
Report("40 CPU-bound tests, one class", cpuOld, cpuNew);
if (cpus >= 4)
    Pass(cpuNew.Median < cpuOld.Median / 1.5, "all: CPU-bound tests gain as well, when cores are free", $"{cpuOld.Median:F0} ms -> {cpuNew.Median:F0} ms");

var hashOld = Series("collections", "-class Demo.Timing.HashTests", "hash", 3);
var hashNew = Series("all", "-class Demo.Timing.HashTests", "hash", 3);
Report("40 SHA256 tests, one class (printed, not asserted)", hashOld, hashNew);

Info($"the same hashing in a plain Parallel.For, no xUnit: {HashLoop(1):F0} ms with 1 worker, {HashLoop(cpus):F0} ms with {cpus} workers (printed, not asserted)");

var manyOld = Series("collections", "-namespace Demo.ManyClasses", "many-classes", 3);
var manyNew = Series("all", "-namespace Demo.ManyClasses", "many-classes", 3);
Report("40 classes of one await each", manyOld, manyNew);
Pass(manyOld.Peak >= 2, "collections: separate classes already overlap", $"peak {manyOld.Peak}");
if (cpus >= 4)
    Pass(manyNew.Median > manyOld.Median * 0.5, "all: no real gain when the tests were already spread over classes", $"{manyOld.Median:F0} ms -> {manyNew.Median:F0} ms");

// ---- the thread cap --------------------------------------------------------------------

var two = Series("all", "-class Demo.Timing.AwaitTests -maxThreads 2", "await", 3);
var four = Series("all", "-class Demo.Timing.AwaitTests -maxThreads 4", "await", 3);
Info($"all, -maxThreads 2: median {two.Median:F0} ms, peak {two.Peak}");
Info($"all, -maxThreads 4: median {four.Median:F0} ms, peak {four.Peak}");
Pass(two.Peak == 2, "all with 2 threads: no more than 2 tests at once", $"peak {two.Peak}");
Pass(four.Peak == 4, "all with 4 threads: no more than 4 tests at once", $"peak {four.Peak}");
Pass(two.Median >= 1900, "all with 2 threads: 40 waits of 100 ms still need about 2 seconds", $"median {two.Median:F0} ms");
Pass(four.Median < two.Median * 0.75, "all with 4 threads is faster than with 2", $"{two.Median:F0} ms -> {four.Median:F0} ms");

var aggressive = Series("all", "-class Demo.Timing.AwaitTests -maxThreads 4 -parallelAlgorithm aggressive", "await", 3);
Info($"all, -maxThreads 4 -parallelAlgorithm aggressive: median {aggressive.Median:F0} ms, peak {aggressive.Peak}");
Pass(aggressive.Peak > 4, "the aggressive algorithm passes the thread limit for awaiting tests", $"peak {aggressive.Peak}");

var cli = Series("collections", "-class Demo.Timing.AwaitTests -parallelMode all", "await", 3);
Info($"the assembly without the attribute, run with -parallelMode all: median {cli.Median:F0} ms, peak {cli.Peak}");
Pass(cli.Peak >= 2, "the -parallelMode all switch turns it on with no code change", $"peak {cli.Peak}");

// ---- how fixtures behave ---------------------------------------------------------------

Pass(awaitNew.Last.GetProperty("fixtures").GetInt32() == 1, "all: one class fixture instance for the whole class", $"fixtures {awaitNew.Last.GetProperty("fixtures").GetInt32()}");
Pass(awaitNew.Last.GetProperty("testObjects").GetInt32() == 40, "all: the test class is still built once per test", $"objects {awaitNew.Last.GetProperty("testObjects").GetInt32()}");

Meet("-class Demo.Overlap.MeetTests", "meet", "first", "second", "two tests of one class");
Meet("-namespace Demo.Collections", "collection", "orders", "customers", "two classes sharing a collection fixture");

var serial = Once("all", "-namespace Demo.SerialCollection", "serial-collection");
Pass(serial.Group.GetProperty("notes").GetProperty("orders").GetString() == "False"
     && serial.Group.GetProperty("peak").GetInt32() == 1,
    "all: a collection marked DisableParallelization goes back to one at a time",
    $"peak {serial.Group.GetProperty("peak").GetInt32()}");

// ---- breaking it on purpose ------------------------------------------------------------

var plainOld = Once("collections", "-class Demo.Broken.PlainListTests", "plain-list");
var kept = plainOld.Group.GetProperty("notes").GetProperty("survived").GetString();
Pass(kept == "200000" && plainOld.Code == 0, "collections: the plain List<int> keeps all 200000 entries", $"survived {kept}");

Info("all: plain List<int>, five runs (a race: printed, never asserted)");
var plainRuns = new List<string>();
for (var i = 0; i < 5; i++)
{
    var run = Once("all", "-class Demo.Broken.PlainListTests", "plain-list");
    var notes = run.Group.GetProperty("notes");
    var thrown = string.Join(", ", notes.EnumerateObject().Where(n => n.Name.StartsWith("threw:")).Select(n => n.Name[6..]));
    var survived = notes.GetProperty("survived").GetString();
    plainRuns.Add($"survived {survived} of 200000, exit {run.Code}{(thrown.Length > 0 ? ", threw " + thrown : ", nothing thrown")}");
    Console.WriteLine($"      {plainRuns[^1]}");
}

var readerOld = Once("collections", "-class Demo.Broken.ReaderWriterTests", "reader-writer");
Pass(readerOld.Group.GetProperty("notes").GetProperty("reader").GetString() == "finished" && readerOld.Code == 0,
    "collections: a reader and a writer on the same list never collide");
var readerNew = Once("all", "-class Demo.Broken.ReaderWriterTests", "reader-writer");
var readerMessage = readerNew.Group.GetProperty("notes").GetProperty("reader").GetString();
Pass(readerMessage == "Collection was modified; enumeration operation may not execute." && readerNew.Code != 0,
    "all: the reader fails, every run, once the two are forced to overlap", readerMessage ?? "");

foreach (var (filter, group, label) in new[]
{
    ("Safe.LockedTests", "safe-lock", "a lock"),
    ("Safe.BagTests", "safe-bag", "ConcurrentBag<int>"),
    ("Safe.PerTestTests", "safe-per-test", "a list owned by each test"),
})
{
    var safe = Once("all", $"-class Demo.{filter}", group);
    var count = safe.Group.GetProperty("notes").GetProperty("survived").GetString();
    Pass(count == "200000" && safe.Code == 0, $"all: {label} keeps all 200000 entries", $"survived {count}");
}

// ---- opting out ------------------------------------------------------------------------

var wholeClass = Once("all", "-class Demo.OptOut.WholeClassTests", "opt-out-class");
Pass(wholeClass.Group.GetProperty("peak").GetInt32() == 1, "all: [TestClass(DisableParallelization = true)] runs that class one at a time", $"peak {wholeClass.Group.GetProperty("peak").GetInt32()}");
var oneMethod = Once("all", "-class Demo.OptOut.OneMethodTests", "opt-out-method");
Pass(oneMethod.Group.GetProperty("peak").GetInt32() == 1, "all: [Fact(DisableParallelization = true)] runs those methods one at a time", $"peak {oneMethod.Group.GetProperty("peak").GetInt32()}");

var probe = Exec("dotnet", $"build \"{Path.Combine(root, "opt-out-probe/OptOutProbe.csproj")}\" -c Release -v q --nologo", root);
var errors = Regex.Matches(probe.Output, @"Names\.cs\((\d+),\d+\): error (\w+): ([^\[\r\n]+)")
    .Select(m => (Line: int.Parse(m.Groups[1].Value), Code: m.Groups[2].Value, Text: m.Groups[3].Value.Trim()))
    .Distinct()
    .ToList();
var lines = File.ReadAllLines(Path.Combine(root, "opt-out-probe/Names.cs"));
bool Code(int n) => !lines[n - 1].TrimStart().StartsWith("//");
var wrongLines = Enumerable.Range(1, lines.Length).Where(n => Code(n) && lines[n - 1].Contains("DisableParallelism")).ToHashSet();
var rightLines = Enumerable.Range(1, lines.Length).Where(n => Code(n) && lines[n - 1].Contains("DisableParallelization")).ToHashSet();
Pass(errors.Select(e => e.Line).ToHashSet().SetEquals(wrongLines) && !errors.Any(e => rightLines.Contains(e.Line)),
    "DisableParallelism is a compile error on every attribute; DisableParallelization compiles on all of them",
    errors.Count > 0 ? $"{errors[0].Code}: {errors[0].Text}" : "");

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

// ---- helpers ---------------------------------------------------------------------------

void Report(string label, Stats before, Stats after)
{
    Info($"{label}: collections {before.Median:F0} ms (peak {before.Peak}), all {after.Median:F0} ms (peak {after.Peak})");
    Console.WriteLine($"      each run, ms: collections {Join(before.Runs)}, all {Join(after.Runs)}");
}

static double HashLoop(int workers)
{
    var clock = Stopwatch.StartNew();
    Parallel.For(0, 40, new ParallelOptions { MaxDegreeOfParallelism = workers }, _ =>
    {
        Span<byte> block = stackalloc byte[64];
        for (var i = 0; i < 200_000; i++)
            System.Security.Cryptography.SHA256.HashData(block, block[..32]);
    });
    return clock.Elapsed.TotalMilliseconds;
}

static string Join(IEnumerable<double> values) => "[" + string.Join(" ", values.Select(v => v.ToString("F0"))) + "]";

void Meet(string filter, string group, string a, string b, string label)
{
    var old = Once("collections", filter, group);
    var oldNotes = old.Group.GetProperty("notes");
    Pass(oldNotes.GetProperty(a).GetString() == "False" && oldNotes.GetProperty(b).GetString() == "False",
        $"collections: {label} never meet");

    var all = Once("all", filter, group);
    var allNotes = all.Group.GetProperty("notes");
    Pass(allNotes.GetProperty(a).GetString() == "True" && allNotes.GetProperty(b).GetString() == "True",
        $"all: {label} meet, so they overlap");
    Pass(all.Group.GetProperty("fixtures").GetInt32() == 1, $"all: {label} share one fixture instance");
}

Stats Series(string mode, string filter, string group, int count)
{
    var results = Enumerable.Range(0, count).Select(_ => Once(mode, filter, group)).ToList();
    var times = results.Select(r => r.Group.GetProperty("elapsedMs").GetDouble()).ToList();
    var sorted = times.Order().ToList();
    return new Stats(sorted[sorted.Count / 2], results.Max(r => r.Group.GetProperty("peak").GetInt32()), times, results[^1].Group);
}

Result Once(string mode, string filter, string group)
{
    var exe = Path.Combine(root, mode == "all" ? "mode-all" : "mode-collections", "bin", "Release", "net10.0", mode == "all" ? "ModeAll" : "ModeCollections");
    if (OperatingSystem.IsWindows()) exe += ".exe";

    var results = Path.Combine(outDir, $"r-{Guid.NewGuid():N}.json");
    var run = Exec(exe, $"{filter} -noColor", root, ("DEMO_RESULTS", results));
    if (!File.Exists(results))
        throw new InvalidOperationException($"no results from {mode} {filter}:\n{run.Output}");

    using var doc = JsonDocument.Parse(File.ReadAllText(results));
    File.Move(results, Path.Combine(outDir, "last.json"), overwrite: true);
    return new Result(doc.RootElement.GetProperty(group).Clone(), run.Code);
}

static (int Code, string Output) Exec(string file, string args, string workDir, params (string Name, string Value)[] env)
{
    var info = new ProcessStartInfo(file, args) { WorkingDirectory = workDir, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var (name, value) in env)
        info.Environment[name] = value;

    using var process = Process.Start(info)!;
    var stderr = process.StandardError.ReadToEndAsync();
    var stdout = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    return (process.ExitCode, stdout + stderr.Result);
}

static string FindRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("global.json not found above the check program");
}

record Result(JsonElement Group, int Code);

record Stats(double Median, int Peak, IReadOnlyList<double> Runs, JsonElement Last);
