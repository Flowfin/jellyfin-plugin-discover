using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Refresh;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A pause that spends no real time and writes down every wait it was asked for.
/// </summary>
/// <remarks>
/// <para>
/// This is the replacement for a test that waits. The thing worth asserting
/// about pacing is how long a run held off and before which request, and both
/// of those are readable here without a second of the runner's time being
/// spent. A test that proved the same by being slow would prove it once and
/// then be given a longer timeout until it proved nothing.
/// </para>
/// <para>
/// Handed a clock, it advances that clock by what it was asked to wait, which
/// is what a real pause does to a real clock and is what makes a run assertable
/// end to end: the instants a run records are then the instants it would have
/// recorded on a machine. Handed no clock, it records the waits and moves
/// nothing, which is what a test wants when it is asserting the waits
/// themselves rather than what follows from them.
/// </para>
/// <para>
/// It honours cancellation, because a run cancelled while it is holding off
/// should stop there rather than after one more wait, and a test asserting that
/// needs the double to behave as the real one does.
/// </para>
/// </remarks>
internal sealed class PauseATestWatches : IPause
{
    private readonly ClockATestAdvances? _clock;

    private readonly List<TimeSpan> _waits = new List<TimeSpan>();

    /// <summary>
    /// Initializes a new instance of the <see cref="PauseATestWatches"/> class
    /// that records what it was asked for and moves nothing.
    /// </summary>
    public PauseATestWatches()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PauseATestWatches"/> class
    /// that advances the given clock by every wait it is asked for.
    /// </summary>
    /// <param name="clock">The clock a wait moves.</param>
    /// <exception cref="ArgumentNullException">Thrown when the clock is null.</exception>
    public PauseATestWatches(ClockATestAdvances clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
    }

    /// <summary>
    /// Gets every wait this pause was asked for, in the order it was asked.
    /// </summary>
    public IReadOnlyList<TimeSpan> Waits => _waits;

    /// <inheritdoc />
    public Task ForAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _waits.Add(duration);
        _clock?.Advance(duration);

        return Task.CompletedTask;
    }
}
