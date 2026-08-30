using System;
using System.Collections.Generic;
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
/// What one pass over the shelves does, and what it refuses to do.
/// </summary>
/// <remarks>
/// Against the fake source and the clock a test advances, which is #87's sixth
/// condition, and against a real store in a directory of the test's own. The
/// store is real rather than faked for the reason
/// <c>CatalogueDocumentStoreTests</c> gives: the property several of these
/// assertions are about is what is on a disk after a run, and a fake store
/// would let "nothing was written" pass whether or not the refresh wrote.
///
/// The folders are under the temporary directory and are named after the test
/// that owns them, because <c>no-random</c> refuses a drawn name and two tests
/// sharing a folder is two tests that pass alone. Each removes what it made.
/// Nothing here writes into the folder a real install would use: that is the
/// one the base plugin class derives, and <c>AFreshInstallWritesNothingTests</c>
/// asserts it stays absent.
/// </remarks>
public class CatalogueRefreshTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _fetchedAt =
        new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] _theTwoUnderTheCap = new[] { "The loud one", "The third one" };

    /// <summary>
    /// A shelf whose source answered holds what it gave, in the shelf's order
    /// and under the shelf's bound.
    /// </summary>
    /// <remarks>
    /// First because everything below asserts that a write did not happen, and a
    /// suite whose only passing case is a refusal proves a refresh that never
    /// writes at all.
    ///
    /// The order is asserted by handing the source the titles in the wrong one.
    /// A source answers in its own sequence, which #91 says is not an order a
    /// shelf may draw, so a refresh that wrote the answer through unchanged
    /// would pass an assertion that only counted the titles.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AShelfWhoseSourceAnsweredHoldsWhatItGave()
    {
        var folder = Folder("refresh-writes");
        Remove(folder);
        try
        {
            var shelf = Row(cap: 2);
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);

            source.Answer(
                shelf.Ask(),
                SourceAnswer.Answered(
                    new[] { Title("The quiet one", "1", votes: 10), Title("The loud one", "2", votes: 900), Title("The third one", "3", votes: 500) },
                    totalCount: 3));

            var store = Store(folder);
            var run = await RefreshOver(source, store).RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.True(run.Started);
            Assert.False(run.Cancelled);
            Assert.Equal(ShelfRefreshOutcome.Refreshed, Assert.Single(run.Shelves).Outcome);
            Assert.Equal(2, run.Shelves[0].TitlesWritten);

            var written = CatalogueDocumentBody.Read(store.Read(CatalogueLayout.DocumentName(shelf))!);

            Assert.Equal(_theTwoUnderTheCap, written.Select(title => title.Name).ToArray(), StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A shelf whose source could not answer keeps every byte it had, and the
    /// source's own words reach the result.
    /// </summary>
    /// <remarks>
    /// #79's first condition, asserted on the bytes rather than on the outcome.
    /// A refresh that wrote an empty document would report the same outcome as
    /// one that wrote nothing if the assertion stopped at the enumeration, and
    /// an emptied shelf is the failure that issue exists against.
    /// </remarks>
    /// <param name="outcome">Which of the three ways a source gives nothing this case is about.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData(SourceOutcome.TemporarilyFailed)]
    [InlineData(SourceOutcome.RateLimited)]
    [InlineData(SourceOutcome.NotConfigured)]
    public async Task AShelfWhoseSourceCouldNotAnswerKeepsEveryByteItHad(SourceOutcome outcome)
    {
        var folder = Folder("refresh-keeps-" + outcome.ToString());
        Remove(folder);
        try
        {
            var shelf = Row(cap: 5);
            var store = Store(folder);
            var document = CatalogueLayout.DocumentName(shelf);

            await RefreshOver(Answering(shelf, SourceAnswer.Answered(new[] { Title("Kept", "1", votes: 1) }, totalCount: 1)), store)
                .RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            var before = store.Read(document);

            var run = await RefreshOver(Answering(shelf, Refusal(outcome)), store)
                .RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(ShelfRefreshOutcome.PreviousKept, Assert.Single(run.Shelves).Outcome);
            Assert.Equal(outcome, run.Shelves[0].SourceOutcome);
            Assert.Equal(before, store.Read(document));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A shelf that is turned off is not asked, not stored and not shown as
    /// having failed.
    /// </summary>
    /// <remarks>
    /// #85's fourth condition, and it is a count on the fake source rather than
    /// an assertion about the document. A refresh that asked and then discarded
    /// the answer would leave the same empty directory and would still have
    /// spent a request against a budget this plugin does not own.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AShelfThatIsTurnedOffIsNotAskedAndNotStored()
    {
        var folder = Folder("refresh-turned-off");
        Remove(folder);
        try
        {
            var shelf = Row(cap: 5) with { Enabled = false };
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);

            source.Answer(Row(cap: 5).Ask(), SourceAnswer.Answered(new[] { Title("Never asked for", "1", votes: 1) }, totalCount: 1));

            var store = Store(folder);
            var run = await RefreshOver(source, store).RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Empty(source.Asked);
            Assert.Equal(ShelfRefreshOutcome.TurnedOff, Assert.Single(run.Shelves).Outcome);
            Assert.Equal(SourceOutcome.None, run.Shelves[0].SourceOutcome);
            Assert.Null(store.Read(CatalogueLayout.DocumentName(shelf)));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A run reports where it has got to, ending at a hundred.
    /// </summary>
    /// <remarks>
    /// #87's fourth condition. What a server draws from this is a bar, and what
    /// this asserts is that the bar moves per shelf and arrives: a run that only
    /// ever reported nought is the apparently hung refresh the condition is
    /// written against.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARunSaysHowFarAlongItIs()
    {
        var folder = Folder("refresh-progress");
        Remove(folder);
        try
        {
            var shelves = new[] { Row(cap: 1), Row(cap: 1, ShelfQuestion.Popular) };
            var reported = new List<double>();

            await RefreshOver(new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb), Store(folder))
                .RunAsync(shelves, new Progress(reported), CancellationToken.None);

            Assert.Equal(0d, reported[0]);
            Assert.Equal(100d, reported[^1]);
            Assert.Contains(50d, reported);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A second run asked for while one is going is refused rather than queued.
    /// </summary>
    /// <remarks>
    /// #87's third condition. The second start is made from inside the first
    /// run's own fetch, so there is no timing in the assertion: the run that
    /// holds the refresh is demonstrably still going, because it is what is
    /// asking.
    ///
    /// What it asserts is the pair. A refused start says so rather than
    /// answering with an empty run, because an empty run and a run that never
    /// happened are what an operator pressing the button in #88 has to be able
    /// to tell apart.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASecondRunIsNotStartedWhileOneIsGoing()
    {
        var folder = Folder("refresh-overlap");
        Remove(folder);
        try
        {
            CatalogueRefresh? refresh = null;
            RefreshRun? second = null;
            var asking = false;
            var shelf = Row(cap: 1);

            var source = new SourceThatRunsSomethingWhileItAnswers(
                MetadataSource.Tmdb,
                async () =>
                {
                    // Once, so that a refresh whose gate has been taken out fails
                    // this test's assertion rather than recursing until the host
                    // dies. A crash proves the gate is load bearing and says
                    // nothing about what it should have answered.
                    if (!asking)
                    {
                        asking = true;
                        second = await refresh!.RunAsync(new[] { shelf }, progress: null, CancellationToken.None).ConfigureAwait(false);
                    }

                    return SourceAnswer.Answered(Array.Empty<DiscoverTitle>(), totalCount: 0);
                });

            refresh = RefreshOver(source, Store(folder));

            var first = await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.True(first.Started);
            Assert.NotNull(second);
            Assert.False(second!.Started);
            Assert.Empty(second.Shelves);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A run stopped part way through leaves the shelves it did not reach
    /// exactly as they were.
    /// </summary>
    /// <remarks>
    /// #87's fifth condition. The first shelf is refreshed and the second is
    /// not reached, and what is asserted about the second is its bytes rather
    /// than its outcome, for the reason recorded on the failure case above.
    ///
    /// The run is stopped from inside the first shelf's fetch, which is where a
    /// server's cancellation arrives from the point of view of this class, and
    /// it costs no wait.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARunStoppedPartWayThroughLeavesWhatItDidNotReach()
    {
        var folder = Folder("refresh-cancelled");
        Remove(folder);
        try
        {
            var first = Row(cap: 5);
            var second = Row(cap: 5, ShelfQuestion.Popular);
            var store = Store(folder);
            var secondDocument = CatalogueLayout.DocumentName(second);

            await RefreshOver(Answering(second, SourceAnswer.Answered(new[] { Title("Kept", "1", votes: 1) }, totalCount: 1)), store)
                .RunAsync(new[] { second }, progress: null, CancellationToken.None);

            var before = store.Read(secondDocument);

            using var stopping = new CancellationTokenSource();

            var source = new SourceThatRunsSomethingWhileItAnswers(
                MetadataSource.Tmdb,
                async () =>
                {
                    await stopping.CancelAsync().ConfigureAwait(false);
                    return SourceAnswer.Answered(new[] { Title("Fetched before the stop", "2", votes: 1) }, totalCount: 1);
                });

            var run = await RefreshOver(source, store).RunAsync(new[] { first, second }, progress: null, stopping.Token);

            Assert.True(run.Cancelled);
            Assert.Equal(ShelfRefreshOutcome.Refreshed, run.Shelves[0].Outcome);
            Assert.Equal(ShelfRefreshOutcome.Cancelled, run.Shelves[1].Outcome);
            Assert.Equal(before, store.Read(secondDocument));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A run stopped before it began asks nobody and writes nothing.
    /// </summary>
    /// <remarks>
    /// The boundary the case above does not reach. A refresh that checked its
    /// token only after the first fetch would spend a request against a source's
    /// budget on a run the operator had already stopped, and every assertion in
    /// the case above would still pass.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARunStoppedBeforeItBeganAsksNobody()
    {
        var folder = Folder("refresh-stopped-first");
        Remove(folder);
        try
        {
            var shelf = Row(cap: 5);
            var store = Store(folder);

            // A source that ignores the token, because what is being asserted is
            // that the refresh checked its own. A fake that refused a cancelled
            // call would leave the same empty record whether or not the caller
            // had looked, which is a test that passes for the wrong reason.
            var source = new SourceThatRunsSomethingWhileItAnswers(
                MetadataSource.Tmdb,
                () => Task.FromResult(SourceAnswer.Answered(Array.Empty<DiscoverTitle>(), totalCount: 0)));

            using var stopped = new CancellationTokenSource();
            await stopped.CancelAsync();

            var run = await RefreshOver(source, store).RunAsync(new[] { shelf }, progress: null, stopped.Token);

            Assert.True(run.Cancelled);
            Assert.Equal(ShelfRefreshOutcome.Cancelled, Assert.Single(run.Shelves).Outcome);
            Assert.Empty(source.Asked);
            Assert.Null(store.Read(CatalogueLayout.DocumentName(shelf)));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A shelf naming a source this server is not set up to ask keeps what it
    /// had, and says which of the four answers that is.
    /// </summary>
    /// <remarks>
    /// The state of every server before an operator has configured anything, so
    /// it is the case a first install is in rather than an edge. It is reported
    /// as the source's own "not set up" answer because that is what it is from
    /// the shelf's side, and #63 turns on an operator being able to tell it from
    /// a shelf that is genuinely empty.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AShelfWithNoSourceOnThisServerKeepsWhatItHad()
    {
        var folder = Folder("refresh-no-source");
        Remove(folder);
        try
        {
            var shelf = Row(cap: 5);
            var store = Store(folder);

            var run = await new CatalogueRefresh(
                Array.Empty<IMetadataSource>(),
                store,
                new ClockATestAdvances(_fetchedAt),
                new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>())
                .RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(ShelfRefreshOutcome.PreviousKept, Assert.Single(run.Shelves).Outcome);
            Assert.Equal(SourceOutcome.NotConfigured, run.Shelves[0].SourceOutcome);
            Assert.Null(store.Read(CatalogueLayout.DocumentName(shelf)));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A run is timed by the clock this plugin reads rather than by the wall.
    /// </summary>
    /// <remarks>
    /// The assertion that makes the injected clock load-bearing here rather than
    /// decorative. A run timed by the wall would report a duration a test cannot
    /// state, and <c>no-wall-clock</c> refuses the direct read that would
    /// produce one.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARunIsTimedByTheClockThisPluginReads()
    {
        var folder = Folder("refresh-timed");
        Remove(folder);
        try
        {
            var clock = new ClockATestAdvances(_fetchedAt);
            var shelf = Row(cap: 1);

            var source = new SourceThatRunsSomethingWhileItAnswers(
                MetadataSource.Tmdb,
                () =>
                {
                    clock.Advance(TimeSpan.FromMinutes(4));
                    return Task.FromResult(SourceAnswer.Answered(Array.Empty<DiscoverTitle>(), totalCount: 0));
                });

            var run = await new CatalogueRefresh(
                new[] { source },
                Store(folder),
                clock,
                new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>())
                .RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            Assert.Equal(_fetchedAt, run.StartedAt);
            Assert.Equal(TimeSpan.FromMinutes(4), run.FinishedAt - run.StartedAt);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Nothing that could not be run is admitted.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task WhatCannotBeRunIsRefused()
    {
        var folder = Folder("refresh-refusals");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var clock = new ClockATestAdvances(_fetchedAt);
            var log = new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>();

            Assert.Throws<ArgumentNullException>(() => new CatalogueRefresh(null!, store, clock, log));
            Assert.Throws<ArgumentNullException>(() => new CatalogueRefresh(Array.Empty<IMetadataSource>(), null!, clock, log));
            Assert.Throws<ArgumentNullException>(() => new CatalogueRefresh(Array.Empty<IMetadataSource>(), store, null!, log));
            Assert.Throws<ArgumentNullException>(() => new CatalogueRefresh(Array.Empty<IMetadataSource>(), store, clock, null!));
            Assert.Throws<ArgumentNullException>(() => new CatalogueRefresh(new IMetadataSource[] { null! }, store, clock, log));

            var refresh = RefreshOver(new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb), store);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => refresh.RunAsync(null!, progress: null, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => refresh.RunAsync(new Shelf[] { null! }, progress: null, CancellationToken.None));
        }
        finally
        {
            Remove(folder);
        }
    }

    private static Shelf Row(int cap, ShelfQuestion question = ShelfQuestion.Trending) => new Shelf
    {
        DisplayName = "A row of films",
        Question = question,
        Kind = DiscoverTitleKind.Movie,
        Source = MetadataSource.Tmdb,
        Cap = cap
    };

    private static DiscoverTitle Title(string name, string identifier, int votes) => new DiscoverTitle
    {
        Kind = DiscoverTitleKind.Movie,
        Name = name,
        VoteCount = votes,
        FetchedAt = _fetchedAt,
        Identity = new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Tmdb, identifier)
        })
    };

    private static SourceAnswer Refusal(SourceOutcome outcome) => outcome switch
    {
        SourceOutcome.RateLimited => SourceAnswer.RateLimited(TimeSpan.FromMinutes(1), "too many requests"),
        SourceOutcome.TemporarilyFailed => SourceAnswer.TemporarilyFailed("the source is having a bad ten minutes"),
        SourceOutcome.NotConfigured => SourceAnswer.NotConfigured(),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Only the three ways a source gives nothing are refusals.")
    };

    private static SourceThatAnswersFromWhatATestGaveIt Answering(Shelf shelf, SourceAnswer answer)
    {
        var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
        source.Answer(shelf.Ask(), answer);
        return source;
    }

    private static CatalogueRefresh RefreshOver(IMetadataSource source, CatalogueDocumentStore store) =>
        new CatalogueRefresh(
            new[] { source },
            store,
            new ClockATestAdvances(_fetchedAt),
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

    /// <summary>
    /// A progress reporter that keeps what it was told, in order.
    /// </summary>
    /// <remarks>
    /// Hand written rather than <c>System.Progress&lt;T&gt;</c>, which posts to
    /// a synchronisation context and would make what this test observes depend
    /// on which thread the run happened to be on.
    /// </remarks>
    private sealed class Progress : IProgress<double>
    {
        private readonly List<double> _reported;

        public Progress(List<double> reported)
        {
            _reported = reported;
        }

        public void Report(double value) => _reported.Add(value);
    }
}
