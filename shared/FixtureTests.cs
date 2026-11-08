using System.Collections.Concurrent;

namespace Demo
{
    public static class Workload
    {
        public const int AddsPerTest = 5_000;
        public const int Tests = 40;

        public static IEnumerable<int> Rows => Enumerable.Range(0, Tests);

        /// <summary>Two tests meet here, or give up after the timeout. Meeting proves they overlapped.</summary>
        public static readonly TimeSpan MeetTimeout = TimeSpan.FromSeconds(3);
    }
}

// ---- proof of overlap: two tests must be inside the class at the same moment -----------

namespace Demo.Overlap
{
    public sealed class MeetingPoint : IDisposable
    {
        public Barrier Barrier { get; } = new(2);
        public void Dispose() => Barrier.Dispose();
    }

    public class MeetTests(MeetingPoint point) : IClassFixture<MeetingPoint>
    {
        private void Meet(string who)
        {
            using (Recorder.Enter("meet", this, point))
                Recorder.Note("meet", who, point.Barrier.SignalAndWait(Workload.MeetTimeout, TestContext.Current.CancellationToken));
        }

        [Fact] public void First() => Meet("first");
        [Fact] public void Second() => Meet("second");
    }
}

// ---- a collection fixture, shared by two classes ---------------------------------------

namespace Demo.Collections
{
    public sealed class SharedDatabase : IDisposable
    {
        public Barrier Barrier { get; } = new(2);
        public void Dispose() => Barrier.Dispose();
    }

    [CollectionDefinition("shared-database")]
    public class SharedDatabaseCollection : ICollectionFixture<SharedDatabase>;

    [Collection("shared-database")]
    public class OrdersTests(SharedDatabase db)
    {
        [Fact]
        public void Meet()
        {
            using (Recorder.Enter("collection", this, db))
                Recorder.Note("collection", "orders", db.Barrier.SignalAndWait(Workload.MeetTimeout, TestContext.Current.CancellationToken));
        }
    }

    [Collection("shared-database")]
    public class CustomersTests(SharedDatabase db)
    {
        [Fact]
        public void Meet()
        {
            using (Recorder.Enter("collection", this, db))
                Recorder.Note("collection", "customers", db.Barrier.SignalAndWait(Workload.MeetTimeout, TestContext.Current.CancellationToken));
        }
    }
}

// ---- the same pair of classes, with the collection opted out ----------------------------

namespace Demo.SerialCollection
{
    public sealed class SerialDatabase : IDisposable
    {
        public Barrier Barrier { get; } = new(2);
        public void Dispose() => Barrier.Dispose();
    }

    [CollectionDefinition("serial-database", DisableParallelization = true)]
    public class SerialDatabaseCollection : ICollectionFixture<SerialDatabase>;

    [Collection("serial-database")]
    public class OrdersTests(SerialDatabase db)
    {
        [Fact]
        public void Meet()
        {
            using (Recorder.Enter("serial-collection", this, db))
                Recorder.Note("serial-collection", "orders", db.Barrier.SignalAndWait(Workload.MeetTimeout, TestContext.Current.CancellationToken));
        }
    }

    [Collection("serial-database")]
    public class CustomersTests(SerialDatabase db)
    {
        [Fact]
        public void Meet()
        {
            using (Recorder.Enter("serial-collection", this, db))
                Recorder.Note("serial-collection", "customers", db.Barrier.SignalAndWait(Workload.MeetTimeout, TestContext.Current.CancellationToken));
        }
    }
}

// ---- breaking it on purpose ------------------------------------------------------------

namespace Demo.Broken
{
    public sealed class ListFixture : IDisposable
    {
        public List<int> Items { get; } = [];

        public void Dispose() => Recorder.Note("plain-list", "survived", Items.Count);
    }

    public class PlainListTests(ListFixture fixture) : IClassFixture<ListFixture>
    {
        public static TheoryData<int> Rows => new(Workload.Rows);

        [Theory]
        [MemberData(nameof(Rows))]
        public void Add_to_the_shared_list(int row)
        {
            using (Recorder.Enter("plain-list", this, fixture))
            {
                try
                {
                    for (var i = 0; i < Workload.AddsPerTest; i++)
                        fixture.Items.Add(row);
                }
                catch (Exception ex)
                {
                    Recorder.Note("plain-list", "threw:" + ex.GetType().Name, ex.Message);
                    throw;
                }
            }
        }
    }

    // A reader walks the list while a writer adds to it. The two meet first, so the
    // result doesn't depend on timing.
    public sealed class ReaderWriterFixture : IDisposable
    {
        public List<int> Items { get; } = [1, 2, 3];
        public Barrier Meet { get; } = new(2);
        public ManualResetEventSlim Written { get; } = new();

        public void Dispose()
        {
            Meet.Dispose();
            Written.Dispose();
        }
    }

    public class ReaderWriterTests(ReaderWriterFixture fixture) : IClassFixture<ReaderWriterFixture>
    {
        [Fact]
        public void Reader()
        {
            using (Recorder.Enter("reader-writer", this, fixture))
            {
                var seen = 0;
                try
                {
                    foreach (var item in fixture.Items)
                    {
                        if (seen++ == 0 && fixture.Meet.SignalAndWait(Workload.MeetTimeout, TestContext.Current.CancellationToken))
                            fixture.Written.Wait(Workload.MeetTimeout, TestContext.Current.CancellationToken);
                    }

                    Recorder.Note("reader-writer", "reader", "finished");
                }
                catch (InvalidOperationException ex)
                {
                    Recorder.Note("reader-writer", "reader", ex.Message);
                    throw;
                }
            }
        }

        [Fact]
        public void Writer()
        {
            using (Recorder.Enter("reader-writer", this, fixture))
            {
                if (fixture.Meet.SignalAndWait(Workload.MeetTimeout, TestContext.Current.CancellationToken))
                {
                    fixture.Items.Add(4);
                    fixture.Written.Set();
                }
            }
        }
    }
}

// ---- the same fixture exactly as the post shows it, without the recording -----------------

namespace Demo.PostSnippets
{
    public sealed class ListFixture
    {
        public List<int> Items { get; } = [];
    }

    public class PlainListTests(ListFixture fixture) : IClassFixture<ListFixture>
    {
        public static TheoryData<int> Rows => new(Enumerable.Range(0, 40));

        [Theory]
        [MemberData(nameof(Rows))]
        public void Add_to_the_shared_list(int row)
        {
            for (var i = 0; i < 5_000; i++)
                fixture.Items.Add(row);
        }
    }
}

// ---- three ways to make it safe -------------------------------------------------------

namespace Demo.Safe
{
    public sealed class LockedFixture : IDisposable
    {
        private readonly List<int> _items = [];
        private readonly Lock _gate = new();

        public void Add(int value)
        {
            lock (_gate)
                _items.Add(value);
        }

        public void Dispose() => Recorder.Note("safe-lock", "survived", _items.Count);
    }

    public class LockedTests(LockedFixture fixture) : IClassFixture<LockedFixture>
    {
        public static TheoryData<int> Rows => new(Workload.Rows);

        [Theory]
        [MemberData(nameof(Rows))]
        public void Add(int row)
        {
            using (Recorder.Enter("safe-lock", this, fixture))
                for (var i = 0; i < Workload.AddsPerTest; i++)
                    fixture.Add(row);
        }
    }

    public sealed class BagFixture : IDisposable
    {
        public ConcurrentBag<int> Items { get; } = [];

        public void Dispose() => Recorder.Note("safe-bag", "survived", Items.Count);
    }

    public class BagTests(BagFixture fixture) : IClassFixture<BagFixture>
    {
        public static TheoryData<int> Rows => new(Workload.Rows);

        [Theory]
        [MemberData(nameof(Rows))]
        public void Add(int row)
        {
            using (Recorder.Enter("safe-bag", this, fixture))
                for (var i = 0; i < Workload.AddsPerTest; i++)
                    fixture.Items.Add(row);
        }
    }

    public class PerTestTests
    {
        private static long _totalKept;

        // xUnit builds this class once per test, so this list has one owner.
        private readonly List<int> _items = [];

        public static TheoryData<int> Rows => new(Workload.Rows);

        [Theory]
        [MemberData(nameof(Rows))]
        public void Add(int row)
        {
            using (Recorder.Enter("safe-per-test", this))
            {
                for (var i = 0; i < Workload.AddsPerTest; i++)
                    _items.Add(row);

                Assert.Equal(Workload.AddsPerTest, _items.Count);
                Recorder.Note("safe-per-test", "survived", Interlocked.Add(ref _totalKept, _items.Count));
            }
        }
    }
}

// ---- opting a class out ---------------------------------------------------------------

namespace Demo.OptOut
{
    public sealed class OptOutFixture;

    [TestClass(DisableParallelization = true)]
    public class WholeClassTests(OptOutFixture fixture) : IClassFixture<OptOutFixture>
    {
        private async Task Run()
        {
            using (Recorder.Enter("opt-out-class", this, fixture))
                await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        [Fact] public Task A() => Run();
        [Fact] public Task B() => Run();
        [Fact] public Task C() => Run();
        [Fact] public Task D() => Run();
        [Fact] public Task E() => Run();
        [Fact] public Task F() => Run();
    }

    public class OneMethodTests(OptOutFixture fixture) : IClassFixture<OptOutFixture>
    {
        private async Task Run()
        {
            using (Recorder.Enter("opt-out-method", this, fixture))
                await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        [Fact(DisableParallelization = true)] public Task A() => Run();
        [Fact(DisableParallelization = true)] public Task B() => Run();
        [Fact(DisableParallelization = true)] public Task C() => Run();
        [Fact(DisableParallelization = true)] public Task D() => Run();
    }
}
