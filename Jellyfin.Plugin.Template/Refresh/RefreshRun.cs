using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// What one pass over the shelves did, as one value.
/// </summary>
/// <remarks>
/// Returned rather than logged, because the two callers want different things
/// from it. The scheduled task writes a line and reports progress; the operator's
/// page in #92 wants the per-shelf state, and a run that had only been logged
/// would have to be parsed back out of a log to give it one.
///
/// <see cref="Started"/> is the member to read first. A run that was declined
/// because another one was already going is not an empty run and not a failure:
/// nothing was asked, nothing was written, and the refresh that is already going
/// will finish. #87's third condition is that a second start does not happen,
/// and this is what says so to whoever tried.
/// </remarks>
public sealed class RefreshRun
{
    private static readonly ShelfRefreshResult[] _nothing = Array.Empty<ShelfRefreshResult>();

    private RefreshRun(
        bool started,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        bool cancelled,
        IReadOnlyList<ShelfRefreshResult> shelves)
    {
        Started = started;
        StartedAt = startedAt;
        FinishedAt = finishedAt;
        Cancelled = cancelled;
        Shelves = shelves;
    }

    /// <summary>
    /// Gets a value indicating whether this run happened at all.
    /// </summary>
    /// <remarks>
    /// False only where another run held the refresh when this one asked for
    /// it. Every other reason a run does nothing - no shelves, every shelf off,
    /// no source configured - is a run that started and found nothing to do,
    /// which is a different thing to tell an operator.
    /// </remarks>
    public bool Started { get; }

    /// <summary>
    /// Gets when this run began, by the clock this plugin reads.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Gets when this run stopped, by the same clock.
    /// </summary>
    /// <remarks>
    /// Equal to <see cref="StartedAt"/> on a run that was declined, because
    /// nothing happened between the two. A duration is the difference rather
    /// than a third field, so there is one answer to how long a run took rather
    /// than two that can disagree.
    /// </remarks>
    public DateTimeOffset FinishedAt { get; }

    /// <summary>
    /// Gets a value indicating whether this run was stopped before it had asked
    /// every shelf.
    /// </summary>
    public bool Cancelled { get; }

    /// <summary>
    /// Gets what happened to each shelf, in the order they were taken.
    /// </summary>
    /// <remarks>
    /// Every shelf the run was handed appears here, including the ones it never
    /// reached, because a list that silently omitted them would say a shelf was
    /// not part of this run when it was.
    /// </remarks>
    public IReadOnlyList<ShelfRefreshResult> Shelves { get; }

    /// <summary>
    /// The answer to somebody who asked for a run while one was already going.
    /// </summary>
    /// <param name="at">When the second start was asked for.</param>
    /// <returns>A run that did not happen.</returns>
    public static RefreshRun Declined(DateTimeOffset at) =>
        new RefreshRun(started: false, at, at, cancelled: false, _nothing);

    /// <summary>
    /// The answer of a run that happened.
    /// </summary>
    /// <param name="startedAt">When it began.</param>
    /// <param name="finishedAt">When it stopped.</param>
    /// <param name="cancelled">Whether it was stopped before it had asked every shelf.</param>
    /// <param name="shelves">What happened to each shelf, in the order they were taken.</param>
    /// <returns>The run.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the results are null, or hold a null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the run stopped before it began. A negative duration is a
    /// clock that moved backwards under a run, and every number computed from
    /// this pair afterwards would be wrong in a way nobody would question.
    /// </exception>
    public static RefreshRun Of(
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        bool cancelled,
        IReadOnlyList<ShelfRefreshResult> shelves)
    {
        ArgumentNullException.ThrowIfNull(shelves);

        if (finishedAt < startedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finishedAt),
                finishedAt,
                "A run cannot stop before it began. A pair like this is a clock that moved backwards while it ran.");
        }

        var taken = new ShelfRefreshResult[shelves.Count];

        for (var index = 0; index < shelves.Count; index++)
        {
            var result = shelves[index];
            ArgumentNullException.ThrowIfNull(result, nameof(shelves));
            taken[index] = result;
        }

        return new RefreshRun(
            started: true,
            startedAt,
            finishedAt,
            cancelled,
            new ReadOnlyCollection<ShelfRefreshResult>(taken));
    }
}
