using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Refresh;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// How long a run is told to hold off before it asks a source again.
/// </summary>
/// <remarks>
/// #78's second condition, asserted on the type that decides it rather than
/// through a run. Everything here is against instants a test chooses, because
/// the whole point of the type is that it decides against a clock somebody else
/// reads: a test that got to a boundary by waiting could not assert one tick
/// either side of it.
/// </remarks>
public class SourcePaceTests
{
    private static readonly DateTimeOffset _noon =
        new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A source nobody has asked is not held off at all.
    /// </summary>
    /// <remarks>
    /// The ordinary case, and it is first because everything below asserts that
    /// a wait was imposed. A pace that held every caller off would pass all of
    /// those and refresh nothing.
    /// </remarks>
    [Fact]
    public void ASourceNobodyHasAskedIsNotHeldOff()
    {
        var pace = new SourcePace();

        Assert.Equal(TimeSpan.Zero, pace.Waiting(MetadataSource.Tmdb, _noon));
    }

    /// <summary>
    /// Every request up to the budget begins at once.
    /// </summary>
    /// <remarks>
    /// The budget is what may be spent without waiting, so the fourth request
    /// inside a window is still free. A pace that held the caller off at the
    /// fourth would be one that spends a window's worth of latency to make
    /// three requests.
    /// </remarks>
    [Fact]
    public void EveryRequestUpToTheBudgetBeginsAtOnce()
    {
        var pace = new SourcePace();

        for (var spent = 0; spent < SourcePace.RequestsPerWindow; spent++)
        {
            Assert.Equal(TimeSpan.Zero, pace.Waiting(MetadataSource.Tmdb, _noon));
            pace.Asked(MetadataSource.Tmdb, _noon);
        }

        Assert.NotEqual(TimeSpan.Zero, pace.Waiting(MetadataSource.Tmdb, _noon));
    }

    /// <summary>
    /// The request past the budget waits until the oldest one leaves the window.
    /// </summary>
    /// <remarks>
    /// Not until the newest leaves it, which is the mistake that turns a budget
    /// over a window into a fixed gap several times too long. With the whole
    /// budget spent at one instant the two answers are the same, so the requests
    /// here are spread across the window and the assertion is on the difference.
    /// </remarks>
    [Fact]
    public void TheRequestPastTheBudgetWaitsForTheOldestToLeaveTheWindow()
    {
        var pace = new SourcePace();
        var gap = SourcePace.Window / (SourcePace.RequestsPerWindow * 2);

        for (var spent = 0; spent < SourcePace.RequestsPerWindow; spent++)
        {
            pace.Asked(MetadataSource.Tmdb, _noon + (gap * spent));
        }

        var asking = _noon + (gap * SourcePace.RequestsPerWindow);

        Assert.Equal((_noon + SourcePace.Window) - asking, pace.Waiting(MetadataSource.Tmdb, asking));
    }

    /// <summary>
    /// A request exactly one window old is out of the window, and one tick
    /// younger than that is still in it.
    /// </summary>
    /// <remarks>
    /// The boundary, asserted from both sides, which is what the clock a test
    /// advances exists for. Getting it the other way round costs one window of
    /// latency per request for ever: every request would be held off by the one
    /// that has just aged out.
    /// </remarks>
    [Fact]
    public void TheBoundaryOfTheWindowIsAssertedFromBothSides()
    {
        var pace = new SourcePace();

        for (var spent = 0; spent < SourcePace.RequestsPerWindow; spent++)
        {
            pace.Asked(MetadataSource.Tmdb, _noon);
        }

        var oneTickEarly = _noon + SourcePace.Window - TimeSpan.FromTicks(1);
        var onTheBoundary = _noon + SourcePace.Window;

        Assert.Equal(TimeSpan.FromTicks(1), pace.Waiting(MetadataSource.Tmdb, oneTickEarly));
        Assert.Equal(TimeSpan.Zero, pace.Waiting(MetadataSource.Tmdb, onTheBoundary));

        // The other side of the boundary asserted on what follows from it rather
        // than on the zero above, which a pace that never drops anything also
        // returns: the four are out of the window, so a whole budget is free
        // again and the request past THAT one waits a whole window.
        for (var spent = 0; spent < SourcePace.RequestsPerWindow; spent++)
        {
            pace.Asked(MetadataSource.Tmdb, onTheBoundary);
        }

        Assert.Equal(SourcePace.Window, pace.Waiting(MetadataSource.Tmdb, onTheBoundary));
    }

    /// <summary>
    /// Two sources have two budgets, and one spending its own does not hold the
    /// other up.
    /// </summary>
    /// <remarks>
    /// The limit being respected is the source's, so a budget shared across
    /// sources would slow a refresh down for a ceiling nobody imposed. The
    /// second source here is one #83 holds as later, and it is named rather than
    /// invented because the type keys on the enumeration a stored identifier
    /// carries.
    /// </remarks>
    [Fact]
    public void TwoSourcesHaveTwoBudgets()
    {
        var pace = new SourcePace();

        for (var spent = 0; spent < SourcePace.RequestsPerWindow; spent++)
        {
            pace.Asked(MetadataSource.Tmdb, _noon);
        }

        Assert.NotEqual(TimeSpan.Zero, pace.Waiting(MetadataSource.Tmdb, _noon));
        Assert.Equal(TimeSpan.Zero, pace.Waiting(MetadataSource.Tvdb, _noon));
    }

    /// <summary>
    /// A caller that serves every wait it is told to serve never begins more
    /// than the budget inside one window, whatever it is asking for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the property #78's second condition asks for, asserted as the
    /// sentence rather than through the queue that holds it. The loop is the
    /// caller a run is: read the wait, let that much time pass, record the
    /// request, make it.
    /// </para>
    /// <para>
    /// Forty requests, which is ten times the budget and more than six times the
    /// shipped shelf count, so the answer does not depend on a run being small.
    /// The window is walked over every recorded instant rather than sampled,
    /// because a burst that fits between two samples is exactly what this is
    /// looking for.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoMoreThanTheBudgetBeginsInsideAnyWindow()
    {
        var pace = new SourcePace();
        var now = _noon;
        var begun = new List<DateTimeOffset>();

        for (var request = 0; request < SourcePace.RequestsPerWindow * 10; request++)
        {
            now += pace.Waiting(MetadataSource.Tmdb, now);

            pace.Asked(MetadataSource.Tmdb, now);
            begun.Add(now);
        }

        foreach (var ending in begun)
        {
            var inside = 0;

            foreach (var began in begun)
            {
                if (began > ending - SourcePace.Window && began <= ending)
                {
                    inside++;
                }
            }

            Assert.True(
                inside <= SourcePace.RequestsPerWindow,
                $"{inside} requests began in the window ending at {ending:O}, which is more than the {SourcePace.RequestsPerWindow} the budget allows.");
        }

        Assert.Equal(SourcePace.RequestsPerWindow * 10, begun.Count);
    }
}
