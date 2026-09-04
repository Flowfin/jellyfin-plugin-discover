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
    /// A fresh install refreshes once a day at four in the morning, on a trigger
    /// an operator can move.
    /// </summary>
    /// <remarks>
    /// #87's first and second conditions, in their default-schedule half. The
    /// hour is asserted as a duration rather than as a tick count, because a
    /// tick count in an assertion is a number a reader cannot check against the
    /// sentence that argues for it.
    ///
    /// The kind is asserted beside the hour, and that is the half a reader
    /// should not skip. An interval trigger of a day and a daily trigger at four
    /// both refresh once a day; only the second says when. Asserting the hour
    /// alone would pass on a trigger that carries no hour at all, because the
    /// property it reads is nullable and an interval trigger leaves it unset.
    /// </remarks>
    [Fact]
    public void TheDefaultScheduleIsOnceADayAtFour()
    {
        var triggers = Composed().GetDefaultTriggers().ToArray();

        Assert.Single(triggers);
        Assert.Equal(TaskTriggerInfoType.DailyTrigger, triggers[0].Type);
        Assert.Equal(TimeSpan.FromHours(4), TimeSpan.FromTicks(triggers[0].TimeOfDayTicks!.Value));
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
    /// A configuration with the plugin turned off gives a run no shelves, which
    /// is not the same answer as no configuration.
    /// </summary>
    /// <remarks>
    /// #109's first condition, at the one place the switch is read. The two
    /// answers are kept apart on purpose: null is a run with nothing to read a
    /// bound from, and an empty set is an operator's decision. A switch that
    /// answered null would make the run log that it had no configuration, which
    /// is the wrong sentence for an operator who wrote one.
    /// </remarks>
    [Fact]
    public void ATurnedOffPluginGivesARunNoShelves()
    {
        var off = new PluginConfiguration { Enabled = false };

        var shelves = DiscoverRefreshTask.ShelvesFor(off);

        Assert.NotNull(shelves);
        Assert.Empty(shelves);
        Assert.True(new PluginConfiguration().Enabled, "A fresh configuration has the plugin turned on.");
    }

    /// <summary>
    /// A run while the plugin is turned off asks no source and leaves every byte
    /// the catalogue held where it was.
    /// </summary>
    /// <remarks>
    /// #109's fourth condition in its source half, and its first and third in
    /// the half about the catalogue: the count is the questions the fake source
    /// was asked, taken before and after a run under a configuration that is
    /// off, and the catalogue is compared document by document and byte by byte
    /// across that run. A run that purged what it would not refresh, or that
    /// asked and discarded the answer, reddens here.
    ///
    /// The item half of that condition has no subject: nothing in this plugin
    /// writes an item to the server's library, so a count of item writes would
    /// be a count that cannot move. It is named here rather than asserted.
    ///
    /// The run is driven through <see cref="DiscoverRefreshTask.Over"/> with the
    /// shelves <see cref="DiscoverRefreshTask.ShelvesFor"/> answers for the
    /// configuration, which is the route the server's own path takes minus the
    /// plugin instance, for the reason this class already gives.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARunWhileTurnedOffAsksNoSourceAndKeepsWhatTheCatalogueHolds()
    {
        var folder = Folder("task-turned-off");
        Remove(folder);
        try
        {
            var configuration = new PluginConfiguration();
            var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
            var store = new CatalogueDocumentStore(
                new CatalogueDirectory(folder),
                new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());
            var refresh = new CatalogueRefresh(
                new[] { source },
                store,
                null,
                new ClockATestAdvances(_now),
                new PauseATestWatches(),
                new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>());
            var logger = new LoggerThatRecordsWhatIsWritten<DiscoverRefreshTask>();

            var on = DiscoverRefreshTask.ShelvesFor(configuration);
            Assert.NotNull(on);
            foreach (var shelf in on)
            {
                source.Answer(
                    shelf.Ask(),
                    SourceAnswer.Answered(
                        new[]
                        {
                            new DiscoverTitle
                            {
                                Kind = shelf.Kind,
                                Name = "Held while off",
                                VoteCount = 1,
                                FetchedAt = _now,
                                Identity = new DiscoverTitleIdentity(new[]
                                {
                                    new ProviderIdentifier(MetadataSource.Tmdb, "1")
                                })
                            }
                        },
                        totalCount: 1));
            }

            await DiscoverRefreshTask.Over(refresh, on, logger)
                .ExecuteAsync(new Progress<double>(), CancellationToken.None)
                .ConfigureAwait(true);

            var held = store.DocumentNames().ToDictionary(name => name, name => store.Read(name), StringComparer.Ordinal);
            Assert.NotEmpty(held);
            var asked = source.Asked.Count;
            Assert.NotEqual(0, asked);

            configuration.Enabled = false;
            var off = DiscoverRefreshTask.ShelvesFor(configuration);
            Assert.NotNull(off);

            await DiscoverRefreshTask.Over(refresh, off, logger)
                .ExecuteAsync(new Progress<double>(), CancellationToken.None)
                .ConfigureAwait(true);

            Assert.Equal(asked, source.Asked.Count);
            Assert.Equal(held.Keys.Order(StringComparer.Ordinal), store.DocumentNames().Order(StringComparer.Ordinal));
            foreach (var (name, bytes) in held)
            {
                Assert.Equal(bytes, store.Read(name));
            }
        }
        finally
        {
            Remove(folder);
        }
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
