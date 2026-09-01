using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Configuration;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What the server is offered when it collects scheduled tasks, and what
/// running one does.
/// </summary>
/// <remarks>
/// The task is the thin half of #87. What a run does is asserted in
/// <see cref="CatalogueRefreshTests"/>, against the refresh that decides it;
/// what is asserted here is the half that only exists because the server is on
/// the other side of it - the identity an operator's dashboard keys on, the
/// default schedule, the registration, and that a run reaches the refresh with
/// the shipped shelves at the configured bound.
/// </remarks>
public class DiscoverRefreshTaskTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _now =
        new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The server is offered exactly one scheduled task, under the interface it
    /// collects them by.
    /// </summary>
    /// <remarks>
    /// #87's sixth condition, in its registration half. The server builds its
    /// task list by asking the container for every implementation of the
    /// interface, so a registration under the concrete type is a task nobody
    /// ever sees and two registrations are two entries in an operator's
    /// dashboard that fetch the same shelves twice.
    ///
    /// The interface is derived from the task rather than named in the
    /// assertion, so a change of interface is still counted rather than leaving
    /// a test that passes while looking for a type nothing implements.
    /// </remarks>
    [Fact]
    public void ExactlyOneScheduledTaskIsOfferedToTheServer()
    {
        var services = new ServiceCollection();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        new PluginServiceRegistrator().RegisterServices(services, new ServerApplicationHostThatRefusesEveryCall());

        var offered = services
            .Where(descriptor => descriptor.ServiceType.IsAssignableFrom(typeof(DiscoverRefreshTask)))
            .Where(descriptor => descriptor.ServiceType != typeof(DiscoverRefreshTask))
            .ToArray();

        Assert.Single(offered);
        Assert.Equal(typeof(DiscoverRefreshTask), offered[0].ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, offered[0].Lifetime);
    }

    /// <summary>
    /// The task says what it is to an operator, and its key is the literal it
    /// was released with.
    /// </summary>
    /// <remarks>
    /// The key is asserted against a literal written here rather than against
    /// the constant, because the constant is the thing that could move. The
    /// server keeps an operator's own triggers and this task's history against
    /// that string, so a rename silently drops both and hands back a task that
    /// has never run and is on its default schedule again.
    /// </remarks>
    [Fact]
    public void TheTaskNamesItselfWhereAnOperatorReadsIt()
    {
        var task = Composed();

        Assert.Equal("DiscoverCatalogueRefresh", task.Key, StringComparer.Ordinal);
        Assert.Equal("Discover", task.Category, StringComparer.Ordinal);
        Assert.NotEmpty(task.Name);
        Assert.NotEmpty(task.Description);
        Assert.NotEqual(task.Name, task.Description, StringComparer.Ordinal);
    }

    /// <summary>
    /// A fresh install refreshes once a day, on a trigger an operator can move.
    /// </summary>
    /// <remarks>
    /// #87's first condition, in its default-schedule half. The interval is
    /// asserted as a duration rather than as a tick count, because a tick count
    /// in an assertion is a number a reader cannot check against the sentence
    /// that argues for it.
    /// </remarks>
    [Fact]
    public void TheDefaultScheduleIsOnceADay()
    {
        var triggers = Composed().GetDefaultTriggers().ToArray();

        Assert.Single(triggers);
        Assert.Equal(TaskTriggerInfoType.IntervalTrigger, triggers[0].Type);
        Assert.Equal(TimeSpan.FromDays(1), TimeSpan.FromTicks(triggers[0].IntervalTicks!.Value));
    }

    /// <summary>
    /// The shelves a run takes are the shipped set, at the bound the
    /// configuration carries.
    /// </summary>
    /// <remarks>
    /// The assertion that the task holds no second copy of either. A task with a
    /// shelf list of its own would pass every other test here and would drift
    /// from <c>ShippedShelves</c> the day a shelf is added; a task with a number
    /// of its own would ask a source for more titles than an operator agreed to
    /// hold, which is #58's whole subject.
    ///
    /// It is asserted on the derivation rather than through a run, because the
    /// run reads the plugin instance and that is a static every test in the
    /// suite shares. A test that constructed a plugin and then read the one the
    /// static happened to be holding would pass or fail on which other test was
    /// running beside it.
    /// </remarks>
    [Fact]
    public void TheShelvesARunTakesAreTheShippedSetAtTheConfiguredBound()
    {
        var configuration = new PluginConfiguration { MaximumTitlesPerShelf = 7, MaximumTitlesAcrossAllShelves = 42 };

        Assert.Equal(ShippedShelves.Bounded(7), DiscoverRefreshTask.ShelvesFor(configuration));
        Assert.Null(DiscoverRefreshTask.ShelvesFor(null));
    }

    /// <summary>
    /// Running the task asks every shelf it was given, and reports its way to a
    /// hundred.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARunAsksEveryShelfItWasGiven()
    {
        var folder = Folder("task-run");
        Remove(folder);
        try
        {
            var shelves = ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf);
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
            var reported = new List<double>();

            await DiscoverRefreshTask.Over(
                new CatalogueRefresh(
                    new[] { source },
                    new CatalogueDocumentStore(
                        new CatalogueDirectory(folder),
                        new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>()),
                    null,
                    new ClockATestAdvances(_now),
                    new PauseATestWatches(),
                    new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>()),
                shelves,
                new LoggerThatRecordsWhatIsWritten<DiscoverRefreshTask>())
                .ExecuteAsync(new Progress(reported), CancellationToken.None);

            Assert.Equal(
                shelves.Select(shelf => shelf.Ask()).ToArray(),
                source.Asked.ToArray());
            Assert.Equal(100d, reported[^1]);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A run the operator stopped is reported to the server as stopped.
    /// </summary>
    /// <remarks>
    /// #87's fifth condition where it meets the dashboard. The refresh answers
    /// with a run marked cancelled rather than throwing, because a caller that
    /// wants to know what was reached needs the run; the server's own task
    /// worker reads a cancellation as an exception, so a task that swallowed it
    /// would show an operator who stopped a refresh a task that says it
    /// finished.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARunTheOperatorStoppedIsReportedAsStopped()
    {
        var folder = Folder("task-stopped");
        Remove(folder);
        try
        {
            using var stopped = new CancellationTokenSource();
            await stopped.CancelAsync();

            var task = DiscoverRefreshTask.Over(
                new CatalogueRefresh(
                    Array.Empty<IMetadataSource>(),
                    new CatalogueDocumentStore(
                        new CatalogueDirectory(folder),
                        new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>()),
                    null,
                    new ClockATestAdvances(_now),
                    new PauseATestWatches(),
                    new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>()),
                ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf),
                new LoggerThatRecordsWhatIsWritten<DiscoverRefreshTask>());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => task.ExecuteAsync(new Progress(new List<double>()), stopped.Token));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Nothing that could not be composed is admitted.
    /// </summary>
    [Fact]
    public void WhatCannotBeComposedIsRefused()
    {
        var log = new LoggerThatRecordsWhatIsWritten<DiscoverRefreshTask>();

        var shelves = ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf);
        var refresh = new CatalogueRefresh(
            Array.Empty<IMetadataSource>(),
            new CatalogueDocumentStore(
                new CatalogueDirectory(Folder("task-refusals")),
                new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>()),
            null,
            new ClockATestAdvances(_now),
            new PauseATestWatches(),
            new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>());

        Assert.Throws<ArgumentNullException>(() => DiscoverRefreshTask.Over(null!, shelves, log));
        Assert.Throws<ArgumentNullException>(() => DiscoverRefreshTask.Over(refresh, null!, log));
        Assert.Throws<ArgumentNullException>(() => DiscoverRefreshTask.Over(refresh, shelves, null!));

        Assert.Throws<ArgumentNullException>(() => new DiscoverRefreshTask(
            null!,
            new LibraryThatHoldsWhatATestGaveIt(),
            new ClockATestAdvances(_now),
            log,
            new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>(),
            new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>()));

        Assert.Throws<ArgumentNullException>(() => new DiscoverRefreshTask(
            new IMetadataSource[] { null! },
            new LibraryThatHoldsWhatATestGaveIt(),
            new ClockATestAdvances(_now),
            log,
            new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>(),
            new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>()));

        // #89. The library is not optional on a task the server built: a null
        // one would be a refresh that filtered nothing and that looked, in
        // every log line and every document, exactly like one with nothing to
        // filter.
        Assert.Throws<ArgumentNullException>(() => new DiscoverRefreshTask(
            Array.Empty<IMetadataSource>(),
            null!,
            new ClockATestAdvances(_now),
            log,
            new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>(),
            new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>()));
    }

    private static DiscoverRefreshTask Composed() => new DiscoverRefreshTask(
        Array.Empty<IMetadataSource>(),
        new LibraryThatHoldsWhatATestGaveIt(),
        new ClockATestAdvances(_now),
        new LoggerThatRecordsWhatIsWritten<DiscoverRefreshTask>(),
        new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>(),
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
