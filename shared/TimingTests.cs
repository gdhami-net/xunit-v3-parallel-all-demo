using System.Security.Cryptography;

namespace Demo.Timing;

// Forty tests in one class, several kinds of slow. The class fixture is shared by
// the forty; the constructor runs once per test. Recorder counts both.

public sealed class TimingFixture;

public class AwaitTests(TimingFixture fixture) : IClassFixture<TimingFixture>
{
    private async Task Run()
    {
        using (Recorder.Enter("await", this, fixture))
            await Task.Delay(100, TestContext.Current.CancellationToken);
    }

    [Fact] public Task T01() => Run();
    [Fact] public Task T02() => Run();
    [Fact] public Task T03() => Run();
    [Fact] public Task T04() => Run();
    [Fact] public Task T05() => Run();
    [Fact] public Task T06() => Run();
    [Fact] public Task T07() => Run();
    [Fact] public Task T08() => Run();
    [Fact] public Task T09() => Run();
    [Fact] public Task T10() => Run();
    [Fact] public Task T11() => Run();
    [Fact] public Task T12() => Run();
    [Fact] public Task T13() => Run();
    [Fact] public Task T14() => Run();
    [Fact] public Task T15() => Run();
    [Fact] public Task T16() => Run();
    [Fact] public Task T17() => Run();
    [Fact] public Task T18() => Run();
    [Fact] public Task T19() => Run();
    [Fact] public Task T20() => Run();
    [Fact] public Task T21() => Run();
    [Fact] public Task T22() => Run();
    [Fact] public Task T23() => Run();
    [Fact] public Task T24() => Run();
    [Fact] public Task T25() => Run();
    [Fact] public Task T26() => Run();
    [Fact] public Task T27() => Run();
    [Fact] public Task T28() => Run();
    [Fact] public Task T29() => Run();
    [Fact] public Task T30() => Run();
    [Fact] public Task T31() => Run();
    [Fact] public Task T32() => Run();
    [Fact] public Task T33() => Run();
    [Fact] public Task T34() => Run();
    [Fact] public Task T35() => Run();
    [Fact] public Task T36() => Run();
    [Fact] public Task T37() => Run();
    [Fact] public Task T38() => Run();
    [Fact] public Task T39() => Run();
    [Fact] public Task T40() => Run();
}

public class SleepTests(TimingFixture fixture) : IClassFixture<TimingFixture>
{
    private void Run()
    {
        using (Recorder.Enter("sleep", this, fixture))
            Thread.Sleep(100);
    }

    [Fact] public void T01() => Run();
    [Fact] public void T02() => Run();
    [Fact] public void T03() => Run();
    [Fact] public void T04() => Run();
    [Fact] public void T05() => Run();
    [Fact] public void T06() => Run();
    [Fact] public void T07() => Run();
    [Fact] public void T08() => Run();
    [Fact] public void T09() => Run();
    [Fact] public void T10() => Run();
    [Fact] public void T11() => Run();
    [Fact] public void T12() => Run();
    [Fact] public void T13() => Run();
    [Fact] public void T14() => Run();
    [Fact] public void T15() => Run();
    [Fact] public void T16() => Run();
    [Fact] public void T17() => Run();
    [Fact] public void T18() => Run();
    [Fact] public void T19() => Run();
    [Fact] public void T20() => Run();
    [Fact] public void T21() => Run();
    [Fact] public void T22() => Run();
    [Fact] public void T23() => Run();
    [Fact] public void T24() => Run();
    [Fact] public void T25() => Run();
    [Fact] public void T26() => Run();
    [Fact] public void T27() => Run();
    [Fact] public void T28() => Run();
    [Fact] public void T29() => Run();
    [Fact] public void T30() => Run();
    [Fact] public void T31() => Run();
    [Fact] public void T32() => Run();
    [Fact] public void T33() => Run();
    [Fact] public void T34() => Run();
    [Fact] public void T35() => Run();
    [Fact] public void T36() => Run();
    [Fact] public void T37() => Run();
    [Fact] public void T38() => Run();
    [Fact] public void T39() => Run();
    [Fact] public void T40() => Run();
}

public class CpuTests(TimingFixture fixture) : IClassFixture<TimingFixture>
{
    // A fixed amount of arithmetic, about 100 ms on one core of the machine in the README.
    private const long Rounds = 100_000_000;
    private static ulong _sink;

    private void Run()
    {
        using (Recorder.Enter("cpu", this, fixture))
        {
            var x = 88172645463325252UL;
            for (long i = 0; i < Rounds; i++)
            {
                x ^= x << 13;
                x ^= x >> 7;
                x ^= x << 17;
            }

            Interlocked.Exchange(ref _sink, x);
        }
    }

    [Fact] public void T01() => Run();
    [Fact] public void T02() => Run();
    [Fact] public void T03() => Run();
    [Fact] public void T04() => Run();
    [Fact] public void T05() => Run();
    [Fact] public void T06() => Run();
    [Fact] public void T07() => Run();
    [Fact] public void T08() => Run();
    [Fact] public void T09() => Run();
    [Fact] public void T10() => Run();
    [Fact] public void T11() => Run();
    [Fact] public void T12() => Run();
    [Fact] public void T13() => Run();
    [Fact] public void T14() => Run();
    [Fact] public void T15() => Run();
    [Fact] public void T16() => Run();
    [Fact] public void T17() => Run();
    [Fact] public void T18() => Run();
    [Fact] public void T19() => Run();
    [Fact] public void T20() => Run();
    [Fact] public void T21() => Run();
    [Fact] public void T22() => Run();
    [Fact] public void T23() => Run();
    [Fact] public void T24() => Run();
    [Fact] public void T25() => Run();
    [Fact] public void T26() => Run();
    [Fact] public void T27() => Run();
    [Fact] public void T28() => Run();
    [Fact] public void T29() => Run();
    [Fact] public void T30() => Run();
    [Fact] public void T31() => Run();
    [Fact] public void T32() => Run();
    [Fact] public void T33() => Run();
    [Fact] public void T34() => Run();
    [Fact] public void T35() => Run();
    [Fact] public void T36() => Run();
    [Fact] public void T37() => Run();
    [Fact] public void T38() => Run();
    [Fact] public void T39() => Run();
    [Fact] public void T40() => Run();
}

// Kept because it surprised me: hashing with SHA256 didn't get faster with more threads on
// Windows, in this class or in a plain Parallel.For. The check prints it and asserts nothing.
public class HashTests(TimingFixture fixture) : IClassFixture<TimingFixture>
{
    private const int Rounds = 200_000;

    private void Run()
    {
        using (Recorder.Enter("hash", this, fixture))
        {
            Span<byte> block = stackalloc byte[64];
            for (var i = 0; i < Rounds; i++)
                SHA256.HashData(block, block[..32]);
        }
    }

    [Fact] public void T01() => Run();
    [Fact] public void T02() => Run();
    [Fact] public void T03() => Run();
    [Fact] public void T04() => Run();
    [Fact] public void T05() => Run();
    [Fact] public void T06() => Run();
    [Fact] public void T07() => Run();
    [Fact] public void T08() => Run();
    [Fact] public void T09() => Run();
    [Fact] public void T10() => Run();
    [Fact] public void T11() => Run();
    [Fact] public void T12() => Run();
    [Fact] public void T13() => Run();
    [Fact] public void T14() => Run();
    [Fact] public void T15() => Run();
    [Fact] public void T16() => Run();
    [Fact] public void T17() => Run();
    [Fact] public void T18() => Run();
    [Fact] public void T19() => Run();
    [Fact] public void T20() => Run();
    [Fact] public void T21() => Run();
    [Fact] public void T22() => Run();
    [Fact] public void T23() => Run();
    [Fact] public void T24() => Run();
    [Fact] public void T25() => Run();
    [Fact] public void T26() => Run();
    [Fact] public void T27() => Run();
    [Fact] public void T28() => Run();
    [Fact] public void T29() => Run();
    [Fact] public void T30() => Run();
    [Fact] public void T31() => Run();
    [Fact] public void T32() => Run();
    [Fact] public void T33() => Run();
    [Fact] public void T34() => Run();
    [Fact] public void T35() => Run();
    [Fact] public void T36() => Run();
    [Fact] public void T37() => Run();
    [Fact] public void T38() => Run();
    [Fact] public void T39() => Run();
    [Fact] public void T40() => Run();
}
