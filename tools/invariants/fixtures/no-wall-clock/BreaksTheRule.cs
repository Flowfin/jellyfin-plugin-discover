// Reads the wall clock where it stands instead of asking for the time.
namespace Fixture;

public sealed class BreaksTheRule
{
    public bool Expired(long stamp) => DateTime.UtcNow.Ticks > stamp;

    // The same read spelled as an elapsed interval, which is what a timeout or
    // a backoff is written with first.
    public long Elapsed()
    {
        var watch = Stopwatch.StartNew();
        return watch.ElapsedMilliseconds;
    }

    // The runtime's own answer to the question IClock already answers here.
    public DateTimeOffset Now() => TimeProvider.System.GetUtcNow();
}
