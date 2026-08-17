// Makes its own randomness instead of asking for it.
namespace Fixture;

public sealed class BreaksTheRule
{
    public int Jitter() => new Random().Next(1000);

    // The same draw one method along, on the framework this project builds on.
    public Guid Identifier() => Guid.CreateVersion7();

    // An unpredictable answer wearing a filename.
    public string Scratch() => Path.GetRandomFileName();
}
