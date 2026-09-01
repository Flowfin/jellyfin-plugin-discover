using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What a run does with a document it did not refresh whose records have gone
/// past the retention.
/// </summary>
/// <remarks>
/// #68's second condition, the half about not KEEPING. Against a real store in
/// a directory of the test's own, because the property is what is on the disk
/// after a run and a fake store would let "it was removed" pass whether or not
/// anything was removed.
///
/// The retention in force here is the fake source's ceiling of one day rather
/// than the shipped ninety, which is what makes the boundary reachable by a
/// clock a test advances instead of by a fixture dated three months back. The
/// number is read off the refresh rather than typed, so a test cannot drift
/// from the value the run applies.
///
/// The folders are named after the test that owns them, because `no-random`
/// refuses a drawn name and two tests sharing a folder is two tests that pass
/// alone. Each removes what it made.
/// </remarks>
public class CatalogueRetentionSweepTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _fetchedAt =
        new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] _theOrderTheDocumentCarries = { "Third", "First", "Second" };

    /// <summary>
    /// A kept document whose every record is past the retention is removed.
    /// </summary>
    /// <remarks>
    /// The case the condition is written against: a shelf whose source stopped
    /// answering months ago, whose document nothing else would ever touch
    /// again. The outcome is asserted as well as the disk, because a run that
    /// removed the document and went on reporting <c>PreviousKept</c> would tell
    /// an operator that the shelf still holds what it held.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task AKeptDocumentPastTheRetentionIsRemovedRatherThanKept()
    {
        var folder = Folder("sweep-all-expired");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row();
            var document = CatalogueLayout.DocumentName(shelf);

            Write(store, document, Title("Long ago", "1", _fetchedAt));

            var clock = new ClockATestAdvances(_fetchedAt);
            var refresh = RefreshOver(NotAnswering(shelf), store, clock);

            clock.Advance(refresh.Retention.Duration + TimeSpan.FromTicks(1));

            var run = await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            var result = Assert.Single(run.Shelves);

            Assert.Equal(ShelfRefreshOutcome.Expired, result.Outcome);
            Assert.Equal(0, result.TitlesWritten);
            Assert.Null(store.Read(document));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A record exactly as old as the retention is kept, and one tick past it is
    /// not.
    /// </summary>
    /// <remarks>
    /// The boundary in both directions, in one test as a pair, because a
    /// comparison written the other way round passes either half alone. The
    /// inclusive side is the one the condition fixes: a record older than the
    /// retention is not kept, and a record exactly as old as it is not older
    /// than it.
    /// </remarks>
    /// <param name="ticksPast">How far past the retention the clock is moved.</param>
    /// <param name="expectedToSurvive">Whether the document is still there afterwards.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task TheBoundaryIsInclusiveAndOneTickPastItIsNot(long ticksPast, bool expectedToSurvive)
    {
        var folder = Folder("sweep-boundary-" + ticksPast.ToString(CultureInfo.InvariantCulture));
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row();
            var document = CatalogueLayout.DocumentName(shelf);

            Write(store, document, Title("On the line", "1", _fetchedAt));

            var clock = new ClockATestAdvances(_fetchedAt);
            var refresh = RefreshOver(NotAnswering(shelf), store, clock);

            clock.Advance(refresh.Retention.Duration + TimeSpan.FromTicks(ticksPast));

            var run = await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(
                expectedToSurvive ? ShelfRefreshOutcome.PreviousKept : ShelfRefreshOutcome.Expired,
                Assert.Single(run.Shelves).Outcome);

            Assert.Equal(expectedToSurvive, store.Read(document) is not null);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A document holding some records past the retention keeps the rest.
    /// </summary>
    /// <remarks>
    /// The near miss, and the one a sweep written per document rather than per
    /// record fails: removing the whole document because one record in it
    /// expired throws away titles this plugin may still keep, and a shelf that
    /// went empty for that reason looks to an operator exactly like a source
    /// that answered with nothing.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task ADocumentKeepsTheRecordsStillHeldAndDropsTheRest()
    {
        var folder = Folder("sweep-mixed");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row();
            var document = CatalogueLayout.DocumentName(shelf);

            var clock = new ClockATestAdvances(_fetchedAt);
            var refresh = RefreshOver(NotAnswering(shelf), store, clock);
            var retention = refresh.Retention.Duration;

            Write(
                store,
                document,
                Title("Older", "1", _fetchedAt),
                Title("Newer", "2", _fetchedAt + retention));

            clock.Advance(retention + TimeSpan.FromTicks(1));

            var run = await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            var result = Assert.Single(run.Shelves);

            Assert.Equal(ShelfRefreshOutcome.Expired, result.Outcome);
            Assert.Equal(1, result.TitlesWritten);

            var kept = CatalogueDocumentBody.Read(store.Read(document)!);

            Assert.Equal("Newer", Assert.Single(kept).Name, StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A shelf that is turned off has its document swept too.
    /// </summary>
    /// <remarks>
    /// The half a sweep written as housekeeping would skip. The ceiling under
    /// the number is a source's terms, and terms do not stop applying because
    /// an operator switched a row off, so the records a server keeps longest
    /// would otherwise be exactly the ones nothing looks at.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task ATurnedOffShelfHasItsDocumentSweptToo()
    {
        var folder = Folder("sweep-turned-off");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row() with { Enabled = false };
            var document = CatalogueLayout.DocumentName(shelf);

            Write(store, document, Title("Left behind", "1", _fetchedAt));

            var clock = new ClockATestAdvances(_fetchedAt);
            var refresh = RefreshOver(NotAnswering(shelf), store, clock);

            clock.Advance(refresh.Retention.Duration + TimeSpan.FromTicks(1));

            var run = await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(ShelfRefreshOutcome.Expired, Assert.Single(run.Shelves).Outcome);
            Assert.Null(store.Read(document));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A shelf whose source answered is written by the run and not swept.
    /// </summary>
    /// <remarks>
    /// The document a run has just written holds what a source answered a
    /// moment ago, so nothing in it can be past the retention. Asserted because
    /// a sweep placed after every shelf rather than after the two that leave a
    /// document standing costs a read and a parse per shelf per run for an
    /// answer that is already known.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task AShelfTheRunRefreshedIsNotSwept()
    {
        var folder = Folder("sweep-refreshed");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row();
            var document = CatalogueLayout.DocumentName(shelf);

            var clock = new ClockATestAdvances(_fetchedAt);
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
            source.Answer(
                shelf.Ask(),
                SourceAnswer.Answered(new[] { Title("Fresh", "1", _fetchedAt) }, totalCount: 1));

            var refresh = RefreshOver(source, store, clock);

            clock.Advance(refresh.Retention.Duration + TimeSpan.FromDays(365));

            var run = await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(ShelfRefreshOutcome.Refreshed, Assert.Single(run.Shelves).Outcome);
            Assert.NotNull(store.Read(document));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A document whose bytes are not a body this build reads is left where it
    /// is.
    /// </summary>
    /// <remarks>
    /// The direction a sweep must not take. Nothing in a document that cannot
    /// be parsed has a fetch time, so nothing in it has been shown to be past
    /// the retention, and removing it would be a sweep deleting what it could
    /// not judge. The header is the store's own, so this is a body the store
    /// hands back and the parser refuses rather than a file the store already
    /// rejects.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task ADocumentThatCannotBeParsedIsLeftWhereItIs()
    {
        var folder = Folder("sweep-unparseable");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row();
            var document = CatalogueLayout.DocumentName(shelf);

            using (var notABody = new MemoryStream(Encoding.UTF8.GetBytes("{ this is not the JSON this plugin writes")))
            {
                store.Write(document, notABody);
            }

            var before = store.Read(document);
            Assert.NotNull(before);

            var clock = new ClockATestAdvances(_fetchedAt);
            var refresh = RefreshOver(NotAnswering(shelf), store, clock);

            clock.Advance(refresh.Retention.Duration + TimeSpan.FromDays(365));

            var run = await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(ShelfRefreshOutcome.PreviousKept, Assert.Single(run.Shelves).Outcome);
            Assert.Equal(before, store.Read(document));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A shelf with no document to sweep is reported as having kept what it had.
    /// </summary>
    /// <remarks>
    /// The state every first run is in. A sweep that treated an absent document
    /// as an expired one would report every shelf on a fresh server as having
    /// lost records it never held.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task AShelfWithNoDocumentAtAllIsNotReportedAsExpired()
    {
        var folder = Folder("sweep-nothing-there");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row();

            var clock = new ClockATestAdvances(_fetchedAt);
            var refresh = RefreshOver(NotAnswering(shelf), store, clock);

            clock.Advance(refresh.Retention.Duration + TimeSpan.FromDays(365));

            var run = await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(ShelfRefreshOutcome.PreviousKept, Assert.Single(run.Shelves).Outcome);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The retention a run applies is the shipped default, or a source's
    /// ceiling where that is shorter, and it says which source shortened it.
    /// </summary>
    /// <remarks>
    /// A run always has to apply something, which is why this shortens where
    /// <c>Of</c> refuses. A refresh that could not be built because a source's
    /// terms are stricter than the number an operator saved would leave the
    /// records already on disk with nothing sweeping them, which is the opposite
    /// of what the stricter terms ask for.
    /// </remarks>
    [Fact]
    public void TheRetentionInForceIsTheShorterOfTheNumberAndEveryCeiling()
    {
        var strict = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb, TimeSpan.FromDays(7));
        var generous = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tvdb, TimeSpan.FromDays(400));

        var underBoth = CatalogueRetention.InForce(
            CatalogueRetention.Default,
            new IMetadataSource[] { strict, generous },
            out var cappedBy);

        Assert.Equal(TimeSpan.FromDays(7), underBoth.Duration);
        Assert.Same(strict, cappedBy);

        var underNeither = CatalogueRetention.InForce(
            CatalogueRetention.Default,
            new IMetadataSource[] { generous },
            out var uncapped);

        Assert.Equal(CatalogueRetention.Default, underNeither.Duration);
        Assert.Null(uncapped);

        var withNoSources = CatalogueRetention.InForce(
            CatalogueRetention.Default,
            Array.Empty<IMetadataSource>(),
            out var alsoUncapped);

        Assert.Equal(CatalogueRetention.Default, withNoSources.Duration);
        Assert.Null(alsoUncapped);
    }

    /// <summary>
    /// A source declaring that nothing may be kept is refused rather than
    /// adopted.
    /// </summary>
    /// <remarks>
    /// A ceiling of nothing is not a retention, and a run that took it would
    /// delete every record on every pass while reporting an ordinary sweep. An
    /// adapter declaring one is a defect in that adapter.
    /// </remarks>
    [Fact]
    public void ASourceThatPermitsNoCachingAtAllIsRefused()
    {
        var permitsNothing = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueRetention.InForce(
                CatalogueRetention.Default,
                new IMetadataSource[] { permitsNothing },
                out _));
    }

    /// <summary>
    /// The sift keeps the document's own order and drops only what is past the
    /// retention.
    /// </summary>
    /// <remarks>
    /// The one place the per-record question is asked over a set, so that not
    /// serving a record and not keeping it are two actions on one answer. The
    /// order is the document's, because what decides the order titles are shown
    /// in is applied where a shelf is written.
    /// </remarks>
    [Fact]
    public void WhatIsStillHeldKeepsTheDocumentsOrder()
    {
        var retention = CatalogueRetention.InForce(
            TimeSpan.FromDays(10),
            Array.Empty<IMetadataSource>(),
            out _);

        var now = _fetchedAt + TimeSpan.FromDays(20);

        var titles = new[]
        {
            Title("Third", "3", now - TimeSpan.FromDays(1)),
            Title("Gone", "9", now - TimeSpan.FromDays(11)),
            Title("First", "1", now - TimeSpan.FromDays(10)),
            Title("Second", "2", now - TimeSpan.FromDays(5))
        };

        Assert.Equal(
            _theOrderTheDocumentCarries,
            retention.StillHeld(titles, now).Select(title => title.Name).ToArray(),
            StringComparer.Ordinal);

        var nothingExpired = new[] { titles[0], titles[3] };

        Assert.Same(nothingExpired, retention.StillHeld(nothingExpired, now));
    }

    private static Shelf Row() => new Shelf
    {
        DisplayName = "A row of films",
        Question = ShelfQuestion.Trending,
        Kind = DiscoverTitleKind.Movie,
        Source = MetadataSource.Tmdb,
        Cap = 20
    };

    private static DiscoverTitle Title(string name, string identifier, DateTimeOffset fetchedAt) => new DiscoverTitle
    {
        Kind = DiscoverTitleKind.Movie,
        Name = name,
        VoteCount = 1,
        FetchedAt = fetchedAt,
        Identity = new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Tmdb, identifier)
        })
    };

    private static SourceThatAnswersFromWhatATestGaveIt NotAnswering(Shelf shelf)
    {
        var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
        source.Answer(shelf.Ask(), SourceAnswer.TemporarilyFailed("the source is having a bad ten minutes"));
        return source;
    }

    private static void Write(CatalogueDocumentStore store, string document, params DiscoverTitle[] titles)
    {
        using var payload = new MemoryStream();

        CatalogueDocumentBody.Write(payload, titles);
        payload.Position = 0;

        store.Write(document, payload);
    }

    private static CatalogueRefresh RefreshOver(
        IMetadataSource source,
        CatalogueDocumentStore store,
        ClockATestAdvances clock) =>
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
