// A file outside the surface adapter that speaks the server's entity
// vocabulary rather than its channel vocabulary. The two halves of the rule are
// broken separately, so a pattern that lost the second half would still fire on
// the first fixture and leg 1 would stay green.
// Not compiled: tools/ is outside every project in the solution.
using MediaBrowser.Controller.Entities;

namespace Fixture;

public sealed class AlsoBreaksTheRule
{
    public BaseItem Item { get; set; }

    public string Key => MetadataProvider.Tmdb.ToString();
}
