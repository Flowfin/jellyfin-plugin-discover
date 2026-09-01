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
/// What happens to a catalogue document whose shelf is gone.
/// </summary>
/// <remarks>
/// #68's second condition, in the half a sweep driven by the shelves in a run
/// cannot reach. A document's name comes from a shelf's question and kind, so a
/// version that ships a different set, and a downgrade, leave a document nothing
/// asks about, nothing dates and nothing removes. The records a server keeps
/// longest are then exactly the ones nobody is looking at, and the ceiling over
/// them is a source's terms rather than this plugin's housekeeping.
///
/// The listing the run needs is asserted here too, rather than beside the rest
/// of the store, because it exists for this case and for nothing else: every
/// other reader of the store arrives with a name a shelf produced.
/// </remarks>
public class ADocumentNoShelfNamesIsStillSweptTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    /// <summary>
    /// A document name no shipped shelf produces, which is what this whole file
    /// is about.
    /// </summary>
    /// <remarks>
    /// Written out rather than composed from a question and a kind, because
    /// composing it would need a pair no shelf holds and the set of pairs is
    /// what is expected to move. A literal is a name from a version that is not
    /// this one, which is exactly the case.
    /// </remarks>
    private const string Orphan = "retired-movie";

    private static readonly DateTimeOffset _fetchedAt =
        new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] _onlyTheOneInsideTheRetention = new[] { "Recent" };

    /// <summary>
    /// The listing answers with the documents that were written.
    /// </summary>
    /// <remarks>
    /// First, because everything below rests on the run being able to see a
    /// document no caller can name. A listing that answered with nothing would
    /// pass every assertion about a document being left alone.
    /// </remarks>
    [Fact]
    public void TheListingAnswersWithTheDocumentsThatWereWritten()
    {
        var folder = Folder("listing-what-was-written");
        Remove(folder);
        try
        {
            var store = Store(folder);

            Write(store, Orphan, Title("Long ago", "1", _fetchedAt));
            Write(store, "trending-movie", Title("Still here", "2", _fetchedAt));

            Assert.Equal(new[] { Orphan, "trending-movie" }, store.DocumentNames(), StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The listing leaves out the file a write is still building.
    /// </summary>
    /// <remarks>
    /// A write lands a temporary beside the document it will replace, and the
    /// directory's own listing sees it. Handing that name to a caller gives it
    /// one the store refuses as unreadable a moment later, and here that would
    /// be a sweep reporting an unreadable body once per refresh that overlapped
    /// a write. Both listings are asserted, so this states the difference
    /// between them rather than only the answer.
    /// </remarks>
    [Fact]
    public void TheListingLeavesOutTheFileAWriteIsStillBuilding()
    {
        var folder = Folder("listing-half-written");
        Remove(folder);
        try
        {
            var directory = new CatalogueDirectory(folder);
            var store = new CatalogueDocumentStore(directory, new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());

            Write(store, Orphan, Title("Long ago", "1", _fetchedAt));

            File.WriteAllText(
                Path.Combine(directory.FullPath, Orphan + CatalogueDocumentStore.TemporaryNameSuffix),
                "half of a document");

            Assert.Equal(
                new[] { Orphan, Orphan + CatalogueDocumentStore.TemporaryNameSuffix },
                directory.ListDocuments(),
                StringComparer.Ordinal);

            Assert.Equal(new[] { Orphan }, store.DocumentNames(), StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The listing is empty on a server where nothing has been written.
    /// </summary>
    /// <remarks>
    /// The state of every fresh install, and reading never creates: a listing
    /// that made the directory would put this plugin's folder on the disk of a
    /// server that had only started, which <c>AFreshInstallWritesNothingTests</c>
    /// asserts against from the other side.
    /// </remarks>
    [Fact]
    public void TheListingIsEmptyBeforeAnythingIsWritten()
    {
        var folder = Folder("listing-nothing-written");
        Remove(folder);
        try
        {
            Assert.Empty(Store(folder).DocumentNames());
            Assert.False(Directory.Exists(new CatalogueDirectory(folder).FullPath));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A document no shelf names loses the records that are past the retention
    /// and keeps the ones that are not.
    /// </summary>
    /// <remarks>
    /// The condition, on the document the per-shelf sweep never reads. Both
    /// directions in one document, because a sweep that emptied it would pass an
    /// assertion that only counted what was dropped.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task ADocumentNoShelfNamesLosesOnlyWhatIsPastTheRetention()
    {
        var folder = Folder("orphan-partly-expired");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row();
            var clock = new ClockATestAdvances(_fetchedAt);
            var refresh = RefreshOver(Answering(shelf), store, clock);
            var retention = refresh.Retention.Duration;

            Write(
                store,
                Orphan,
                Title("Long ago", "1", _fetchedAt),
                Title("Recent", "2", _fetchedAt + retention));

            clock.Advance(retention + TimeSpan.FromTicks(1));

            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            var held = CatalogueDocumentBody.Read(store.Read(Orphan)!);

            Assert.Equal(_onlyTheOneInsideTheRetention, held.Select(title => title.Name).ToArray(), StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A document no shelf names whose every record is past the retention is
    /// removed.
    /// </summary>
    /// <remarks>
    /// The case the finding on #68 describes: a shelf that a version dropped,
    /// whose document nothing would ever touch again, holding a source's records
    /// for ever rather than for the ninety days its terms allow.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ADocumentNoShelfNamesIsRemovedWhenEveryRecordIsPastTheRetention()
    {
        var folder = Folder("orphan-all-expired");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row();
            var clock = new ClockATestAdvances(_fetchedAt);
            var refresh = RefreshOver(Answering(shelf), store, clock);

            Write(store, Orphan, Title("Long ago", "1", _fetchedAt));

            clock.Advance(refresh.Retention.Duration + TimeSpan.FromTicks(1));

            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Null(store.Read(Orphan));
            Assert.DoesNotContain(Orphan, store.DocumentNames(), StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A document no shelf names and nothing in which is past the retention is
    /// left exactly as it was.
    /// </summary>
    /// <remarks>
    /// The other direction, and the one that keeps this from being a sweep that
    /// deletes whatever it does not recognise. Asserted on the bytes rather than
    /// on the records, because a sweep that read the document and wrote it back
    /// unchanged would pass a comparison of the titles and would still be
    /// rewriting a file for nothing on every refresh.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ADocumentNoShelfNamesInsideTheRetentionIsLeftAsItWas()
    {
        var folder = Folder("orphan-inside");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row();
            var clock = new ClockATestAdvances(_fetchedAt);
            var refresh = RefreshOver(Answering(shelf), store, clock);

            Write(store, Orphan, Title("Recent", "2", _fetchedAt));

            var before = store.Read(Orphan);
            var written = File.GetLastWriteTimeUtc(new CatalogueDirectory(folder).DocumentPath(Orphan));

            clock.Advance(refresh.Retention.Duration);

            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(before, store.Read(Orphan));
            Assert.Equal(written, File.GetLastWriteTimeUtc(new CatalogueDirectory(folder).DocumentPath(Orphan)));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A cancelled run leaves a document no shelf names alone.
    /// </summary>
    /// <remarks>
    /// Doing work after a cancellation is the thing a cancellation asked to
    /// stop, and this is more work than the per-shelf sweep it already skips: a
    /// listing of the directory and a read for every document in it. The record
    /// here is past the retention, so a run that swept anyway would remove it
    /// and the assertion would fail rather than pass for want of anything to do.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ACancelledRunLeavesADocumentNoShelfNamesAlone()
    {
        var folder = Folder("orphan-cancelled");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var shelf = Row();
            var clock = new ClockATestAdvances(_fetchedAt);
            var refresh = RefreshOver(Answering(shelf), store, clock);

            Write(store, Orphan, Title("Long ago", "1", _fetchedAt));

            clock.Advance(refresh.Retention.Duration + TimeSpan.FromTicks(1));

            using var stopped = new CancellationTokenSource();
            await stopped.CancelAsync();

            var run = await refresh.RunAsync(new[] { shelf }, progress: null, stopped.Token);

            Assert.True(run.Cancelled);
            Assert.NotNull(store.Read(Orphan));
        }
        finally
        {
            Remove(folder);
        }
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

    private static SourceThatAnswersFromWhatATestGaveIt Answering(Shelf shelf)
    {
        var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);

        source.Answer(
            shelf.Ask(),
            SourceAnswer.Answered(new[] { Title("Fresh", "9", _fetchedAt) }, totalCount: 1));

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
