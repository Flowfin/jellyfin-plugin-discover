using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// The order titles are put in before anybody sees them, decided here rather
/// than by the sequence a source answered in.
/// </summary>
/// <remarks>
/// #91 is what this exists for. A source ranked by anything popular answers the
/// same query in a different sequence from one hour to the next, so a shelf that
/// kept arrival order rearranges under a user who is scrolling it, and every
/// refresh looks like a change to the server's cache and to any client drawing
/// the row. What stops that is a sort key that travels with the title instead of
/// with its position.
///
/// Which order a given shelf uses is not decided here. That belongs with the
/// record that describes a shelf, #85, and with the shelves that ship, #86. What
/// is decided here is that an order exists, that it reads only fields on the
/// record, and that it is total: no two distinct titles are left to the sort's
/// own choice, because two titles a comparison calls equal are ordered by
/// whichever of them the sort met first, which is arrival order arriving through
/// the back door.
/// </remarks>
public static class DiscoverTitleOrder
{
    /// <summary>
    /// Gets the order a shelf uses where nothing says otherwise: what the
    /// source's own audience thought of a title, most-scored first.
    /// </summary>
    /// <remarks>
    /// Four keys, in order, and each one is on the record.
    ///
    /// <see cref="DiscoverTitle.VoteCount"/> descending first, because it is
    /// the one that moves slowest and means the plainest thing. An average
    /// alone puts a title three people scored ten above one a hundred thousand
    /// people scored nine, which is not a shelf anybody wanted.
    ///
    /// <see cref="DiscoverTitle.VoteAverage"/> descending second, which
    /// separates titles the same number of people scored.
    ///
    /// <see cref="DiscoverTitle.Name"/> ascending third, ordinal. Ordinal
    /// rather than a culture's collation, because a collation is a property of
    /// the server that ran the sort: the same catalogue ordered on two servers
    /// would come out in two orders, and a server whose locale is changed
    /// reorders every shelf on it without a refresh. What a client does with
    /// the row it is given is the client's.
    ///
    /// The identifiers last, which is what makes the order total. Two titles
    /// with the same counts and the same name are still two titles, and they
    /// have different identifiers or they would be one record.
    ///
    /// A source's own composite ranking is not among the keys and is not on the
    /// record. TMDB documents one, <c>popularity</c>, on every address this
    /// plugin asks, and it is the number that moves daily and by a formula
    /// nobody outside the source can state. Sorting on it produces exactly the
    /// row that rearranges under a user, for reasons neither the user nor this
    /// plugin can see, which is what #91 exists against. A shelf whose whole
    /// premise is that ranking is a case for #86 to argue, and the field can be
    /// added to the record then: nothing has been published, so adding one
    /// costs no migration until the first release.
    /// </remarks>
    public static IComparer<DiscoverTitle> ByStanding { get; } = new StandingComparer();

    /// <summary>
    /// Puts a list of titles in <see cref="ByStanding"/> order.
    /// </summary>
    /// <param name="titles">The titles, in whatever sequence they arrived in.</param>
    /// <returns>The same titles, in this plugin's order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="titles"/> is null.</exception>
    /// <remarks>
    /// A copy rather than a sort in place, because what arrives here is what a
    /// source answered and a caller holding that list did not ask for it to be
    /// rearranged underneath them.
    /// </remarks>
    public static IReadOnlyList<DiscoverTitle> Sort(IEnumerable<DiscoverTitle> titles)
    {
        ArgumentNullException.ThrowIfNull(titles);

        var ordered = new List<DiscoverTitle>(titles);
        ordered.Sort(ByStanding);
        return ordered;
    }

    /// <summary>
    /// The comparison behind <see cref="ByStanding"/>.
    /// </summary>
    private sealed class StandingComparer : IComparer<DiscoverTitle>
    {
        /// <inheritdoc/>
        public int Compare(DiscoverTitle? x, DiscoverTitle? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            var byCount = Descending(x.VoteCount, y.VoteCount);

            if (byCount != 0)
            {
                return byCount;
            }

            var byAverage = Descending(x.VoteAverage, y.VoteAverage);

            if (byAverage != 0)
            {
                return byAverage;
            }

            var byName = string.CompareOrdinal(x.Name, y.Name);

            if (byName != 0)
            {
                return byName;
            }

            return CompareIdentity(x.Identity, y.Identity);
        }

        /// <summary>
        /// Orders a number a source may not have sent, larger first.
        /// </summary>
        /// <typeparam name="T">The number's type.</typeparam>
        /// <param name="left">The first title's value, or null where the source sent none.</param>
        /// <param name="right">The second title's value, or null where the source sent none.</param>
        /// <returns>The comparison, with absence after every value.</returns>
        /// <remarks>
        /// A title the source scored nothing for sorts after every title it
        /// scored, and after nothing else. It is not a zero: a zero is a title
        /// the source says nobody rated, which is a statement, and burying an
        /// unscored title below one that was rated badly would be reading an
        /// absence as the worst possible answer.
        /// </remarks>
        private static int Descending<T>(T? left, T? right)
            where T : struct, IComparable<T>
        {
            if (left is { } first)
            {
                return right is { } second ? second.CompareTo(first) : -1;
            }

            return right is null ? 0 : 1;
        }

        /// <summary>
        /// Orders two identities, so that two titles alike in everything a user
        /// sees still come out in the same order on every run.
        /// </summary>
        /// <param name="left">The first title's identity.</param>
        /// <param name="right">The second title's identity.</param>
        /// <returns>The comparison.</returns>
        /// <remarks>
        /// <see cref="DiscoverTitleIdentity.Identifiers"/> is ordered by source
        /// by that type rather than by the response, so this reads a sequence
        /// that does not depend on how a source listed them.
        ///
        /// The bound is worth stating because it is the one place this order is
        /// not fixed by the record alone: an identity that later gains an
        /// identifier compares differently from the one that was stored without
        /// it. Two titles that were tied on everything above therefore swap
        /// when one of them gains an identifier. That is a title whose identity
        /// changed rather than a shelf shuffling on its own, and there is no
        /// key that is both total and blind to it.
        /// </remarks>
        private static int CompareIdentity(DiscoverTitleIdentity left, DiscoverTitleIdentity right)
        {
            var mine = left.Identifiers;
            var theirs = right.Identifiers;
            var shared = mine.Count < theirs.Count ? mine.Count : theirs.Count;

            for (var index = 0; index < shared; index++)
            {
                var bySource = mine[index].Source.CompareTo(theirs[index].Source);

                if (bySource != 0)
                {
                    return bySource;
                }

                var byValue = string.CompareOrdinal(mine[index].Value, theirs[index].Value);

                if (byValue != 0)
                {
                    return byValue;
                }
            }

            return mine.Count.CompareTo(theirs.Count);
        }
    }
}
