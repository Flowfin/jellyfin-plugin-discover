namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// What sort of thing a discover title is.
/// </summary>
/// <remarks>
/// The distinction a client draws differently and a requests plugin acts on
/// differently, so it is carried rather than inferred from whether a year or an
/// episode count happens to be present.
///
/// Two members and not more. A source's own type vocabulary is wider than this,
/// and mapping the wider one onto these two is the adapter's work in #74 rather
/// than something the record admits.
/// </remarks>
public enum DiscoverTitleKind
{
    /// <summary>
    /// No kind. What an unset field reads as, and refused by
    /// <see cref="DiscoverTitle.Kind"/> rather than stored.
    /// </summary>
    None = 0,

    /// <summary>
    /// A film.
    /// </summary>
    Movie = 1,

    /// <summary>
    /// A series, as the whole thing rather than as one of its episodes.
    /// </summary>
    Series = 2
}
