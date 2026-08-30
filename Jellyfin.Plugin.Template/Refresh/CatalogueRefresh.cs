using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Jellyfin.Plugin.Template.Time;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// One pass over the shelves: ask each one's source, and put what came back
/// where the layout says it goes.
/// </summary>
/// <remarks>
/// <para>
/// #87 is what this exists for, and it is written in this plugin's own
/// vocabulary with no type from the server in it. That is the same seam
/// <see cref="Surface.DiscoverSurface"/> stands on: the thing that decides is
/// testable without a server, and the thing that speaks to the server is a thin
/// class beside it, which here is <see cref="DiscoverRefreshTask"/>.
/// </para>
/// <para>
/// It composes nothing of its own. The question a shelf asks is
/// <see cref="Shelf.Ask"/>, the order and the bound are the shelf's, the
/// document's name is <see cref="CatalogueLayout"/>'s, and the bytes are
/// <see cref="CatalogueDocumentBody"/>'s. A refresh that built its own query,
/// sorted with its own comparer or named its own file would be a second copy of
/// four decisions taken elsewhere, and the drift would show as a shelf holding
/// more titles than it may or a document nothing reads.
/// </para>
/// <para>
/// What it does decide is the order of operations, and there is one rule in it:
/// nothing is written for a shelf whose source did not answer. That is #79's
/// first condition, and it is held by the shape rather than by a branch
/// somebody could take out - the only call to <see cref="CatalogueDocumentStore.Write"/>
/// here is inside the arm that has an answer in hand.
/// </para>
/// <para>
/// A source that throws is a fault rather than an answer. <see cref="IMetadataSource"/>
/// says so of itself: the three ways a source has nothing to give come back as
/// answers precisely so that a refresh can act on them, so an adapter that
/// throws instead has a defect and the exception is left to reach the caller
/// rather than being folded into "this shelf failed". Cancellation is the one
/// exception that is caught, because it is an instruction rather than a fault.
/// </para>
/// </remarks>
public sealed class CatalogueRefresh
{
    private readonly IMetadataSource[] _sources;
    private readonly CatalogueDocumentStore _store;
    private readonly IClock _clock;
    private readonly ILogger<CatalogueRefresh> _logger;
    private readonly Dictionary<string, int> _failuresInARow = new Dictionary<string, int>(StringComparer.Ordinal);

    private int _running;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogueRefresh"/> class.
    /// </summary>
    /// <param name="sources">The sources this server is set up to ask.</param>
    /// <param name="store">Where a shelf's titles are kept.</param>
    /// <param name="clock">The clock a run is timed by.</param>
    /// <param name="logger">Where a run says what it did.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any argument is null, or when the sources hold a null.
    /// </exception>
    /// <remarks>
    /// An empty set of sources is the ordinary state of a server nobody has
    /// configured rather than an error, so it is admitted. What such a run does
    /// is ask nobody and write nothing, and every shelf comes back
    /// <see cref="ShelfRefreshOutcome.PreviousKept"/> with
    /// <see cref="SourceOutcome.NotConfigured"/>, which is what #63 asks an
    /// operator be able to see.
    /// </remarks>
    public CatalogueRefresh(
        IReadOnlyCollection<IMetadataSource> sources,
        CatalogueDocumentStore store,
        IClock clock,
        ILogger<CatalogueRefresh> logger)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        var taken = new List<IMetadataSource>(sources.Count);

        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(sources));
            taken.Add(source);
        }

        _sources = taken.ToArray();
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether a run is going right now.
    /// </summary>
    /// <remarks>
    /// Read rather than acted on. A caller that reads this and then starts a
    /// run has a gap between the two in which somebody else can start one, so
    /// what refuses the second start is <see cref="RunAsync"/> itself and this
    /// is for saying so on a page.
    /// </remarks>
    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>
    /// Asks every shelf's source and writes what came back.
    /// </summary>
    /// <param name="shelves">The shelves to refresh, in the order to take them.</param>
    /// <param name="progress">Where to report how far along the run is, or null.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What the run did, or a declined run where one was already going.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the shelves are null or hold a null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown by <see cref="Shelf.Validated"/> when a shelf could not be asked
    /// for anything.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Two runs cannot overlap, which is #87's third condition, and the gate is
    /// a compare-and-swap rather than a lock. A lock would make the second
    /// caller wait for the first and then run anyway, which is the manual
    /// trigger in #88 queueing a refresh behind the scheduled one instead of
    /// being told that one is already going.
    /// </para>
    /// <para>
    /// Progress is reported per shelf, in the nought-to-a-hundred the server's
    /// scheduler draws. A shelf that is turned off advances it too, because a
    /// bar that stalls on a shelf nobody is fetching reads as a refresh that
    /// hung.
    /// </para>
    /// <para>
    /// Cancellation stops the run between shelves and is honoured inside a
    /// fetch by the source, which is handed the token. What was written before
    /// the stop stays written: those documents hold what a source answered
    /// moments ago, and undoing them would be a refresh that threw away good
    /// data because it was interrupted, which is #79's failure with a different
    /// cause. What #79's fifth condition asks for is that a run in progress
    /// never empties a shelf, and that holds under cancellation for the same
    /// reason it holds under a failure: nothing is written for a shelf whose
    /// source did not answer.
    /// </para>
    /// </remarks>
    public async Task<RefreshRun> RunAsync(
        IReadOnlyList<Shelf> shelves,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shelves);

        if (Interlocked.CompareExchange(ref _running, 1, 0) == 1)
        {
            _logger.LogInformation("A discover catalogue refresh was asked for while one was already running, and was not started a second time.");

            return RefreshRun.Declined(_clock.UtcNow);
        }

        try
        {
            return await PassAsync(shelves, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    /// <summary>
    /// The pass itself, with the gate already held.
    /// </summary>
    /// <param name="shelves">The shelves to refresh.</param>
    /// <param name="progress">Where to report how far along the run is, or null.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What the run did.</returns>
    private async Task<RefreshRun> PassAsync(
        IReadOnlyList<Shelf> shelves,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var startedAt = _clock.UtcNow;
        var results = new List<ShelfRefreshResult>(shelves.Count);
        var cancelled = false;

        progress?.Report(0);

        for (var index = 0; index < shelves.Count; index++)
        {
            var shelf = shelves[index];
            ArgumentNullException.ThrowIfNull(shelf, nameof(shelves));

            shelf.Validated();

            var documentName = CatalogueLayout.DocumentName(shelf);

            if (cancelled || cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                results.Add(ShelfRefreshResult.Cancelled(shelf.DisplayName, documentName));
            }
            else if (!shelf.Enabled)
            {
                results.Add(ShelfRefreshResult.TurnedOff(shelf.DisplayName, documentName));
            }
            else
            {
                try
                {
                    results.Add(await OneShelfAsync(shelf, documentName, cancellationToken).ConfigureAwait(false));
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    results.Add(ShelfRefreshResult.Cancelled(shelf.DisplayName, documentName));
                }
            }

            progress?.Report((index + 1) * 100d / shelves.Count);
        }

        progress?.Report(100);

        var run = RefreshRun.Of(startedAt, _clock.UtcNow, cancelled, results);

        _logger.LogInformation(
            "A discover catalogue refresh took {Shelves} shelves, refreshed {Refreshed}, kept what {Kept} already held, skipped {Off} that are turned off, did not reach {Unreached}, and found {Standing} whose source has now failed more than once in a row.",
            run.Shelves.Count,
            run.Shelves.Count(result => result.Outcome is ShelfRefreshOutcome.Refreshed),
            run.Shelves.Count(result => result.Outcome is ShelfRefreshOutcome.PreviousKept),
            run.Shelves.Count(result => result.Outcome is ShelfRefreshOutcome.TurnedOff),
            run.Shelves.Count(result => result.Outcome is ShelfRefreshOutcome.Cancelled),
            run.Shelves.Count(result => result.ConsecutiveFailures > 1));

        return run;
    }

    /// <summary>
    /// Asks one shelf's source and writes what came back.
    /// </summary>
    /// <param name="shelf">The shelf.</param>
    /// <param name="documentName">The document its titles are kept in.</param>
    /// <param name="cancellationToken">Stops the fetch.</param>
    /// <returns>What happened to this shelf.</returns>
    /// <remarks>
    /// A shelf naming a source this server is not set up to ask is answered
    /// exactly as a configured source that reported it has not been set up,
    /// because that is what it is from the shelf's side and because
    /// <see cref="Shelf.ValidatedAgainst"/> is where such a shelf is refused, at
    /// the moment a configuration is saved rather than in the middle of a run
    /// nobody is watching.
    /// </remarks>
    private async Task<ShelfRefreshResult> OneShelfAsync(
        Shelf shelf,
        string documentName,
        CancellationToken cancellationToken)
    {
        var source = SourceFor(shelf.Source);

        if (source is null)
        {
            return Kept(shelf, documentName, SourceAnswer.NotConfigured());
        }

        var answer = await source.FetchAsync(shelf.Ask(), cancellationToken).ConfigureAwait(false);

        if (answer.Outcome is not SourceOutcome.Answered)
        {
            _logger.LogWarning(
                "The shelf {Shelf} was not refreshed because its source answered {Outcome}, so its document {Document} still holds what it held.",
                shelf.DisplayName,
                answer.Outcome,
                documentName);

            return Kept(shelf, documentName, answer);
        }

        var titles = answer.Titles
            .OrderBy(title => title, shelf.Order)
            .Take(shelf.Cap)
            .ToArray();

        using var payload = new MemoryStream();

        CatalogueDocumentBody.Write(payload, titles);
        payload.Position = 0;

        _store.Write(documentName, payload);

        _failuresInARow.Remove(documentName);

        return ShelfRefreshResult.Refreshed(shelf.DisplayName, documentName, titles.Length);
    }

    /// <summary>
    /// The result of a shelf whose source did not answer, with the run of
    /// failures behind it counted.
    /// </summary>
    /// <param name="shelf">The shelf.</param>
    /// <param name="documentName">The document its titles are kept in.</param>
    /// <param name="answer">What the source said instead of answering.</param>
    /// <returns>What happened to this shelf.</returns>
    /// <remarks>
    /// #79's fourth condition. The count is kept here rather than on the result
    /// because a result is one run and the question is about the runs before
    /// it, and it is keyed on the document name rather than on the shelf
    /// because that name is derived from a closed pair of vocabularies: the
    /// table can hold one entry per document this plugin can name and no more,
    /// so a shelf renamed, re-capped or reordered does not leave an entry
    /// behind and nothing grows without a bound.
    ///
    /// A source that has not been set up leaves the count where it is, for the
    /// reason <see cref="ShelfRefreshResult.ConsecutiveFailures"/> carries, and
    /// that is the one branch here that is not arithmetic.
    ///
    /// THE COUNT LIVES FOR AS LONG AS THE PROCESS AND NO LONGER. A server that
    /// restarts starts every shelf at nothing, so what this separates is a
    /// standing fault from a blip within one run of the server rather than
    /// across its life. Making it survive would mean writing it down, and where
    /// a shelf's run state is kept is #92's question rather than this one's;
    /// this is the bound to read before treating the number as a history.
    /// </remarks>
    private ShelfRefreshResult Kept(Shelf shelf, string documentName, SourceAnswer answer)
    {
        if (answer.Outcome is SourceOutcome.NotConfigured)
        {
            return ShelfRefreshResult.PreviousKept(shelf.DisplayName, documentName, answer, consecutiveFailures: 0);
        }

        _failuresInARow.TryGetValue(documentName, out var before);

        var now = before + 1;

        _failuresInARow[documentName] = now;

        return ShelfRefreshResult.PreviousKept(shelf.DisplayName, documentName, answer, now);
    }

    /// <summary>
    /// The adapter that speaks for a shelf's source, or null where this server
    /// has none.
    /// </summary>
    /// <param name="source">The body the shelf names.</param>
    /// <returns>The adapter, or null.</returns>
    private IMetadataSource? SourceFor(MetadataSource source)
    {
        for (var index = 0; index < _sources.Length; index++)
        {
            if (_sources[index].Source == source)
            {
                return _sources[index];
            }
        }

        return null;
    }
}
