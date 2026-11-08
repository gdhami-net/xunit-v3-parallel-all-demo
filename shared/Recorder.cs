using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

[assembly: AssemblyFixture(typeof(Demo.ResultsWriter))]

namespace Demo;

/// <summary>Counts what happened per named group of tests, so the check can read it afterwards.</summary>
public static class Recorder
{
    public sealed class Group
    {
        public int InFlight, Peak, Runs;
        public long FirstStart = long.MaxValue, LastEnd;
        public readonly ConcurrentDictionary<object, byte> TestObjects = new(ReferenceEqualityComparer.Instance);
        public readonly ConcurrentDictionary<object, byte> Fixtures = new(ReferenceEqualityComparer.Instance);
        public readonly ConcurrentDictionary<string, string> Notes = new();
    }

    private static readonly ConcurrentDictionary<string, Group> Groups = new();

    public readonly struct Scope(string name, Group group) : IDisposable
    {
        private readonly Group _group = group;
        public string Name { get; } = name;

        public void Dispose()
        {
            Interlocked.Decrement(ref _group.InFlight);
            UpdateMax(ref _group.LastEnd, Stopwatch.GetTimestamp());
        }
    }

    public static Scope Enter(string name, object testObject, object? fixture = null)
    {
        var group = Groups.GetOrAdd(name, _ => new Group());
        group.TestObjects.TryAdd(testObject, 0);
        if (fixture is not null)
            group.Fixtures.TryAdd(fixture, 0);

        Interlocked.Increment(ref group.Runs);
        UpdateMin(ref group.FirstStart, Stopwatch.GetTimestamp());
        UpdateMax(ref group.Peak, Interlocked.Increment(ref group.InFlight));
        return new Scope(name, group);
    }

    public static void Note(string name, string key, object? value) =>
        Groups.GetOrAdd(name, _ => new Group()).Notes[key] = value?.ToString() ?? "";

    public static string ToJson()
    {
        var report = Groups.ToDictionary(g => g.Key, g => new
        {
            runs = g.Value.Runs,
            peak = g.Value.Peak,
            testObjects = g.Value.TestObjects.Count,
            fixtures = g.Value.Fixtures.Count,
            elapsedMs = g.Value.LastEnd == 0
                ? 0
                : Stopwatch.GetElapsedTime(g.Value.FirstStart, g.Value.LastEnd).TotalMilliseconds,
            notes = g.Value.Notes,
        });
        return JsonSerializer.Serialize(report);
    }

    private static void UpdateMax(ref int target, int value)
    {
        int seen;
        while (value > (seen = Volatile.Read(ref target)))
            if (Interlocked.CompareExchange(ref target, value, seen) == seen)
                return;
    }

    private static void UpdateMax(ref long target, long value)
    {
        long seen;
        while (value > (seen = Volatile.Read(ref target)))
            if (Interlocked.CompareExchange(ref target, value, seen) == seen)
                return;
    }

    private static void UpdateMin(ref long target, long value)
    {
        long seen;
        while (value < (seen = Volatile.Read(ref target)))
            if (Interlocked.CompareExchange(ref target, value, seen) == seen)
                return;
    }
}

/// <summary>Writes the recorded results when the assembly finishes, if the check asked for them.</summary>
public sealed class ResultsWriter : IDisposable
{
    public void Dispose()
    {
        var path = Environment.GetEnvironmentVariable("DEMO_RESULTS");
        if (!string.IsNullOrEmpty(path))
            File.WriteAllText(path, Recorder.ToJson());
    }
}
