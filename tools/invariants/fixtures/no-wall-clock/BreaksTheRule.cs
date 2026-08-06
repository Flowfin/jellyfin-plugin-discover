// Reads the wall clock where it stands instead of asking for the time.
namespace Fixture;

public sealed class BreaksTheRule
{
    public bool Expired(long stamp) => DateTime.UtcNow.Ticks > stamp;
}
