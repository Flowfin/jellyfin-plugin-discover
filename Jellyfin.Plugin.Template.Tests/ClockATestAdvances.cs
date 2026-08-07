using System;
using Jellyfin.Plugin.Template.Time;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A clock that moves only when a test moves it.
/// </summary>
/// <remarks>
/// This is the replacement for sleeping. A test that needs an hour to pass
/// advances an hour here and the assertion that follows is about the code under
/// test rather than about how loaded the runner was.
///
/// It never advances on its own, including between two reads in the same
/// assertion. That is the property that makes a boundary testable: a decision
/// taken one tick before an expiry and one tick after it can be asserted
/// separately, which is impossible against a clock that moved in between.
/// </remarks>
internal sealed class ClockATestAdvances : IClock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClockATestAdvances"/> class.
    /// </summary>
    /// <param name="start">The instant the clock reads until it is advanced.</param>
    public ClockATestAdvances(DateTimeOffset start)
    {
        UtcNow = start;
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; private set; }

    /// <summary>
    /// Moves the clock forward.
    /// </summary>
    /// <param name="amount">How far forward. Must not be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="amount"/> is negative. A clock that can be
    /// wound back would let a test pass by rewinding past the decision it was
    /// supposed to be asserting.
    /// </exception>
    public void Advance(TimeSpan amount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, TimeSpan.Zero);

        UtcNow += amount;
    }
}
