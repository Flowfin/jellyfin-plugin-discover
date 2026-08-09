// Reaches the sibling's own types instead of the interface this plugin declares.
namespace Fixture;

using Jellyfin.Plugin.Requests;

public sealed class BreaksTheRule
{
    public void Hand(RequestRecord record) => _ = record;
}
