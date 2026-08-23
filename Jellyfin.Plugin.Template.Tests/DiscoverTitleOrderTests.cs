using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.Template.Catalogue;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The order a shelf's titles come out in, and what it refuses to depend on.
/// </summary>
/// <remarks>
/// #91 is what these are for. The defect is a row that rearranges under a user
/// who is scrolling it, and it arrives whenever the sequence a source answered
/// in is what carries the order: a source ranked by anything popular answers
/// one query in two sequences within an hour.
///
/// So the assertions below are about a property rather than about a particular
/// list. What is proven is that the same set of titles comes out in the same
/// sequence whatever sequence it went in as, that no two distinct titles are
/// left for the sort to place, and that nothing outside the record is read.
///
/// What is not here is a refresh. Two refreshes of identical source data
/// producing identical order is #91's own third condition and it has no subject
/// yet: nothing in this tree runs a refresh, and nothing writes a title to the
/// catalogue. The half that catches the defect is the one below, because an
/// order that depends on arrival sequence fails on a shuffled response long
/// before a second refresh exists to notice.
/// </remarks>
public class DiscoverTitleOrderTests
{
    /// <summary>
    /// The instant these fixtures were fetched at.
    /// </summary>
    /// <remarks>
    /// Fixed rather than read from any clock, so a record built here carries
    /// the same age on every run. Nothing below asserts against it and nothing
    /// below sorts on it; the record refuses to be built without one.
    /// </remarks>
    private static readonly DateTimeOffset _fetched = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The shelf <see cref="APageWhereEveryKeyDecidesSomething"/> sorts to.
    /// </summary>
    private static readonly string[] _theShelf =
    {
        "Widely Seen",
        "Also Widely Seen",
        "Seen By Some",
        "Seen By Some",
        "Scored By Nobody"
    };

    /// <summary>
    /// The unscored title after the one scored zero.
    /// </summary>
    private static readonly string[] _zeroThenNothingSaid = { "Scored Zero", "Nothing Said" };

    /// <summary>
    /// The upper-case name first, which is what the code points say and not what a locale says.
    /// </summary>
    private static readonly string[] _byTheirBytes = { "Banana", "apple" };

    /// <summary>
    /// Every arrangement of one page of titles sorts to one shelf.
    /// </summary>
    /// <remarks>
    /// All one hundred and twenty arrangements of five titles rather than a
    /// handful of shuffles, because a handful is a sample and this is a
    /// property. The permutations are generated rather than drawn, so nothing
    /// here reaches for a random source, which
    /// <c>tools/invariants/rules/no-random.rule</c> refuses anyway.
    ///
    /// The five are built so that each key in the order decides at least one
    /// pair: two share a count, two of those share an average, two of those
    /// share a name, and one carries neither number at all.
    ///
    /// The identifiers are what is compared rather than the names, because two
    /// of the five carry one name. Comparing the names would call a shelf that
    /// had swapped exactly that pair unchanged, which is the pair the last key
    /// exists for.
    /// </remarks>
    [Fact]
    public void EveryArrangementOfOnePageSortsToOneShelf()
    {
        var page = APageWhereEveryKeyDecidesSomething();
        var expected = Identifiers(DiscoverTitleOrder.Sort(page));

        foreach (var arrangement in Arrangements(page))
        {
            Assert.Equal(expected, Identifiers(DiscoverTitleOrder.Sort(arrangement)));
        }
    }

    /// <summary>
    /// The shelf that comes out is the one this plugin decided rather than the one that arrived.
    /// </summary>
    /// <remarks>
    /// The assertion above proves every arrangement agrees. On its own that
    /// would also pass an order that agreed on the wrong thing, so the sequence
    /// is written out here once: most-scored first, then the better average,
    /// then the name, then the identifiers, and the title the source scored
    /// nothing for last.
    /// </remarks>
    [Fact]
    public void TheShelfIsTheOneTheKeysDescribe()
    {
        Assert.Equal(
            _theShelf,
            Names(DiscoverTitleOrder.Sort(APageWhereEveryKeyDecidesSomething())));

        var tied = DiscoverTitleOrder.Sort(APageWhereEveryKeyDecidesSomething());

        Assert.Equal("300003", tied[2].Identity.Primary.Value);
        Assert.Equal("300004", tied[3].Identity.Primary.Value);
    }

    /// <summary>
    /// Two titles the source scored alike are still put in an order, and it is the same order both ways round.
    /// </summary>
    /// <remarks>
    /// This is the near-miss for the last key rather than a restatement of the
    /// one above. A comparison that stops at the name calls these two equal,
    /// and a list of two elements that a sort is told are equal comes back in
    /// the sequence it went in, so the shelf follows arrival order for exactly
    /// the titles a ranked source is most likely to reorder.
    ///
    /// Watched failing rather than asserted: with the identity key removed from
    /// <c>StandingComparer.Compare</c>, this test reddens and the one above
    /// reddens with it, and both are quoted in the pull request that landed
    /// them.
    /// </remarks>
    [Fact]
    public void TwoTitlesAlikeInEverythingAUserSeesAreOrderedTheSameWayRoundEitherWay()
    {
        var first = ATitle("Indistinguishable", "300010", 900, 7.5);
        var second = ATitle("Indistinguishable", "300011", 900, 7.5);

        Assert.Equal(
            Identifiers(DiscoverTitleOrder.Sort(new[] { first, second })),
            Identifiers(DiscoverTitleOrder.Sort(new[] { second, first })));
    }

    /// <summary>
    /// A title the source scored nothing for sorts after every title it scored, and not as a zero.
    /// </summary>
    /// <remarks>
    /// The two are different answers and only one of them is right. A source
    /// that sent no count has said nothing about a title, and a source that
    /// sent zero has said nobody has scored it. Reading the first as the second
    /// would bury an announcement that has not been released yet under a film
    /// the source's audience actively disliked, and an unreleased title is
    /// exactly what a discover page exists to show.
    /// </remarks>
    [Fact]
    public void AnUnscoredTitleSortsAfterAScoreOfZeroRatherThanBesideIt()
    {
        var nothingSaid = ATitle("Nothing Said", "300020", null, null);
        var scoredZero = ATitle("Scored Zero", "300021", 0, 0);

        Assert.Equal(
            _zeroThenNothingSaid,
            Names(DiscoverTitleOrder.Sort(new[] { nothingSaid, scoredZero })));
    }

    /// <summary>
    /// Names are compared by their bytes, so the shelf does not depend on the server that sorted it.
    /// </summary>
    /// <remarks>
    /// A culture's collation puts <c>a</c> before <c>B</c> and the ordinal
    /// comparison puts <c>B</c> first, because the letter's code point is
    /// lower. The pair is chosen for exactly that disagreement: this assertion
    /// fails the moment the comparison is written as a culture-aware one, and
    /// it fails on a machine in any locale rather than only on one whose locale
    /// happens to differ.
    ///
    /// What is at stake is not which of the two is prettier. It is that a
    /// catalogue ordered on two servers has to come out in one order, and that
    /// a server whose locale is changed must not reorder every shelf on it
    /// without a refresh.
    /// </remarks>
    [Fact]
    public void NamesAreComparedByTheirBytesRatherThanByALocalesRules()
    {
        var lower = ATitle("apple", "300030", 10, 5);
        var upper = ATitle("Banana", "300031", 10, 5);

        Assert.Equal(
            _byTheirBytes,
            Names(DiscoverTitleOrder.Sort(new[] { lower, upper })));

        Assert.True(
            CultureInfo.CurrentCulture.CompareInfo.Compare("apple", "Banana") < 0,
            FormattableString.Invariant(
                $"The pair no longer disagrees under the culture this run used, '{CultureInfo.CurrentCulture.Name}', so this test would pass with a culture-aware comparison and proves nothing."));
    }

    /// <summary>
    /// Sorting a shelf that is already sorted changes nothing.
    /// </summary>
    /// <remarks>
    /// The property a stored shelf needs, which is not the same as the one
    /// above. A shelf read back from a catalogue and sorted again has to come
    /// out unmoved, or a page a user has open moves for the second reader of
    /// the same bytes.
    /// </remarks>
    [Fact]
    public void SortingASortedShelfMovesNothing()
    {
        var once = DiscoverTitleOrder.Sort(APageWhereEveryKeyDecidesSomething());
        var twice = DiscoverTitleOrder.Sort(once);

        Assert.Equal(Identifiers(once), Identifiers(twice));
    }

    /// <summary>
    /// The caller's own list is not rearranged underneath it.
    /// </summary>
    /// <remarks>
    /// What arrives is what a source answered, in the sequence it answered in,
    /// and a caller that still wants to know that sequence has not asked for it
    /// to be thrown away. A sort in place would take it, and the theft is
    /// invisible at the call site.
    ///
    /// A <see cref="List{T}"/> is handed in rather than an array, because that
    /// is the only shape the theft is available in: the cheap version of this
    /// method tests for one and sorts it where it lies. An array would pass
    /// this test against that version as well and prove nothing.
    /// </remarks>
    [Fact]
    public void TheListHandedInIsLeftAsItWas()
    {
        var page = new List<DiscoverTitle>(APageWhereEveryKeyDecidesSomething());
        var asItArrived = Identifiers(page);

        DiscoverTitleOrder.Sort(page);

        Assert.Equal(asItArrived, Identifiers(page));
    }

    /// <summary>
    /// Nothing to sort is a shelf with nothing on it rather than a failure.
    /// </summary>
    [Fact]
    public void AnEmptyPageSortsToAnEmptyShelf()
    {
        Assert.Empty(DiscoverTitleOrder.Sort(Array.Empty<DiscoverTitle>()));
    }

    /// <summary>
    /// A caller with no list at all is a caller with a defect.
    /// </summary>
    /// <remarks>
    /// Separate from the case above, because an empty answer from a source and
    /// a caller that never had a list are different things, and returning an
    /// empty shelf for the second hides it.
    /// </remarks>
    [Fact]
    public void NoListAtAllIsRefusedRatherThanReadAsAnEmptyOne()
    {
        Assert.Throws<ArgumentNullException>(() => DiscoverTitleOrder.Sort(null!));
    }

    /// <summary>
    /// Five titles arranged so that each key in the order decides at least one pair.
    /// </summary>
    /// <returns>The page, in a sequence that is not its sorted one.</returns>
    private static DiscoverTitle[] APageWhereEveryKeyDecidesSomething() =>
        new[]
        {
            ATitle("Seen By Some", "300004", 12, 8.1),
            ATitle("Scored By Nobody", "300005", null, null),
            ATitle("Also Widely Seen", "300002", 4000, 6.0),
            ATitle("Seen By Some", "300003", 12, 8.1),
            ATitle("Widely Seen", "300001", 4000, 7.2)
        };

    /// <summary>
    /// One title, built the way an adapter builds one out of a response.
    /// </summary>
    /// <param name="name">The name the source spells it with.</param>
    /// <param name="identifier">The source's identifier for it.</param>
    /// <param name="voteCount">How many scores the source's average is over, or null.</param>
    /// <param name="voteAverage">The source's average score, or null.</param>
    /// <returns>The record.</returns>
    private static DiscoverTitle ATitle(string name, string identifier, int? voteCount, double? voteAverage) =>
        new DiscoverTitle
        {
            Identity = new DiscoverTitleIdentity(
                new[] { new ProviderIdentifier(MetadataSource.Tmdb, identifier) }),
            Kind = DiscoverTitleKind.Movie,
            Name = name,
            FetchedAt = _fetched,
            VoteCount = voteCount,
            VoteAverage = voteAverage
        };

    /// <summary>
    /// The names of a shelf, in its order.
    /// </summary>
    /// <param name="shelf">The shelf.</param>
    /// <returns>The names.</returns>
    private static string[] Names(IEnumerable<DiscoverTitle> shelf) =>
        shelf.Select(title => title.Name).ToArray();

    /// <summary>
    /// The identifiers of a shelf, in its order.
    /// </summary>
    /// <param name="shelf">The shelf.</param>
    /// <returns>The identifiers, which are what tells two titles with one name apart.</returns>
    private static string[] Identifiers(IEnumerable<DiscoverTitle> shelf) =>
        shelf.Select(title => title.Identity.Primary.Value).ToArray();

    /// <summary>
    /// Every arrangement of a page.
    /// </summary>
    /// <param name="page">The titles.</param>
    /// <returns>Each arrangement of them, once.</returns>
    /// <remarks>
    /// Generated rather than drawn. A test that shuffled would need a random
    /// source, and this codebase refuses one outside
    /// <c>IRandomSource</c>; it would also prove the property for the
    /// arrangements it happened to draw rather than for all of them.
    /// </remarks>
    private static IEnumerable<DiscoverTitle[]> Arrangements(DiscoverTitle[] page)
    {
        if (page.Length <= 1)
        {
            yield return page;
            yield break;
        }

        for (var index = 0; index < page.Length; index++)
        {
            var head = page[index];
            var rest = page.Where((_, position) => position != index).ToArray();

            foreach (var arrangement in Arrangements(rest))
            {
                yield return new[] { head }.Concat(arrangement).ToArray();
            }
        }
    }
}
