namespace Jellyfin.Plugin.Template.Shelves;

/// <summary>
/// What a shelf asks for, as this plugin's own closed vocabulary rather than as
/// free text.
/// </summary>
/// <remarks>
/// A closed set is a decision this issue was asked to take rather than a
/// convenience. <see cref="Sources.SourceQuery"/> carries the question as a
/// string and says in its own words that the name is the shelf's vocabulary and
/// not the source's, and the one adapter answers a name it has no question for
/// as though it had not been set up. So a shelf built from free text reports a
/// mistyped question at fetch time, as a source that is not configured, which is
/// the failure #85's second condition exists against.
///
/// The two shapes that would repair it were a source that can be asked whether
/// it understands a name, and a shelf whose legal names are this plugin's own.
/// This is the second. It costs a source nothing, it needs no credential and no
/// request to decide, and an unknown question stops being a value that can be
/// built rather than one that is refused later.
///
/// What it does not decide is whether a source can answer a given question for a
/// given kind. That pair is still the source's to accept or decline, and nothing
/// here or on <see cref="Shelf"/> can settle it without asking.
///
/// The three members are the three `docs/shelves.md` argues for, and a fourth is
/// a question every present and future source has to answer or decline rather
/// than a row in a table. Adding one is therefore a change here and in every
/// adapter, which is the cost that page already states.
/// </remarks>
public enum ShelfQuestion
{
    /// <summary>
    /// No question. What an unset field reads as, and never valid on a shelf,
    /// which is why <see cref="Shelf.Validated"/> refuses it rather than
    /// letting an adapter choose one.
    /// </summary>
    None = 0,

    /// <summary>
    /// What is being watched elsewhere this week.
    /// </summary>
    Trending = 1,

    /// <summary>
    /// The steady baseline, which is what a first-run server shows when nothing
    /// is trending yet.
    /// </summary>
    Popular = 2,

    /// <summary>
    /// Older titles a server is likely to be missing, which the other two never
    /// surface.
    /// </summary>
    TopRated = 3
}
