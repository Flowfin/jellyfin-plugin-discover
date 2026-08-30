using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Jellyfin.Plugin.Template.Time;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// The one place this plugin speaks the server's scheduler vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// #87's first condition. The refresh is a task the server owns rather than a
/// timer of this plugin's, so an operator sees it where they see every other
/// scheduled task, can move it off a time when the server is busy, can watch it
/// run and can stop it. A timer of the plugin's own gets none of that and is
/// invisible when it stops.
/// </para>
/// <para>
/// It decides nothing. What a run does is <see cref="CatalogueRefresh"/>, which
/// is written in this plugin's own words and is testable with no server behind
/// it; this class turns the server's progress reporter and cancellation token
/// into that call and turns what came back into a line an operator can read.
/// It is the same seam <see cref="Surface.DiscoverSurfaceAdapter"/> stands on,
/// for the same reason.
/// </para>
/// <para>
/// The refresh is composed here rather than in the container, and that is a
/// consequence of where the catalogue lives rather than a preference. The
/// directory is under the folder the server derived for this plugin, which the
/// base plugin class computes in its own constructor and exposes on the plugin
/// instance; nothing hands that path to the container, so a registration for
/// the store would have to read the same static this does and would do it while
/// the container is being built rather than when a run starts. Composing it on
/// the first run also keeps one refresh for the life of the server, which is
/// what makes the overlap gate mean anything: a refresh built per run would
/// have a gate each, and two of them do not exclude each other.
/// </para>
/// <para>
/// A server with no plugin instance is not one this task refuses loudly. It
/// says so and does nothing, because the only way to be here without one is a
/// composition nobody has built yet, and throwing would put a red task in an
/// operator's dashboard for a state they cannot act on.
/// </para>
/// </remarks>
public sealed class DiscoverRefreshTask : IScheduledTask
{
    /// <summary>
    /// The name the server stores this task's schedule and history under.
    /// </summary>
    /// <remarks>
    /// Fixed forever from the first release. The server keeps an operator's
    /// triggers and this task's last-run record against this string, so moving
    /// it silently drops both and hands the operator a task that has never run
    /// and is on its default schedule again. That is the same hazard #107 names
    /// for the plugin's own identifier, one register down.
    /// </remarks>
    public const string TaskKey = "DiscoverCatalogueRefresh";

    private readonly IReadOnlyList<IMetadataSource> _sources;
    private readonly IClock _clock;
    private readonly ILogger<DiscoverRefreshTask> _logger;
    private readonly ILogger<CatalogueRefresh>? _refreshLogger;
    private readonly ILogger<CatalogueDocumentStore>? _storeLogger;
    private readonly object _composing = new object();

    private readonly IReadOnlyList<Shelf>? _shelves;

    private CatalogueRefresh? _refresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoverRefreshTask"/> class,
    /// composing its refresh out of the plugin's own data folder when it first runs.
    /// </summary>
    /// <param name="sources">Every source this server is set up to ask, which is what the container holds.</param>
    /// <param name="clock">The clock a run is timed by.</param>
    /// <param name="logger">Where this task says what it did.</param>
    /// <param name="refreshLogger">The logger the refresh writes through.</param>
    /// <param name="storeLogger">The logger the catalogue store writes through.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any argument is null, or when the sources hold a null.
    /// </exception>
    /// <remarks>
    /// The two loggers for other types are here rather than resolved later
    /// because this class is where those types are constructed, and a factory
    /// resolved out of the container at run time would be the container reached
    /// into after it was built. An empty set of sources is the ordinary state of
    /// a server nobody has configured: nothing registers an adapter today, so
    /// this is what a real server hands over.
    /// </remarks>
    public DiscoverRefreshTask(
        IEnumerable<IMetadataSource> sources,
        IClock clock,
        ILogger<DiscoverRefreshTask> logger,
        ILogger<CatalogueRefresh> refreshLogger,
        ILogger<CatalogueDocumentStore> storeLogger)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(refreshLogger);
        ArgumentNullException.ThrowIfNull(storeLogger);

        var taken = new List<IMetadataSource>();

        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(sources));
            taken.Add(source);
        }

        _sources = taken;
        _clock = clock;
        _logger = logger;
        _refreshLogger = refreshLogger;
        _storeLogger = storeLogger;
    }

    private DiscoverRefreshTask(CatalogueRefresh refresh, IReadOnlyList<Shelf> shelves, ILogger<DiscoverRefreshTask> logger)
    {
        _sources = Array.Empty<IMetadataSource>();
        _clock = new NeverAskedClock();
        _logger = logger;
        _refresh = refresh;
        _shelves = shelves;
    }

    /// <inheritdoc />
    /// <remarks>
    /// What an operator reads in the scheduled tasks list, so it says what the
    /// task does rather than naming the plugin twice: the server already groups
    /// it under <see cref="Category"/>.
    /// </remarks>
    public string Name => "Refresh the discover catalogue";

    /// <inheritdoc />
    public string Key => TaskKey;

    /// <inheritdoc />
    /// <remarks>
    /// It says what the run costs and what it does not do, because those are the
    /// two things an operator deciding when to schedule it needs and neither is
    /// derivable from the name. What it does not do is add anything to the
    /// library: the catalogue is this plugin's own directory.
    /// </remarks>
    public string Description =>
        "Asks each shelf's metadata source for its titles and stores them in this plugin's own catalogue. "
        + "One request per shelf, against the source's budget rather than the server's. "
        + "A shelf whose source cannot answer keeps what it already had.";

    /// <inheritdoc />
    /// <remarks>
    /// The plugin's own name, which is what the server groups a plugin's tasks
    /// under in the dashboard.
    /// </remarks>
    public string Category => "Discover";

    /// <summary>
    /// Composes a task over a refresh and a set of shelves somebody else built.
    /// </summary>
    /// <param name="refresh">The refresh a run goes through.</param>
    /// <param name="shelves">The shelves a run takes.</param>
    /// <param name="logger">Where this task says what it did.</param>
    /// <returns>The task.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <remarks>
    /// A named constructor rather than a second public constructor, so the
    /// container has exactly one to choose between and the choice is not made by
    /// which arguments it happened to be able to resolve.
    ///
    /// What uses it is a test, and both of the things it replaces are why. A
    /// refresh handed over here writes into a directory of its own rather than
    /// into the folder a real install would use, and shelves handed over here
    /// come from the caller rather than from the plugin instance, which is a
    /// static shared by every test in a run and therefore not a thing one test
    /// can hold still. What the server's own path builds instead is
    /// <see cref="ShelvesFor"/>, which is asserted on its own.
    /// </remarks>
    public static DiscoverRefreshTask Over(
        CatalogueRefresh refresh,
        IReadOnlyList<Shelf> shelves,
        ILogger<DiscoverRefreshTask> logger)
    {
        ArgumentNullException.ThrowIfNull(refresh);
        ArgumentNullException.ThrowIfNull(shelves);
        ArgumentNullException.ThrowIfNull(logger);

        return new DiscoverRefreshTask(refresh, shelves, logger);
    }

    /// <summary>
    /// The shelves a run takes, at the bound a configuration carries.
    /// </summary>
    /// <param name="configuration">What the operator saved, or null where there is none to read.</param>
    /// <returns>The shipped set at that bound, or null where there is no configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown by <see cref="Configuration.CatalogueBounds.Of"/> when the pair
    /// the document carries contradicts itself.
    /// </exception>
    /// <remarks>
    /// Public and separate from <see cref="ExecuteAsync"/> because it is the
    /// only part of the server's own path that can be asserted without a server:
    /// what it reads from is a configuration handed in rather than the plugin
    /// instance, which the run itself has to go to and which no test can hold
    /// still. What it must not become is a second copy of either half - the set
    /// is <see cref="ShippedShelves"/>'s and the number is #58's - and that is
    /// what the assertion on it is about.
    /// </remarks>
    public static IReadOnlyList<Shelf>? ShelvesFor(Configuration.PluginConfiguration? configuration) =>
        configuration is null
            ? null
            : ShippedShelves.Bounded(configuration.Bounds().TitlesPerShelf);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// One interval trigger of a day. #87's second condition asks for the
    /// cadence to be derived from the source's request limit and the shelf
    /// count, and that derivation does not bound anything: six requests against
    /// a ceiling the source states as roughly forty per second permits several
    /// refreshes a second, which is not a cadence. That reading is recorded on
    /// the issue and this default is not it.
    /// </para>
    /// <para>
    /// What it is derived from instead is what a shorter one would buy. Two of
    /// the shipped shelves ask their source for a weekly window, so a refresh
    /// far inside that window returns mostly the previous answer; the server
    /// caches what a surface returned for three hours of its own, so a cadence
    /// under that is spent on something no user can see; and the retention is
    /// ninety days, so a daily refresh keeps every stored record two orders of
    /// magnitude away from its expiry. A day is the longest cadence that
    /// refreshes a weekly window several times over and the shortest that is
    /// not paying for a difference behind a cache.
    /// </para>
    /// <para>
    /// A default rather than a decision about the cadence. The trigger is one an
    /// operator can move or replace in the dashboard, which is the whole reason
    /// this is the server's scheduler rather than a timer of this plugin's.
    /// </para>
    /// </remarks>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
    [
        new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromDays(1).Ticks
        }
    ];

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The shelves are the shipped set at the bound the configuration carries,
    /// which is #58's number. Nothing here chooses how many titles a shelf holds
    /// and nothing here holds a second copy of the set.
    /// </para>
    /// <para>
    /// Cancellation is rethrown after the run has been reported, so the server
    /// records the task as cancelled rather than as completed while the log
    /// still says which shelves were reached. Swallowing it would leave an
    /// operator who stopped a refresh looking at a task that says it finished.
    /// </para>
    /// </remarks>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var refresh = Composed();

        if (refresh is null)
        {
            _logger.LogWarning("The discover catalogue refresh has no plugin instance to read a data folder from, so it did nothing.");

            return;
        }

        var shelves = _shelves ?? ShelvesFor(Plugin.Instance?.Configuration);

        if (shelves is null)
        {
            _logger.LogWarning("The discover catalogue refresh has no configuration to read a bound from, so it did nothing.");

            return;
        }

        var run = await refresh.RunAsync(shelves, progress, cancellationToken).ConfigureAwait(false);

        if (!run.Started)
        {
            _logger.LogInformation("A discover catalogue refresh was already running, so this one did not start.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// The refresh this task runs through, built once and kept.
    /// </summary>
    /// <returns>The refresh, or null where there is no plugin instance to read a data folder from.</returns>
    private CatalogueRefresh? Composed()
    {
        if (_refresh is { } already)
        {
            return already;
        }

        lock (_composing)
        {
            if (_refresh is { } built)
            {
                return built;
            }

            var dataFolderPath = Plugin.Instance?.DataFolderPath;

            if (string.IsNullOrWhiteSpace(dataFolderPath) || _refreshLogger is null || _storeLogger is null)
            {
                return null;
            }

            _refresh = new CatalogueRefresh(
                _sources,
                new CatalogueDocumentStore(new CatalogueDirectory(dataFolderPath), _storeLogger),
                _clock,
                _refreshLogger);

            return _refresh;
        }
    }

    /// <summary>
    /// The clock a task composed over somebody else's refresh never reads.
    /// </summary>
    /// <remarks>
    /// The refresh holds the clock a run is timed by, so a task handed one
    /// already built has no use for a second. Refusing rather than answering
    /// keeps that true: a later change that starts reading the clock here fails
    /// loudly instead of silently timing something by a clock no test controls.
    /// </remarks>
    private sealed class NeverAskedClock : IClock
    {
        /// <inheritdoc />
        public DateTimeOffset UtcNow => throw new InvalidOperationException(
            "A refresh task composed over an existing refresh does not read a clock. The refresh it was handed holds the one a run is timed by.");
    }
}
