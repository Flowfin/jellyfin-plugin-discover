// Makes its own randomness instead of asking for it.
namespace Fixture;

public sealed class BreaksTheRule
{
    public int Jitter() => new Random().Next(1000);
}
