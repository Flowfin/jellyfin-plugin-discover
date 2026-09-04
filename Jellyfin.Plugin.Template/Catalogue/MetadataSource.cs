namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// A body that issues identifiers for titles, named in this plugin's own
/// vocabulary rather than in any source's.
/// </summary>
/// <remarks>
/// A closed set rather than the free string a source's response carries. An
/// identity keyed by whatever text arrived would let one misspelling split a
/// title into two records that never compare equal again, and nothing later in
/// the plugin could tell that apart from two genuinely different titles. Adding
/// a source is therefore a change here and a position in
/// <see cref="DiscoverTitleIdentity.Precedence"/>, which is a decision somebody
/// makes rather than a value that arrives.
///
/// The set is not the set of sources this plugin fetches from. It is the set of
/// identifier bodies a fetched response may name, which is wider: the first
/// adapter is #74 and TheTVDB was refused as a source on #83, but a response
/// from either commonly carries an IMDb identifier for the same title.
/// </remarks>
public enum MetadataSource
{
    /// <summary>
    /// No source. What an uninitialised field reads as, and never valid inside
    /// an identity, which is why <see cref="DiscoverTitleIdentity"/> refuses it
    /// rather than storing it.
    /// </summary>
    None = 0,

    /// <summary>
    /// IMDb, whose identifiers are the ones the other sources here also carry.
    /// </summary>
    Imdb = 1,

    /// <summary>
    /// The Movie Database, the source #74 puts behind the first adapter.
    /// </summary>
    Tmdb = 2,

    /// <summary>
    /// TheTVDB, which #83 refused as a source of this plugin and which is
    /// declared here anyway, so that a response naming one of its identifiers
    /// has somewhere to put it. Carrying an identifier and asking a source for
    /// something are different acts, and only the second one was refused.
    /// </summary>
    Tvdb = 3
}
