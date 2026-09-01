using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Configuration;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What a run does when it has more shelves to fetch than the source's budget
/// allows inside one window.
/// </summary>
/// <remarks>
/// #78's second condition, asserted through a run rather than on
/// <see cref="SourcePace"/>, which <c>SourcePaceTests</c> covers on its own.
/// What is worth asserting here is the part the pace cannot decide: that a run
/// reads it before every request it is about to make, serves the wait it is
/// given, and refreshes every shelf anyway.
///
/// Nothing here spends real time. The pause is the double that records what it
/// was asked for and advances the clock by it, so the instants the run records
/// are the instants it would have recorded on a machine, and the assertions are
/// on what was asked for rather than on how long the runner took.
/// </remarks>
public class ARefreshHoldsToTheSourcesBudgetTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _noon =
        new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The set this plugin ships is larger than one window's budget, so the
    /// pacing is what an ordinary refresh meets rather than a guard for a shelf
    /// count nobody has.
    /// </summary>
    /// <remarks>
    /// Asserted rather than stated in a remark, because it is the sentence that
    /// makes every test below about the ordinary path. A future set that fits
    /// inside the budget reddens this and the reader is told that the pacing has
    /// stopped biting on the shipped configuration rather than finding out from
    /// a source's refusal.
    /// </remarks>
    [Fact]
    public void TheShippedSetIsLargerThanOneWindowsBudget()
    {
        Assert.True(
            ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf).Count > SourcePace.RequestsPerWindow,
            "The shipped shelf set no longer exceeds one window's budget, so nothing in an ordinary refresh reaches the pacing.");
    }

    /// <summary>
    /// A run of the shipped set holds off once, for exactly one window, and
    /// refreshes every shelf.
    /// </summary>
    /// <remarks>
    /// The three halves of the condition in one assertion set. That the wait
    /// happened at all is the pacing; that there is one of it rather than one
    /// per shelf is the budget being a budget rather than a fixed gap; and that
    /// every shelf came back refreshed is the thing a run that simply dropped
    /// the shelves past the budget would fail.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARunOfTheShippedSetHoldsOffOnceAndRefreshesEveryShelf()
    {
        var folder = Folder("budget-whole-set");
        Remove(folder);
        try
        {
            var shelves = ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf);
            var source = Answering(shelves);
            var clock = new ClockATestAdvances(_noon);
            var pause = new PauseATestWatches(clock);

            var run = await Refresh(source, Store(folder), clock, pause)
                .RunAsync(shelves, progress: null, CancellationToken.None);

            Assert.All(run.Shelves, shelf => Assert.Equal(ShelfRefreshOutcome.Refreshed, shelf.Outcome));
            Assert.Equal(shelves.Count, source.Asked.Count);

            Assert.Equal(SourcePace.Window, Assert.Single(pause.Waits));
            Assert.Equal(_noon + SourcePace.Window, clock.UtcNow);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The wait falls after the budget is spent and not before it.
    /// </summary>
    /// <remarks>
    /// Where a run holds off is not visible in the total, so this counts what
    /// the source had been asked at the moment the wait was served. A run that
    /// held off before its first request would spend a window on a budget
    /// nobody had touched; one that held off after every request would spend
    /// several.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task TheWaitFallsAfterTheBudgetIsSpentAndNotBefore()
    {
        var folder = Folder("budget-when");
        Remove(folder);
        try
        {
            var shelves = ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf);
            var source = Answering(shelves);
            var clock = new ClockATestAdvances(_noon);
            var pause = new PauseThatCountsWhatTheSourceWasAskedFirst(source, clock);

            await Refresh(source, Store(folder), clock, pause)
                .RunAsync(shelves, progress: null, CancellationToken.None);

            Assert.Equal(SourcePace.RequestsPerWindow, Assert.Single(pause.AskedByThen));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A run that stays inside the budget holds off for nothing.
    /// </summary>
    /// <remarks>
    /// The other direction, and the one that keeps the pacing from being a cost
    /// every refresh pays. A pace read as "wait unless nothing has been asked"
    /// passes the test above and fails this one.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARunInsideTheBudgetHoldsOffForNothing()
    {
        var folder = Folder("budget-inside");
        Remove(folder);
        try
        {
            var shelves = ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf)
                .Take(SourcePace.RequestsPerWindow)
                .ToArray();

            var source = Answering(shelves);
            var clock = new ClockATestAdvances(_noon);
            var pause = new PauseATestWatches(clock);

            var run = await Refresh(source, Store(folder), clock, pause)
                .RunAsync(shelves, progress: null, CancellationToken.None);

            Assert.All(run.Shelves, shelf => Assert.Equal(ShelfRefreshOutcome.Refreshed, shelf.Outcome));
            Assert.Empty(pause.Waits);
            Assert.Equal(_noon, clock.UtcNow);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A shelf whose source is resting costs no wait, because it costs no
    /// request.
    /// </summary>
    /// <remarks>
    /// The order the two guards are read in. A run that paced before it read the
    /// rest would hold itself off for requests it is not going to make, so a
    /// source this plugin has given up on would slow every later refresh down
    /// for as long as the rest lasts. The source here refuses the first shelf
    /// and is left alone for the rest of the run, so five of the six shelves are
    /// never asked.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AShelfWhoseSourceIsRestingCostsNoWait()
    {
        var folder = Folder("budget-resting");
        Remove(folder);
        try
        {
            var shelves = ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf);
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);

            foreach (var shelf in shelves)
            {
                source.Answer(shelf.Ask(), SourceAnswer.RateLimited(TimeSpan.FromMinutes(30), "too many requests"));
            }

            var clock = new ClockATestAdvances(_noon);
            var pause = new PauseATestWatches(clock);

            var run = await Refresh(source, Store(folder), clock, pause)
                .RunAsync(shelves, progress: null, CancellationToken.None);

            Assert.All(run.Shelves, shelf => Assert.Equal(ShelfRefreshOutcome.PreviousKept, shelf.Outcome));
            Assert.Single(source.Asked);
            Assert.Empty(pause.Waits);
        }
        finally
        {
            Remove(folder);
        }
    }

    private static SourceThatAnswersFromWhatATestGaveIt Answering(System.Collections.Generic.IReadOnlyList<Shelf> shelves)
    {
        var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);

        for (var index = 0; index < shelves.Count; index++)
        {
            var shelf = shelves[index];

            source.Answer(
                shelf.Ask(),
                SourceAnswer.Answered(new[] { Title(shelf.DisplayName, index.ToString(System.Globalization.CultureInfo.InvariantCulture), shelf.Kind) }, totalCount: 1));
        }

        return source;
    }

    private static DiscoverTitle Title(string name, string identifier, DiscoverTitleKind kind) => new DiscoverTitle
    {
        Kind = kind,
        Name = name,
        VoteCount = 10,
        FetchedAt = _noon,
        Identity = new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Tmdb, identifier)
        })
    };

    private static CatalogueRefresh Refresh(
        IMetadataSource source,
        CatalogueDocumentStore store,
        ClockATestAdvances clock,
        IPause pause) =>
        new CatalogueRefresh(
            new[] { source },
            store,
            null,
            clock,
            pause,
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
    /// A pause that writes down how many requests the source had already been
    /// asked each time a run held off.
    /// </summary>
    /// <remarks>
    /// What it is for is the one thing the recorded durations cannot say, which
    /// is where in the run a wait fell. It advances the clock as
    /// <see cref="PauseATestWatches"/> does, because a run whose clock stood
    /// still would be held off again for the same request.
    /// </remarks>
    private sealed class PauseThatCountsWhatTheSourceWasAskedFirst : IPause
    {
        private readonly SourceThatAnswersFromWhatATestGaveIt _source;

        private readonly ClockATestAdvances _clock;

        private readonly System.Collections.Generic.List<int> _askedByThen =
            new System.Collections.Generic.List<int>();

        public PauseThatCountsWhatTheSourceWasAskedFirst(
            SourceThatAnswersFromWhatATestGaveIt source,
            ClockATestAdvances clock)
        {
            _source = source;
            _clock = clock;
        }

        public System.Collections.Generic.IReadOnlyList<int> AskedByThen => _askedByThen;

        public Task ForAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _askedByThen.Add(_source.Asked.Count);
            _clock.Advance(duration);

            return Task.CompletedTask;
        }
    }
}
