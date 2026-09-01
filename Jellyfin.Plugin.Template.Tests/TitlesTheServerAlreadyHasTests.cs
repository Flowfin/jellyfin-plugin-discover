using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Server;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A shelf does not offer a title this server already holds.
/// </summary>
/// <remarks>
/// #89. The rule is one sentence over both kinds: a title is owned when the
/// server holds at least one part of it, where a part is the film for a movie
/// and an episode for a series. The series half is #2's answer of 2026-08-24.
///
/// The assertions are over what is on the disk after a run rather than over a
/// helper's return value, for the reason <c>CatalogueRefreshTests</c> gives
/// about the same store: what this issue is about is a title reaching a
/// catalogue document, and a comparison made in the middle of a run that is
/// then written anyway is the failure a test of the helper would pass.
/// </remarks>
public class TitlesTheServerAlreadyHasTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _fetchedAt =
        new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] _theOneNobodyHas = new[] { "The one nobody has" };

    private static readonly string[] _theOneWithNoEpisode = new[] { "The one the server has only a row for" };

    private static readonly string[] _oneName = new[] { "The Thing" };

    private static readonly string[] _theTwoIdentifiersAsked = new[] { "41", "42" };

    private static readonly string[] _theTwoUnderTheCap =
        new[] { "The next loudest", "The one after that" };

    private static readonly Type[] _anIdentityAndAKind =
        new[] { typeof(DiscoverTitleIdentity), typeof(DiscoverTitleKind) };

    /// <summary>
    /// A film the server holds is not written to the shelf's document, and one it does not is.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    /// <remarks>
    /// Both directions in one run, because a filter asserted only on what it
    /// dropped passes just as well when it drops everything.
    /// </remarks>
    [Fact]
    public async Task AFilmTheServerHoldsIsNotOnTheShelf()
    {
        var folder = Folder("owned-films");
        Remove(folder);
        try
        {
            var shelf = Row();
            var owned = Film("The one the household bought", "11");
            var missing = Film("The one nobody has", "22");

            var library = new LibraryThatHoldsWhatATestGaveIt().Holding(owned);

            await RunAsync(shelf, folder, library, owned, missing);

            Assert.Equal(_theOneNobodyHas, NamesIn(folder, shelf), StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A series the server carries with one episode is owned, and one it carries with none is not.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    /// <remarks>
    /// The rule #2 answered on 2026-08-24, at the boundary it turns on. One
    /// episode is the smallest number that makes a series owned, and zero is a
    /// server carrying the row and nothing under it, which is what makes the
    /// seam answer with a count rather than a yes.
    ///
    /// Watched failing with the refresh's comparison written as more than one
    /// part rather than more than none, which is the one-character mistake at
    /// this boundary. That reddens this and three others.
    /// </remarks>
    [Fact]
    public async Task ASeriesIsOwnedFromItsFirstEpisodeAndNotBefore()
    {
        var folder = Folder("owned-series");
        Remove(folder);
        try
        {
            var shelf = Row(DiscoverTitleKind.Series);
            var oneEpisode = Series("The one with a season on the server", "31");
            var noEpisode = Series("The one the server has only a row for", "32");

            var library = new LibraryThatHoldsWhatATestGaveIt()
                .Holding(oneEpisode, parts: 1)
                .Holding(noEpisode, parts: 0);

            await RunAsync(shelf, folder, library, oneEpisode, noEpisode);

            Assert.Equal(_theOneWithNoEpisode, NamesIn(folder, shelf), StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Two titles with one name and different identifiers are two different questions.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    /// <remarks>
    /// #89's second condition. The remake and the original share a name and a
    /// year is not always enough to tell them apart, so a comparison by title
    /// text takes both off a shelf when the server holds one. What holds the
    /// rule is the seam's signature rather than this test:
    /// <see cref="IServerLibrary"/> is handed an identity and a kind and never
    /// a title, so there is no name for an implementation to match on. This
    /// asserts the consequence, which is that the two are separable at all.
    /// </remarks>
    [Fact]
    public async Task TwoTitlesWithOneNameAreToldApartByTheirIdentifiers()
    {
        var folder = Folder("owned-namesakes");
        Remove(folder);
        try
        {
            var shelf = Row();
            var held = Film("The Thing", "41");
            var notHeld = Film("The Thing", "42");

            var library = new LibraryThatHoldsWhatATestGaveIt().Holding(held);

            await RunAsync(shelf, folder, library, held, notHeld);

            var stored = TitlesIn(folder, shelf);

            Assert.Equal(_oneName, stored.Select(title => title.Name).ToArray(), StringComparer.Ordinal);
            Assert.Equal("42", stored[0].Identity.Primary.Value);

            Assert.Equal(
                _theTwoIdentifiersAsked,
                library.Asked.Select(question => question.Identity.Primary.Value).ToArray(),
                StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The question carries the identity and the kind, and nothing else.
    /// </summary>
    /// <remarks>
    /// #89's second condition again, in the direction a test over behaviour
    /// cannot reach: that no title text is available to be compared. Read off
    /// the seam's one member rather than off a call, so a parameter added to it
    /// later fails here rather than being noticed by a reader.
    ///
    /// A near-miss worth having, because the cheap way to write this seam is to
    /// hand it the title, and every assertion above passes under that shape.
    /// Watched failing with a name added to the seam's one member and passed
    /// from the refresh: this reddens and nothing else does.
    /// </remarks>
    [Fact]
    public void TheSeamCarriesNoTitleText()
    {
        var members = typeof(IServerLibrary).GetMethods();

        Assert.Single(members);

        var parameters = members[0].GetParameters();

        Assert.Equal(
            _anIdentityAndAKind,
            parameters.Select(parameter => parameter.ParameterType).ToArray());

        Assert.Equal(typeof(int), members[0].ReturnType);
    }

    /// <summary>
    /// With nothing able to say what the server holds, every title a source offered is kept.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    /// <remarks>
    /// The state of this tree rather than a mode: nothing implements the seam,
    /// so this is what a run on a real server does today. Asserted so that the
    /// day an implementation arrives, the change in behaviour is a red test
    /// rather than a difference nobody wrote down.
    /// </remarks>
    [Fact]
    public async Task WithNoLibraryToAskNothingIsLeftOut()
    {
        var folder = Folder("owned-no-library");
        Remove(folder);
        try
        {
            var shelf = Row();
            var first = Film("The first", "51");
            var second = Film("The second", "52");

            await RunAsync(shelf, folder, library: null, first, second);

            Assert.Equal(2, TitlesIn(folder, shelf).Count);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The cap counts what is left after the filter, not before it.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    /// <remarks>
    /// A shelf's cap is how many titles it shows. Filtering after the cap would
    /// hand a user a row of one because the other two were already on the
    /// server, and the number an operator set would silently mean something
    /// else. Watched failing with the filter moved after the <c>Take</c>: the
    /// shelf then holds one title rather than two.
    /// </remarks>
    [Fact]
    public async Task TheCapIsFilledFromWhatIsLeft()
    {
        var folder = Folder("owned-cap");
        Remove(folder);
        try
        {
            var shelf = Row(cap: 2);
            var owned = Film("The loudest one, and the server has it", "61", votes: 300);
            var first = Film("The next loudest", "62", votes: 200);
            var second = Film("The one after that", "63", votes: 100);

            var library = new LibraryThatHoldsWhatATestGaveIt().Holding(owned);

            await RunAsync(shelf, folder, library, owned, first, second);

            Assert.Equal(_theTwoUnderTheCap, NamesIn(folder, shelf), StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    private static async Task RunAsync(
        Shelf shelf,
        string folder,
        IServerLibrary? library,
        params DiscoverTitle[] offered)
    {
        var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
        source.Answer(shelf.Ask(), SourceAnswer.Answered(offered, offered.Length));

        var run = await new CatalogueRefresh(
            new[] { source },
            Store(folder),
            library,
            new ClockATestAdvances(_fetchedAt),
            new PauseATestWatches(),
            new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>())
            .RunAsync(new[] { shelf }, progress: null, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ShelfRefreshOutcome.Refreshed, run.Shelves[0].Outcome);
    }

    private static IReadOnlyList<DiscoverTitle> TitlesIn(string folder, Shelf shelf) =>
        CatalogueDocumentBody.Read(Store(folder).Read(CatalogueLayout.DocumentName(shelf))!);

    private static string[] NamesIn(string folder, Shelf shelf) =>
        TitlesIn(folder, shelf).Select(title => title.Name).ToArray();

    private static Shelf Row(DiscoverTitleKind kind = DiscoverTitleKind.Movie, int cap = 10) => new Shelf
    {
        DisplayName = "A row",
        Question = ShelfQuestion.Trending,
        Kind = kind,
        Source = MetadataSource.Tmdb,
        Cap = cap
    };

    private static DiscoverTitle Film(string name, string identifier, int votes = 10) =>
        Of(name, identifier, DiscoverTitleKind.Movie, votes);

    private static DiscoverTitle Series(string name, string identifier, int votes = 10) =>
        Of(name, identifier, DiscoverTitleKind.Series, votes);

    private static DiscoverTitle Of(string name, string identifier, DiscoverTitleKind kind, int votes) =>
        new DiscoverTitle
        {
            Kind = kind,
            Name = name,
            VoteCount = votes,
            FetchedAt = _fetchedAt,
            Identity = new DiscoverTitleIdentity(new[]
            {
                new ProviderIdentifier(MetadataSource.Tmdb, identifier)
            })
        };

    private static CatalogueDocumentStore Store(string folder) =>
        new CatalogueDocumentStore(
            new CatalogueDirectory(folder),
            new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());

    private static string Folder(string name) => Path.Combine(Path.GetTempPath(), TestFolders, name);

    private static void Remove(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
