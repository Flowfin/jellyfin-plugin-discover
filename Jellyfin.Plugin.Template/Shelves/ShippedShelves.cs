using System.Collections.Generic;
using System.Collections.ObjectModel;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Shelves;

/// <summary>
/// The shelves a first install browses, as data rather than as a table in a
/// document.
/// </summary>
/// <remarks>
/// #86 is what this exists for, and it is that issue's fourth condition: the set
/// is data, per #85. Until this existed <c>docs/shelves.md</c> argued for six
/// rows and nothing in the tree held them, so a question added to
/// <see cref="ShelfQuestion"/> and not to the page, or the reverse, was caught
/// by a reader or not at all. The page says exactly that of itself, and this is
/// the half of it that can be held.
///
/// Three questions across the two kinds of title, which is six shelves. Why
/// those three and not others is the page's argument and is not restated here: a
/// name is a vocabulary every adapter is answerable to rather than a row in a
/// table, and these three are the ones the shipped adapter answers directly.
///
/// Nothing here decides how many titles a shelf may hold. That is #58, the bound
/// arrives as an argument rather than as a constant on this class, and
/// <see cref="Shelf.Cap"/> makes the same refusal for the same reason: a number
/// written here would be this issue quietly answering that one.
///
/// Nothing here reads or writes anything either. A set that fetched, stored or
/// drew would be the second place a shelf is decided, and <see cref="Shelf"/> is
/// the first.
/// </remarks>
public static class ShippedShelves
{
    /// <summary>
    /// The set every install starts with, in the order a discover page draws it.
    /// </summary>
    /// <param name="cap">The most titles each shelf may hold, which is #58's number.</param>
    /// <returns>The six shelves, already validated.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown by <see cref="Shelf.Validated"/> when <paramref name="cap"/> is
    /// zero or negative, because a shelf that may hold nothing is a row that is
    /// empty for a reason no operator can see.
    /// </exception>
    /// <remarks>
    /// The order is this list's, which is #54's first condition read from the
    /// other side: the top level is in an order the plugin controls rather than
    /// one a client happens to apply. Grouped by question rather than by kind,
    /// so the two halves of one theme sit beside each other on a remote.
    ///
    /// A new list on every call rather than a cached one. The cap is a
    /// configured value, so a set built once would be the set built against
    /// whatever the cap was the first time anybody asked, which is the shape a
    /// stale bound takes.
    ///
    /// Every shelf names <see cref="MetadataSource.Tmdb"/> because it is the one
    /// source with an adapter. A second source does not redistribute these six:
    /// it is a decision about which shelf asks whom, and it belongs where the
    /// set is stated rather than where the source is added.
    ///
    /// Each is validated here rather than by a caller, so a set this plugin
    /// ships can never be one <see cref="Shelf.Validated"/> would refuse.
    /// </remarks>
    public static IReadOnlyList<Shelf> Bounded(int cap) => new ReadOnlyCollection<Shelf>(
        new Shelf[]
        {
            Row("Trending films", ShelfQuestion.Trending, DiscoverTitleKind.Movie, cap),
            Row("Trending series", ShelfQuestion.Trending, DiscoverTitleKind.Series, cap),
            Row("Popular films", ShelfQuestion.Popular, DiscoverTitleKind.Movie, cap),
            Row("Popular series", ShelfQuestion.Popular, DiscoverTitleKind.Series, cap),
            Row("Top-rated films", ShelfQuestion.TopRated, DiscoverTitleKind.Movie, cap),
            Row("Top-rated series", ShelfQuestion.TopRated, DiscoverTitleKind.Series, cap)
        });

    /// <summary>
    /// One row of the shipped set.
    /// </summary>
    /// <param name="displayName">What an operator and a client see this row called.</param>
    /// <param name="question">What the shelf asks for.</param>
    /// <param name="kind">Which sort of title it holds.</param>
    /// <param name="cap">The most titles it may hold.</param>
    /// <returns>The shelf, already validated.</returns>
    /// <remarks>
    /// The name is carried rather than composed out of the question and the
    /// kind, which <see cref="Shelf.DisplayName"/> refuses for two reasons this
    /// set would have met immediately: a concatenation is a string a later
    /// reader has to take apart again, and it fixes an English word order on a
    /// name that is not this plugin's to translate.
    /// </remarks>
    private static Shelf Row(string displayName, ShelfQuestion question, DiscoverTitleKind kind, int cap) =>
        new Shelf
        {
            DisplayName = displayName,
            Question = question,
            Kind = kind,
            Source = MetadataSource.Tmdb,
            Cap = cap
        }.Validated();
}
