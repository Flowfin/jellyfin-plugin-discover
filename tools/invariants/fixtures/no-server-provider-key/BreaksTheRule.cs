// Takes the server's own metadata provider key, in all three spellings the rule
// refuses. The hexadecimal string below is invented for this fixture and is not
// anybody's key.
namespace Fixture;

using MediaBrowser.Providers.Plugins.Tmdb;

public sealed class BreaksTheRule
{
    public string FromTheServersType() => TmdbUtils.ApiKey;

    public string FromTheValueItself() => "0123456789abcdef0123456789abcdef";
}
