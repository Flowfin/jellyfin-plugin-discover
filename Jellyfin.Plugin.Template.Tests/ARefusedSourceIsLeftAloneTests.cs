using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What a run does with a source that refused, and what it does not ask it.
/// </summary>
/// <remarks>
/// #78's third and fourth conditions where they are worth anything, which is at
/// the request the plugin does not make. <c>SourceRestTests</c> asserts the
/// decision; this asserts that a run acts on it, because a rest nothing reads
/// is a type with tests and no effect.
///
/// The assertions are on what the source was asked rather than on what a result
/// says, for the reason <c>CatalogueRefreshTests</c> gives about bytes: a run
/// that asked six times and reported six kept shelves is indistinguishable from
/// one that asked once, if the assertion stops at the outcome.
///
/// Nothing sleeps. Time passes by advancing the clock the refresh was built
/// with, which is #78's fifth condition and is why the rest is an instant to
/// compare against rather than a delay to serve.
/// </remarks>
public class ARefusedSourceIsLeftAloneTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _noon =
        new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// One refusal stops the rest of the run asking the same source.
    /// </summary>
    /// <remarks>
    /// The whole point of the third condition on a plugin that refreshes
    /// several shelves from one source: a refusal on the first shelf is a
    /// refusal for the fetch the second shelf was about to make, and a run that
    /// walked on would spend the source's budget three times over telling it
    /// something it has already said.
    ///
    /// Three shelves rather than two, so that a run stopping after the second
    /// request rather than after the first is still red.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task OneRefusalStopsTheRestOfTheRunAskingTheSameSource()
    {
        var folder = Folder("rest-one-run");
        Remove(folder);
        try
        {
            var shelves = new[] { Row(ShelfQuestion.Trending), Row(ShelfQuestion.Popular), Row(ShelfQuestion.TopRated) };
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);

            foreach (var shelf in shelves)
            {
                source.Answer(shelf.Ask(), SourceAnswer.RateLimited(TimeSpan.FromMinutes(30), "too many requests"));
            }

            var run = await Refresh(source, Store(folder), new ClockATestAdvances(_noon))
                .RunAsync(shelves, progress: null, CancellationToken.None);

            Assert.Single(source.Asked);
            Assert.Equal(3, run.Shelves.Count);
            Assert.All(run.Shelves, result => Assert.Equal(ShelfRefreshOutcome.PreviousKept, result.Outcome));
            Assert.All(run.Shelves, result => Assert.Equal(SourceOutcome.RateLimited, result.SourceOutcome));
            Assert.All(run.Shelves, result => Assert.Equal("too many requests", result.SourceMessage));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A shelf that was not asked keeps every byte it had.
    /// </summary>
    /// <remarks>
    /// #79's first condition, asked of the shelf this change stops asking for.
    /// A refusal reaching a shelf through a rest rather than through a fetch is
    /// a new route to the same result, and the failure it could introduce is
    /// the one that issue exists against: a shelf emptied because the plugin
    /// decided not to ask.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AShelfThatWasNotAskedKeepsEveryByteItHad()
    {
        var folder = Folder("rest-keeps-bytes");
        Remove(folder);
        try
        {
            var refused = Row(ShelfQuestion.Trending);
            var unasked = Row(ShelfQuestion.Popular);
            var store = Store(folder);
            var clock = new ClockATestAdvances(_noon);

            var answering = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
            answering.Answer(unasked.Ask(), SourceAnswer.Answered(new[] { Title("Fetched while the source was well") }, totalCount: 1));
            await Refresh(answering, store, clock).RunAsync(new[] { unasked }, progress: null, CancellationToken.None);

            var refusing = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
            refusing.Answer(refused.Ask(), SourceAnswer.TemporarilyFailed("the source is having a bad ten minutes"));
            await Refresh(refusing, store, clock).RunAsync(new[] { refused, unasked }, progress: null, CancellationToken.None);

            Assert.Single(refusing.Asked);

            var kept = CatalogueDocumentBody.Read(store.Read(CatalogueLayout.DocumentName(unasked))!);

            Assert.Equal("Fetched while the source was well", Assert.Single(kept).Name, StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The next run asks again only once the wait the source named has passed.
    /// </summary>
    /// <param name="minutesLater">How far the clock is advanced before the second run.</param>
    /// <param name="askedAgain">Whether the source is asked a second time.</param>
    /// <remarks>
    /// The boundary of the third condition where an operator meets it, which is
    /// across two runs rather than inside one. The wait is thirty minutes and
    /// the two rows are one minute either side of it, so a comparison written
    /// the wrong way round is red rather than merely late.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData(29, false)]
    [InlineData(31, true)]
    public async Task TheNextRunAsksAgainOnlyOnceTheWaitHasPassed(int minutesLater, bool askedAgain)
    {
        var folder = Folder("rest-across-runs-" + minutesLater.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Remove(folder);
        try
        {
            var shelf = Row(ShelfQuestion.Trending);
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
            source.Answer(shelf.Ask(), SourceAnswer.RateLimited(TimeSpan.FromMinutes(30), null));

            var clock = new ClockATestAdvances(_noon);
            var refresh = Refresh(source, Store(folder), clock);

            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);
            clock.Advance(TimeSpan.FromMinutes(minutesLater));
            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(askedAgain ? 2 : 1, source.Asked.Count);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A source that has been given up on is left alone for the longest rest,
    /// and the operator is told once.
    /// </summary>
    /// <remarks>
    /// #78's fourth condition end to end. The source names a one-minute wait
    /// every time, so anything that only honoured the stated wait would go on
    /// asking it for ever; after four refusals it is left for six hours, which
    /// the fifth run is inside.
    ///
    /// The telling is asserted as one line rather than as at least one, because
    /// the failure worth catching is a warning written per shelf that then goes
    /// unasked: an operator reading six of them is reading about six shelves
    /// rather than about one source.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASourceThatHasBeenGivenUpOnIsLeftAloneAndTheOperatorIsTold()
    {
        var folder = Folder("rest-given-up");
        Remove(folder);
        try
        {
            var shelves = new[] { Row(ShelfQuestion.Trending), Row(ShelfQuestion.Popular) };
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);

            foreach (var shelf in shelves)
            {
                source.Answer(shelf.Ask(), SourceAnswer.RateLimited(TimeSpan.FromMinutes(1), null));
            }

            var clock = new ClockATestAdvances(_noon);
            var written = new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>();
            var refresh = new CatalogueRefresh(new[] { source }, Store(folder), null, clock, new PauseATestWatches(), written);

            for (var run = 0; run < SourceRest.Tries; run++)
            {
                await refresh.RunAsync(shelves, progress: null, CancellationToken.None);
                clock.Advance(TimeSpan.FromMinutes(2));
            }

            Assert.Equal(SourceRest.Tries, source.Asked.Count);

            clock.Advance(SourceRest.LongestRest - TimeSpan.FromMinutes(SourceRest.Tries * 2));
            await refresh.RunAsync(shelves, progress: null, CancellationToken.None);

            Assert.Equal(SourceRest.Tries, source.Asked.Count);
            Assert.Single(written.Lines, line => line.Contains("is being left alone until", StringComparison.Ordinal));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A source that answers is asked again on the next run.
    /// </summary>
    /// <remarks>
    /// The direction that fails silently. Every assertion above is that a
    /// request was not made, and a rest that never ends satisfies all of them
    /// while stopping this plugin fetching anything ever again.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASourceThatAnswersIsAskedAgainOnTheNextRun()
    {
        var folder = Folder("rest-answering");
        Remove(folder);
        try
        {
            var shelf = Row(ShelfQuestion.Trending);
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
            source.Answer(shelf.Ask(), SourceAnswer.Answered(new[] { Title("Answered") }, totalCount: 1));

            var refresh = Refresh(source, Store(folder), new ClockATestAdvances(_noon));

            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);
            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(2, source.Asked.Count);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A source this server is not set up to ask is asked again next time.
    /// </summary>
    /// <remarks>
    /// The case to be careful about, because it arrives at a run looking like
    /// the other two ways a source gives nothing. Nothing is wrong with a
    /// source that has not been set up, so resting it would stop this plugin
    /// asking a source whose only fault is that a shelf named it, and an
    /// operator who then configures the source would wait six hours for a shelf
    /// with no way to tell why.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASourceThatIsNotSetUpIsAskedAgainNextTime()
    {
        var folder = Folder("rest-not-configured");
        Remove(folder);
        try
        {
            var shelf = Row(ShelfQuestion.Trending);
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
            source.Answer(shelf.Ask(), SourceAnswer.NotConfigured());

            var refresh = Refresh(source, Store(folder), new ClockATestAdvances(_noon));

            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);
            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(2, source.Asked.Count);
        }
        finally
        {
            Remove(folder);
        }
    }

    private static Shelf Row(ShelfQuestion question) => new Shelf
    {
        DisplayName = "A row of films",
        Question = question,
        Kind = DiscoverTitleKind.Movie,
        Source = MetadataSource.Tmdb,
        Cap = 5
    };

    private static DiscoverTitle Title(string name) => new DiscoverTitle
    {
        Kind = DiscoverTitleKind.Movie,
        Name = name,
        VoteCount = 10,
        FetchedAt = _noon,
        Identity = new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Tmdb, "1")
        })
    };

    private static CatalogueRefresh Refresh(IMetadataSource source, CatalogueDocumentStore store, ClockATestAdvances clock) =>
        new CatalogueRefresh(
            new[] { source },
            store,
            null,
            clock,
            new PauseATestWatches(),
            new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>());

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
