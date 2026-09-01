using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Server;
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
    private readonly IServerLibrary? _library;
    private readonly IClock _clock;
    private readonly IPause _pause;
    private readonly ILogger<CatalogueRefresh> _logger;
    private readonly Dictionary<string, int> _failuresInARow = new Dictionary<string, int>(StringComparer.Ordinal);

    // #78's third and fourth conditions. Built here rather than taken as an
    // argument for the same reason the retention below is: everything it needs
    // is a declared constant and a clock this class already holds, and a run
    // that could be handed a rest with no backoff in it would be a run in which
    // the guard is optional.
    //
    // It holds no logger, so what an operator is told about a source this
    // plugin has given up on is written here, once, at the moment the giving up
    // happens rather than once per shelf that then goes unasked.
    private readonly SourceRest _rest = new SourceRest();

    // #78's second condition. Built here for the same reason the rest above is:
    // everything it needs is a declared constant and a clock this class already
    // holds, and a run that could be handed a pace with no budget in it would be
    // a run in which the guard is optional.
    //
    // It is separate from the rest rather than folded into it because the two
    // answer different questions. The rest says whether a source that already
    // refused may be asked at all; the pace says how long to hold off before
    // asking a source that is answering perfectly well, so that a refusal is
    // not provoked in the first place.
    private readonly SourcePace _pace = new SourcePace();

    private readonly CatalogueRetention _retention;

    private int _running;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogueRefresh"/> class.
    /// </summary>
    /// <param name="sources">The sources this server is set up to ask.</param>
    /// <param name="store">Where a shelf's titles are kept.</param>
    /// <param name="library">
    /// What this server already holds, or null where nothing can answer that.
    /// </param>
    /// <param name="clock">The clock a run is timed by.</param>
    /// <param name="pause">How a run holds off between requests to one source.</param>
    /// <param name="logger">Where a run says what it did.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any argument but <paramref name="library"/> is null, or when
    /// the sources hold a null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// An empty set of sources is the ordinary state of a server nobody has
    /// configured rather than an error, so it is admitted. What such a run does
    /// is ask nobody and write nothing, and every shelf comes back
    /// <see cref="ShelfRefreshOutcome.PreviousKept"/> with
    /// <see cref="SourceOutcome.NotConfigured"/>, which is what #63 asks an
    /// operator be able to see.
    /// </para>
    /// <para>
    /// THE LIBRARY IS NULLABLE AND WHAT THAT ADMITS IS NARROWER THAN IT WAS.
    /// #89 asks that titles the server already has be left out of a shelf, and
    /// <see cref="Server.ServerLibraryAdapter"/> is what answers that on a
    /// server, so a run the task composed has somebody to ask. Null is a run
    /// composed with nobody to ask, which is what a test drives and what a
    /// caller written before that adapter existed had. Such a run keeps every
    /// title a source offered. The parameter is required rather than defaulted
    /// so that every caller states which of the two it is in, and a run with no
    /// library says so in its own log line instead of looking like a run that
    /// asked and found nothing.
    /// </para>
    /// <para>
    /// THE PAUSE IS REQUIRED FOR THE SAME REASON THE CLOCK IS. It is the one
    /// thing in a run that lets real time pass, so a run built without it would
    /// be a run that either paced nothing or paced by sleeping where a test
    /// could not see it. Every caller therefore states what its waiting is
    /// served by, and the one a server builds is in
    /// <see cref="DiscoverRefreshTask"/>.
    /// </para>
    /// </remarks>
    public CatalogueRefresh(
        IReadOnlyCollection<IMetadataSource> sources,
        CatalogueDocumentStore store,
        IServerLibrary? library,
        IClock clock,
        IPause pause,
        ILogger<CatalogueRefresh> logger)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(pause);
        ArgumentNullException.ThrowIfNull(logger);

        var taken = new List<IMetadataSource>(sources.Count);

        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(sources));
            taken.Add(source);
        }

        _sources = taken.ToArray();
        _store = store;
        _library = library;
        _clock = clock;
        _pause = pause;
        _logger = logger;

        // Built here rather than taken as an argument, because both halves of
        // it are already in this constructor: the number is the shipped default
        // until an operator can type one, which is #103, and the ceiling it is
        // held under belongs to the sources handed over on the line above.
        //
        // InForce rather than Of. Of judges a number somebody typed and refuses
        // one no active source allows, which belongs at the save; a run has to
        // do something whatever the sources say, because the records are
        // already on disk and the terms already apply to them. So a stricter
        // source shortens this rather than making the refresh unbuildable, and
        // the shortening is reported rather than absorbed.
        _retention = CatalogueRetention.InForce(CatalogueRetention.Default, _sources, out var cappedBy);

        if (cappedBy is not null)
        {
            _logger.LogInformation(
                "The catalogue retention this refresh applies is {RetentionDays} days rather than the {ConfiguredDays} days configured, because {Source} allows no longer. The ceiling is that source's terms rather than this plugin's preference.",
                _retention.Duration.TotalDays,
                CatalogueRetention.Default.TotalDays,
                cappedBy.Source);
        }
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
    /// Gets how long this run may keep what a source answered with.
    /// </summary>
    /// <remarks>
    /// Read by the suite so that the number a sweep applies is the one
    /// <see cref="CatalogueRetention"/> declares rather than one asserted twice.
    /// It is the shipped default checked against this run's sources, and the
    /// setting that will replace the default is #103's.
    /// </remarks>
    public CatalogueRetention Retention => _retention;

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
                results.Add(Swept(ShelfRefreshResult.TurnedOff(shelf.DisplayName, documentName)));
            }
            else
            {
                try
                {
                    results.Add(Swept(await OneShelfAsync(shelf, documentName, cancellationToken).ConfigureAwait(false)));
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    results.Add(ShelfRefreshResult.Cancelled(shelf.DisplayName, documentName));
                }
            }

            progress?.Report((index + 1) * 100d / shelves.Count);
        }

        if (!cancelled)
        {
            SweepWhatNoShelfNamed(results);
        }

        progress?.Report(100);

        var run = RefreshRun.Of(startedAt, _clock.UtcNow, cancelled, results);

        _logger.LogInformation(
            "A discover catalogue refresh took {Shelves} shelves, refreshed {Refreshed}, kept what {Kept} already held, took what {Expired} held past the {RetentionDays}-day retention, skipped {Off} that are turned off, did not reach {Unreached}, and found {Standing} whose source has now failed more than once in a row.",
            run.Shelves.Count,
            run.Shelves.Count(result => result.Outcome is ShelfRefreshOutcome.Refreshed),
            run.Shelves.Count(result => result.Outcome is ShelfRefreshOutcome.PreviousKept),
            run.Shelves.Count(result => result.Outcome is ShelfRefreshOutcome.Expired),
            _retention.Duration.TotalDays,
            run.Shelves.Count(result => result.Outcome is ShelfRefreshOutcome.TurnedOff),
            run.Shelves.Count(result => result.Outcome is ShelfRefreshOutcome.Cancelled),
            run.Shelves.Count(result => result.ConsecutiveFailures > 1));

        return run;
    }

    /// <summary>
    /// Takes whatever the documents no shelf in this run named hold past the
    /// retention.
    /// </summary>
    /// <param name="perShelf">What this run did, one entry for each shelf it was handed.</param>
    /// <remarks>
    /// <para>
    /// #68's second condition, in the half the per-shelf sweep cannot reach. A
    /// document's name comes from a shelf's question and kind and from nothing
    /// else, so a version that ships a different set, and a downgrade, leave the
    /// document written under the old pair standing. Nothing reads it, nothing
    /// dates it and nothing removes it, which makes the records a server keeps
    /// longest exactly the ones nobody is looking at. The ceiling under the
    /// retention is a source's terms rather than this plugin's housekeeping,
    /// and terms do not stop applying because the shelf that fetched the records
    /// no longer exists.
    /// </para>
    /// <para>
    /// IT IS SAFE FOR A RUN HANDED A SUBSET, which is the reading to do before
    /// taking "no shelf named it" for "nobody owns it". This never removes a
    /// document that holds anything: the only removal is of one whose every
    /// record is past the retention, and that is true of those records whoever
    /// owns them. So a run given one shelf sweeps the other five's documents by
    /// the same rule it would apply to them itself, and takes nothing a later
    /// run wanted.
    /// </para>
    /// <para>
    /// Not after a cancellation, for the same reason the per-shelf sweep is not:
    /// doing work after a cancellation is the thing a cancellation asked to
    /// stop, and a listing of the directory plus a read per document is more
    /// work than the sweep it skips.
    /// </para>
    /// <para>
    /// It is a second method rather than the per-shelf sweep widened. What
    /// differs is not the decision, which is
    /// <see cref="CatalogueRetention.StillHeld"/>'s in both, but what an
    /// operator is told: one names a shelf that keeps what it holds until a
    /// source answers again, and the other names a document no shelf will ever
    /// write to. Assembling one message from the other would make the template a
    /// value rather than a constant, which is not a structured log line.
    /// </para>
    /// </remarks>
    private void SweepWhatNoShelfNamed(IReadOnlyList<ShelfRefreshResult> perShelf)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (var result in perShelf)
        {
            named.Add(result.DocumentName);
        }

        foreach (var documentName in _store.DocumentNames())
        {
            if (!named.Contains(documentName))
            {
                SweepOneNoShelfNamed(documentName);
            }
        }
    }

    /// <summary>
    /// Takes whatever one document no shelf named holds past the retention.
    /// </summary>
    /// <param name="documentName">The document.</param>
    /// <remarks>
    /// Every failure leaves the document alone, which is the rule the per-shelf
    /// sweep states: a document that is absent, that the store refused, or whose
    /// bytes are not a body this build reads is not one whose records can be
    /// dated, so removing it would be a sweep deleting what it could not judge.
    /// That matters more here than there, because a document nothing names is
    /// also a document nothing will rewrite.
    /// </remarks>
    private void SweepOneNoShelfNamed(string documentName)
    {
        var payload = _store.Read(documentName);

        if (payload is null)
        {
            return;
        }

        IReadOnlyList<DiscoverTitle> stored;

        try
        {
            stored = CatalogueDocumentBody.Read(payload);
        }
        catch (InvalidDataException reason)
        {
            _logger.LogWarning(
                reason,
                "The document {Document} is named by no shelf in this run and could not be read as a catalogue body, so nothing in it was dated and it was left where it is.",
                documentName);

            return;
        }

        var held = _retention.StillHeld(stored, _clock.UtcNow);

        if (held.Count == stored.Count)
        {
            return;
        }

        if (held.Count == 0)
        {
            _store.Remove(documentName);

            _logger.LogInformation(
                "The document {Document} is named by no shelf in this run, held {Dropped} records and every one of them was older than the {RetentionDays}-day retention, so the document was removed. Nothing will write to it again.",
                documentName,
                stored.Count,
                _retention.Duration.TotalDays);

            return;
        }

        using var payloadToKeep = new MemoryStream();

        CatalogueDocumentBody.Write(payloadToKeep, held);
        payloadToKeep.Position = 0;

        _store.Write(documentName, payloadToKeep);

        _logger.LogInformation(
            "The document {Document} is named by no shelf in this run, held {Dropped} records older than the {RetentionDays}-day retention, which were removed, and {Kept} that are still held and were written back. Nothing will write to it again.",
            documentName,
            stored.Count - held.Count,
            _retention.Duration.TotalDays,
            held.Count);
    }

    /// <summary>
    /// Takes whatever a document this run left standing holds past the retention.
    /// </summary>
    /// <param name="result">What this run did to the shelf.</param>
    /// <returns>
    /// The same result where nothing expired, and an
    /// <see cref="ShelfRefreshOutcome.Expired"/> one where something did.
    /// </returns>
    /// <remarks>
    /// #68's second condition, in the half that is about not KEEPING a record
    /// past the retention. The other half, not serving one, is the same
    /// question asked of the same type: everything that reads a stored document
    /// goes through <see cref="CatalogueRetention.StillHeld"/>, and this is its
    /// first caller. Nothing else in this plugin reads a catalogue document
    /// yet, so this is the whole of the read path today rather than one of
    /// several.
    ///
    /// It runs on the two outcomes that leave a document standing and on no
    /// others. A refreshed shelf's document was written in this run out of what
    /// a source has just answered, so nothing in it can be past the retention
    /// and reading it back would be a second read per shelf per run for an
    /// answer that is known. A cancelled shelf was not reached at all, and
    /// doing work after a cancellation is the thing a cancellation asked to
    /// stop.
    ///
    /// A TURNED-OFF SHELF IS SWEPT TOO, which is the part worth reading twice.
    /// The ceiling under this number is a source's terms rather than this
    /// plugin's housekeeping, and terms do not stop applying because an
    /// operator switched a row off. A sweep that skipped them would leave the
    /// records a server keeps longest as exactly the ones nothing looks at.
    ///
    /// Every failure here leaves the document alone and the result unchanged. A
    /// document that is absent, that the store refused, or whose bytes are not
    /// a body this build reads is not a document whose records can be dated, so
    /// removing it would be a sweep deleting what it could not judge. The store
    /// reports the ones it refuses; this reports the third case, once, with the
    /// document named.
    /// </remarks>
    private ShelfRefreshResult Swept(ShelfRefreshResult result)
    {
        if (result.Outcome is not (ShelfRefreshOutcome.PreviousKept or ShelfRefreshOutcome.TurnedOff))
        {
            return result;
        }

        var payload = _store.Read(result.DocumentName);

        if (payload is null)
        {
            return result;
        }

        IReadOnlyList<DiscoverTitle> stored;

        try
        {
            stored = CatalogueDocumentBody.Read(payload);
        }
        catch (InvalidDataException reason)
        {
            _logger.LogWarning(
                reason,
                "The document {Document} could not be read as a catalogue body, so nothing in it was dated and it was left where it is. Its shelf keeps whatever it holds until a source answers for it again.",
                result.DocumentName);

            return result;
        }

        var held = _retention.StillHeld(stored, _clock.UtcNow);

        if (held.Count == stored.Count)
        {
            return result;
        }

        if (held.Count == 0)
        {
            _store.Remove(result.DocumentName);

            _logger.LogInformation(
                "The document {Document} held {Dropped} records and every one of them was older than the {RetentionDays}-day retention, so the document was removed rather than kept. Its shelf was not refreshed in this run, so it holds nothing until a source answers for it.",
                result.DocumentName,
                stored.Count,
                _retention.Duration.TotalDays);
        }
        else
        {
            using var payloadToKeep = new MemoryStream();

            CatalogueDocumentBody.Write(payloadToKeep, held);
            payloadToKeep.Position = 0;

            _store.Write(result.DocumentName, payloadToKeep);

            _logger.LogInformation(
                "The document {Document} held {Dropped} records older than the {RetentionDays}-day retention, which were removed, and {Kept} that are still held and were written back.",
                result.DocumentName,
                stored.Count - held.Count,
                _retention.Duration.TotalDays,
                held.Count);
        }

        return ShelfRefreshResult.Expired(result, held.Count);
    }

    /// <summary>
    /// Asks one shelf's source and writes what came back.
    /// </summary>
    /// <param name="shelf">The shelf.</param>
    /// <param name="documentName">The document its titles are kept in.</param>
    /// <param name="cancellationToken">Stops the fetch.</param>
    /// <returns>What happened to this shelf.</returns>
    /// <remarks>
    /// <para>
    /// A shelf naming a source this server is not set up to ask is answered
    /// exactly as a configured source that reported it has not been set up,
    /// because that is what it is from the shelf's side and because
    /// <see cref="Shelf.ValidatedAgainst"/> is where such a shelf is refused, at
    /// the moment a configuration is saved rather than in the middle of a run
    /// nobody is watching.
    /// </para>
    /// <para>
    /// A source that refused is not asked again until it said it may be, which
    /// is #78's third condition, and is not asked more than
    /// <see cref="SourceRest.Tries"/> times in a row whatever it said, which is
    /// the fourth. Both are decided by <see cref="SourceRest"/> and neither
    /// waits: a shelf whose source is resting is one this run does not ask, so
    /// six shelves on one refusing source cost one request rather than six.
    /// That is where the second request into a refusal is prevented rather than
    /// in the adapter, which reports a refusal and decides nothing about when
    /// to ask again.
    /// </para>
    /// <para>
    /// A request that would put this run over the source's budget waits until
    /// it would not, which is #78's second condition and is
    /// <see cref="SourcePace"/>'s. The order of the two is the whole of what is
    /// decided here: the rest is read first, because a resting source is one
    /// this run does not ask at all and paying a wait for a request nobody is
    /// going to make would be a run slowed down by a source it has given up on.
    /// The pace is then read for the request that is actually about to happen,
    /// and the request is recorded before it is made rather than after, because
    /// a budget counts requests and a request that fails has spent one.
    /// </para>
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

        if (_rest.RestingFor(shelf.Source, _clock.UtcNow) is { } left)
        {
            var standing = _rest.Standing(shelf.Source);

            _logger.LogInformation(
                "The shelf {Shelf} was not asked, because {Source} answered {Outcome} and is being left alone for another {RestMinutes} minutes. Its document {Document} still holds what it held.",
                shelf.DisplayName,
                shelf.Source,
                standing.Outcome,
                left.TotalMinutes,
                documentName);

            return Kept(shelf, documentName, standing);
        }

        var waiting = _pace.Waiting(shelf.Source, _clock.UtcNow);

        if (waiting > TimeSpan.Zero)
        {
            _logger.LogDebug(
                "The shelf {Shelf} waited {WaitMilliseconds} milliseconds before its request, because {Source} has already been asked {Requests} times in the last {WindowSeconds} seconds.",
                shelf.DisplayName,
                waiting.TotalMilliseconds,
                shelf.Source,
                SourcePace.RequestsPerWindow,
                SourcePace.Window.TotalSeconds);

            await _pause.ForAsync(waiting, cancellationToken).ConfigureAwait(false);
        }

        _pace.Asked(shelf.Source, _clock.UtcNow);

        var answer = await source.FetchAsync(shelf.Ask(), cancellationToken).ConfigureAwait(false);

        Rested(shelf.Source, answer);

        if (answer.Outcome is not SourceOutcome.Answered)
        {
            _logger.LogWarning(
                "The shelf {Shelf} was not refreshed because its source answered {Outcome}, so its document {Document} still holds what it held.",
                shelf.DisplayName,
                answer.Outcome,
                documentName);

            return Kept(shelf, documentName, answer);
        }

        var titles = WithoutWhatThisServerHas(shelf, answer.Titles)
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
    /// Drops the titles this server already holds.
    /// </summary>
    /// <param name="shelf">The shelf the titles were offered for.</param>
    /// <param name="offered">What the source answered with.</param>
    /// <returns>The titles this server does not have.</returns>
    /// <remarks>
    /// <para>
    /// #89. A discover page's whole premise is titles the server does not have,
    /// so a film the household already owns is the defect a user notices first.
    /// The rule is one sentence over both kinds: A TITLE IS OWNED WHEN THE
    /// SERVER HOLDS AT LEAST ONE PART OF IT, where a part is the film for a
    /// movie and an episode for a series. The series half is #2's answer of
    /// 2026-08-24 rather than this file's invention, and it is why the seam
    /// answers with a count instead of a yes: a series a server carries with no
    /// episode is a row in a library rather than something anybody can watch.
    /// </para>
    /// <para>
    /// It runs here rather than while a user is browsing, which is the other
    /// half of that answer. The price is one library question per title per
    /// refresh, paid on a schedule, instead of the same questions paid again
    /// every time a client opens a row.
    /// </para>
    /// <para>
    /// IT RUNS BEFORE THE CAP RATHER THAN AFTER IT. A shelf's cap is how many
    /// titles it shows, so filtering afterwards would hand a user a row of
    /// three because seventeen of the twenty offered were already on the
    /// server, and the number an operator set would silently mean something
    /// else. What it costs is that the question is asked of every title a
    /// source offered rather than of the capped set, and that cost is the one
    /// the paragraph above names.
    /// </para>
    /// <para>
    /// WHAT IS COMPARED IS THE IDENTITY AND NOTHING ELSE, held by the seam's
    /// signature rather than by this method: <see cref="IServerLibrary"/> is
    /// handed an identity and a kind, so no title text crosses it and two
    /// titles sharing a name are two questions with two answers.
    /// </para>
    /// <para>
    /// With no library to ask, every title is kept and the run says so. That is
    /// a run composed with nobody to ask rather than a mode an operator can be
    /// in, which the constructor's remark carries.
    /// </para>
    /// </remarks>
    private IReadOnlyList<DiscoverTitle> WithoutWhatThisServerHas(Shelf shelf, IReadOnlyList<DiscoverTitle> offered)
    {
        if (_library is null)
        {
            _logger.LogDebug(
                "The shelf {Shelf} kept all {Offered} titles its source offered, because nothing on this server answers what the library already holds.",
                shelf.DisplayName,
                offered.Count);

            return offered;
        }

        var kept = new List<DiscoverTitle>(offered.Count);

        foreach (var title in offered)
        {
            if (_library.PartsHeld(title.Identity, title.Kind) > 0)
            {
                continue;
            }

            kept.Add(title);
        }

        if (kept.Count != offered.Count)
        {
            _logger.LogInformation(
                "The shelf {Shelf} left out {Owned} of the {Offered} titles its source offered, because this server already holds them.",
                shelf.DisplayName,
                offered.Count - kept.Count,
                offered.Count);
        }

        return kept;
    }

    /// <summary>
    /// Leaves a source that refused alone, and tells the operator where this
    /// plugin has stopped taking it at its word.
    /// </summary>
    /// <param name="source">The source that was asked.</param>
    /// <param name="answer">What it gave.</param>
    /// <remarks>
    /// The two refusals are the two this acts on, and the other two answers are
    /// not silence. An answer clears the count, so a source that fails once a
    /// week is never left alone for six hours; a source that has not been set
    /// up is left exactly where it was, because nothing is wrong with it and
    /// the fault is in what built the shelf, which is the reading
    /// <see cref="ShelfRefreshResult.ConsecutiveFailures"/> already carries on
    /// the same case.
    ///
    /// The warning is written once, on the run in which the threshold is
    /// reached, rather than on every shelf that then goes unasked. An operator
    /// reading six lines saying a source has been given up on would be reading
    /// about six shelves rather than about one source.
    /// </remarks>
    private void Rested(MetadataSource source, SourceAnswer answer)
    {
        if (answer.Outcome is SourceOutcome.Answered)
        {
            _rest.Answered(source);

            return;
        }

        if (answer.Outcome is not (SourceOutcome.RateLimited or SourceOutcome.TemporarilyFailed))
        {
            return;
        }

        var taken = _rest.Refused(source, answer, _clock.UtcNow);

        if (!taken.GaveUp)
        {
            return;
        }

        _logger.LogWarning(
            "{Source} has refused {Refusals} times in a row, most recently with {Outcome}, so it is being left alone until {Until:u} rather than asked again. Every shelf it feeds keeps what it holds until then, and nothing here retries sooner even where the source named a shorter wait.",
            source,
            taken.Refusals,
            answer.Outcome,
            taken.Until);
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
