using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Jellyfin.Plugin.Template.Tests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The record a shelf is, and what it refuses to be built as.
/// </summary>
/// <remarks>
/// #85 is what these are for. The defect the record is written against is a
/// shelf that is code, so what is asserted below is that a shelf is buildable
/// as data, that the one question it stands for is composed in one place out of
/// its own fields, and that the shapes which cannot be asked for anything are
/// refused rather than stored.
///
/// Two of #85's conditions have no subject here and are not pretended at, and
/// the reason given for the first of them has stopped being true. It said
/// nothing in this tree fetches, so a shelf that is off being neither fetched
/// nor stored nor shown is a property with nothing to count calls on. The
/// refresh landed under #87 on 2026-08-30 and counts those calls, in
/// <c>CatalogueRefreshTests.AShelfThatIsTurnedOffIsNotAskedAndNotStored</c>.
/// The property is still not asserted here, for a reason that outlives the
/// one that was withdrawn: it is a property of what reads the flag rather than
/// of the record that carries it, and what this suite holds is that the flag
/// is data a shelf can be built with. Not shown is the third of the three and
/// has no reader anywhere yet. The other condition is the save path an unknown
/// pair would be refused at, which is #105 and does not exist. Both are
/// recorded on the issue.
/// </remarks>
public class ShelfTests
{
    /// <summary>
    /// The instant the clock the adapters below read.
    /// </summary>
    /// <remarks>
    /// Fixed rather than read from the machine. Nothing here asserts against
    /// it; the adapter refuses to stamp a record without one.
    /// </remarks>
    private static readonly DateTimeOffset _fetched = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Gets every question this plugin carries, apart from the unset member,
    /// against both kinds of title.
    /// </summary>
    public static TheoryData<ShelfQuestion, DiscoverTitleKind> EveryQuestionAndKind
    {
        get
        {
            var data = new TheoryData<ShelfQuestion, DiscoverTitleKind>();

            foreach (var question in Enum.GetValues<ShelfQuestion>().Where(q => q != ShelfQuestion.None))
            {
                data.Add(question, DiscoverTitleKind.Movie);
                data.Add(question, DiscoverTitleKind.Series);
            }

            return data;
        }
    }

    /// <summary>
    /// A shelf is buildable as data, with no code written for it.
    /// </summary>
    /// <remarks>
    /// #85's fifth condition asks that adding a shelf change no code outside
    /// its definition, proven by a test that adds one. This is that test: the
    /// shelf below exists nowhere in the plugin, it names a question and a kind
    /// the plugin already carries, and it produces a usable question without
    /// anything in <c>Jellyfin.Plugin.Template</c> having been told about it.
    ///
    /// The bound on what that proves is worth stating rather than leaving to be
    /// discovered. Nothing consumes a shelf yet, so what is shown is that the
    /// record admits a new instance, not that a surface and a refresh draw one
    /// without a case being added for it. That half arrives with #87 and #86.
    /// </remarks>
    [Fact]
    public void AShelfNobodyWroteCodeForIsStillAShelf()
    {
        var invented = new Shelf
        {
            DisplayName = "A row that exists only in this test",
            Question = ShelfQuestion.TopRated,
            Kind = DiscoverTitleKind.Series,
            Source = MetadataSource.Tmdb,
            Cap = 7
        };

        var asked = invented.Ask();

        Assert.Equal("top-rated", asked.Name);
        Assert.Equal(DiscoverTitleKind.Series, asked.Kind);
        Assert.Equal(7, asked.Limit);
    }

    /// <summary>
    /// The question a shelf asks carries the shelf's own cap.
    /// </summary>
    /// <remarks>
    /// #85's third condition asks that the cap live on the record so that no
    /// other component holds a second copy. What holds it is that the query is
    /// composed here: a refresh building its own would carry whatever limit it
    /// decided, and the drift would show as a shelf fetching more titles than
    /// it may hold.
    /// </remarks>
    [Fact]
    public void TheQuestionAskedIsBoundedByTheShelfsOwnCap()
    {
        var shelf = Shipped() with { Cap = 3 };

        Assert.Equal(3, shelf.Ask().Limit);
        Assert.Equal(3, shelf.Ask(startIndex: 20).Limit);
        Assert.Equal(20, shelf.Ask(startIndex: 20).StartIndex);
        Assert.Null(shelf.Ask().StartIndex);
    }

    /// <summary>
    /// A shelf arrives with this plugin's order rather than with none.
    /// </summary>
    /// <remarks>
    /// #85's third condition again, for the other borrowed field. A shelf built
    /// without an order stated would leave its titles in the sequence a source
    /// answered in, which is the row that rearranges under a user and is what
    /// #91 exists against, so the default is the order that issue built rather
    /// than an absence a caller has to remember to fill.
    /// </remarks>
    [Fact]
    public void AShelfCarriesAnOrderWithoutBeingGivenOne()
    {
        Assert.Same(DiscoverTitleOrder.ByStanding, Shipped().Order);
    }

    /// <summary>
    /// A shelf is on unless somebody turned it off.
    /// </summary>
    /// <remarks>
    /// The flag #86's fourth condition asks for, and no more than the flag.
    /// Nothing in this tree reads it, so what is asserted is the default a
    /// shelf is built with and not that an off shelf is skipped, which is
    /// #85's fourth condition and has nothing to count calls on.
    /// </remarks>
    [Fact]
    public void AShelfIsOnUnlessItSaysOtherwise()
    {
        Assert.True(Shipped().Enabled);
        Assert.False((Shipped() with { Enabled = false }).Enabled);
    }

    /// <summary>
    /// A shelf that could not be asked for anything is refused.
    /// </summary>
    /// <remarks>
    /// The unset member of each of the three closed sets, and a cap that is not
    /// a bound. Each is what a field nobody filled reads as, and each would
    /// otherwise reach an adapter as a choice for it to make.
    /// </remarks>
    /// <param name="question">What the shelf asks for.</param>
    /// <param name="kind">Which sort of title it holds.</param>
    /// <param name="source">Which source answers it.</param>
    /// <param name="cap">The most titles it may hold.</param>
    /// <param name="field">The field the refusal is expected to name.</param>
    [Theory]
    [InlineData(ShelfQuestion.None, DiscoverTitleKind.Movie, MetadataSource.Tmdb, 20, "Question")]
    [InlineData(ShelfQuestion.Popular, DiscoverTitleKind.None, MetadataSource.Tmdb, 20, "Kind")]
    [InlineData(ShelfQuestion.Popular, DiscoverTitleKind.Movie, MetadataSource.None, 20, "Source")]
    [InlineData(ShelfQuestion.Popular, DiscoverTitleKind.Movie, MetadataSource.Tmdb, 0, "Cap")]
    [InlineData(ShelfQuestion.Popular, DiscoverTitleKind.Movie, MetadataSource.Tmdb, -1, "Cap")]
    public void AShelfThatCouldNotBeAskedForAnythingIsRefused(
        ShelfQuestion question,
        DiscoverTitleKind kind,
        MetadataSource source,
        int cap,
        string field)
    {
        var shelf = new Shelf
        {
            DisplayName = "A row built out of an unset field",
            Question = question,
            Kind = kind,
            Source = source,
            Cap = cap
        };

        Assert.Equal(field, Assert.Throws<ArgumentOutOfRangeException>(() => shelf.Validated()).ParamName);
        Assert.Equal(field, Assert.Throws<ArgumentOutOfRangeException>(() => shelf.Ask()).ParamName);
    }

    /// <summary>
    /// A shelf with no name is refused where it is built.
    /// </summary>
    /// <remarks>
    /// At the setter rather than in <see cref="Shelf.Validated"/>, because a
    /// blank name has no reading at all: it is not a row an operator could find
    /// on a page, and unlike the closed sets there is no unset member to refuse
    /// later.
    /// </remarks>
    /// <param name="name">The name the shelf is built with.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AShelfWithNoNameIsRefused(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Shelf
        {
            DisplayName = name!,
            Question = ShelfQuestion.Popular,
            Kind = DiscoverTitleKind.Movie,
            Source = MetadataSource.Tmdb,
            Cap = 20
        });
    }

    /// <summary>
    /// A shelf naming a source this server does not ask is refused.
    /// </summary>
    /// <remarks>
    /// The half of #85's second condition that is decidable without asking
    /// anybody. The message names the source rather than saying a source is
    /// missing, because an operator with two configured sources otherwise has
    /// to guess which shelf is the orphan.
    /// </remarks>
    [Fact]
    public void AShelfNamingASourceNobodyAsksIsRefused()
    {
        var shelf = Shipped() with { Source = MetadataSource.Tvdb };

        var refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => shelf.ValidatedAgainst(new IMetadataSource[] { Adapter() }));

        Assert.Contains("Tvdb", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A shelf whose source is set up passes the same check.
    /// </summary>
    /// <remarks>
    /// The near-miss's other side. A check that refused every shelf would pass
    /// the test above and be useless, so the accepting direction is asserted
    /// rather than assumed from it.
    /// </remarks>
    [Fact]
    public void AShelfNamingASourceThisServerAsksIsAccepted()
    {
        var shelf = Shipped();

        Assert.Same(shelf, shelf.ValidatedAgainst(new IMetadataSource[] { Adapter() }));
    }

    /// <summary>
    /// A server that asks nobody has no shelf it can answer.
    /// </summary>
    [Fact]
    public void AServerWithNoSourceSetUpAnswersNoShelf()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Shipped().ValidatedAgainst(Array.Empty<IMetadataSource>()));
    }

    /// <summary>
    /// The check refuses the arguments it cannot read rather than passing them.
    /// </summary>
    [Fact]
    public void TheSourceListItselfIsRefusedWhenItIsNotOne()
    {
        Assert.Throws<ArgumentNullException>(() => Shipped().ValidatedAgainst(null!));
        Assert.Throws<ArgumentNullException>(() => Shipped().ValidatedAgainst(new IMetadataSource[] { null! }));
    }

    /// <summary>
    /// Every question this plugin carries is one the shipped adapter answers.
    /// </summary>
    /// <remarks>
    /// This is the near-miss the vocabulary is worth having. The spelling a
    /// shelf hands an adapter lives in one place and the address the adapter
    /// builds from it lives in another, so a member added to
    /// <see cref="ShelfQuestion"/> and not to the adapter is a shelf that is
    /// empty on every server, and it is empty in the way a source that was
    /// never configured is: the adapter answers
    /// <see cref="SourceOutcome.NotConfigured"/> for a name it has no question
    /// for, which is indistinguishable at the surface from a missing key.
    ///
    /// Watched failing rather than assumed to bite. Changing one spelling in
    /// <c>Shelf.Spelling</c> to a name the adapter has no address for reddens
    /// two of the six cases here, and adding a fourth member to the enum
    /// without touching the adapter reddens the two it adds.
    ///
    /// It asserts an answer rather than an address, so it does not restate the
    /// paths the adapter's own suite already holds. What it holds is the
    /// agreement between the two vocabularies.
    /// </remarks>
    /// <param name="question">The question a shelf asks.</param>
    /// <param name="kind">The kind it asks about.</param>
    /// <returns>The awaited assertion.</returns>
    [Theory]
    [MemberData(nameof(EveryQuestionAndKind))]
    public async Task EveryQuestionAShelfCanAskIsOneTheShippedAdapterAnswers(
        ShelfQuestion question,
        DiscoverTitleKind kind)
    {
        var shelf = Shipped() with { Question = question, Kind = kind };

        var answer = await Adapter()
            .FetchAsync(shelf.Ask(), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
    }

    /// <summary>
    /// A shelf of the shape the shipped set is made of.
    /// </summary>
    /// <returns>The shelf.</returns>
    /// <remarks>
    /// A fixture rather than one of the six, because which six ship is #86's
    /// and a copy of that set here would be the second register of it. The cap
    /// is a number this test chose and is not a default: what the bound is on a
    /// real server is #58's.
    /// </remarks>
    private static Shelf Shipped() => new()
    {
        DisplayName = "Popular films",
        Question = ShelfQuestion.Popular,
        Kind = DiscoverTitleKind.Movie,
        Source = MetadataSource.Tmdb,
        Cap = 20
    };

    /// <summary>
    /// An adapter that answers every address with an empty page.
    /// </summary>
    /// <returns>The adapter.</returns>
    /// <remarks>
    /// Configured, so that the one thing left that can make it answer
    /// <see cref="SourceOutcome.NotConfigured"/> is a question it has no
    /// address for. That is what makes the agreement above assertable without
    /// reading the address itself.
    /// </remarks>
    private static TmdbSourceAdapter Adapter() =>
        new TmdbSourceAdapter(
            (address, cancellationToken) =>
                Task.FromResult(new SourceTransportReply(200, TmdbFixtures.Body(TmdbFixtures.EmptyPage), null)),
            configured: true,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);
}
