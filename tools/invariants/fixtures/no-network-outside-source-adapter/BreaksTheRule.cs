// Leaves the server from a file that is not a source adapter.
namespace Fixture;

public sealed class BreaksTheRule
{
    public void Fetch() => _ = new HttpClient();
}
