using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// How long a run holds off before it asks a source again, so that a refresh
/// cannot outrun the source's request budget.
/// </summary>
/// <remarks>
/// <para>
/// #78's second condition. It lives beside the thing that drives a refresh
/// rather than inside an adapter, which is the answer taken on #2 on
/// 2026-08-24, and the reason is in the shape of a query: a query carries no
/// shelf count and no bound, so an adapter asked one question cannot know it is
/// the fortieth question of a run. What does know is the run.
/// </para>
/// <para>
/// THIS IS NOT <see cref="SourceRest"/> AND NEITHER IS A WEAKER FORM OF THE
/// OTHER. A rest is what happens once a source has already refused: it stops
/// the second and third request into a refusal that has been made. A pace is
/// what stops the refusal being provoked, and it applies to a source that is
/// answering perfectly. A run holds both, reads the rest first because a
/// resting source is not asked at all, and pays the pace only for a request it
/// is about to make.
/// </para>
/// <para>
/// WHAT IT GUARANTEES, STATED AS THE PROPERTY RATHER THAN AS THE MECHANISM. No
/// more than <see cref="RequestsPerWindow"/> requests to one source begin
/// inside any half-open window of <see cref="Window"/>, whatever the shelf
/// count and whatever bound #58 puts on a shelf. That is the sentence a test
/// asserts; the queue below is one way to hold it.
/// </para>
/// <para>
/// The budget is per source rather than per run or per shelf, because the
/// limit being respected is the source's. Six shelves on one source share one
/// budget; two sources are two budgets, and neither slows the other down.
/// </para>
/// <para>
/// THIS ONE WAITS, WHICH IS THE OPPOSITE OF WHAT <see cref="SourceRest"/> DOES,
/// and the difference is which of the two answers is honest. A shelf whose
/// source refused keeps what it had and the retry is the next run, so nothing
/// is lost by not waiting. A shelf that is merely fifth in the queue has been
/// asked for nothing yet, and a run that dropped it would refresh the first
/// four shelves for ever and never the rest. So the wait is real, it is served
/// through <see cref="IPause"/> rather than by touching the runtime's timer
/// here, and it is bounded by <see cref="Window"/> however long a run is.
/// </para>
/// <para>
/// The state lives for as long as the process and no longer, which is the bound
/// <see cref="SourceRest"/> carries for the same reason. A server that restarts
/// starts its window empty, which errs towards up to one extra window's worth
/// of requests rather than towards a run held back by a window nobody is in.
/// </para>
/// </remarks>
public sealed class SourcePace
{
    /// <summary>
    /// How many requests to one source may begin inside one <see cref="Window"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four, which is a tenth of the number the one implemented source names.
    /// <c>docs/source-api/tmdb.md</c> records that reading: the source says its
    /// upper limits "sit somewhere in the 40 requests per second range" and, in
    /// the same place, that the limit could change at any time. A caller pacing
    /// at the stated number is a caller that is over the limit on the day it
    /// moves down, and the number is an approximation the source declines to
    /// commit to, so pacing at it is pacing at a guess.
    /// </para>
    /// <para>
    /// A tenth leaves an order of magnitude for that number to move before this
    /// plugin is what provoked a refusal. What it costs is latency in a task
    /// that runs on a daily timer and that nobody watches: the six shipped
    /// shelves are six requests and take about one second longer than they
    /// would unpaced, and the twenty-one-times-larger refresh
    /// <c>docs/source-api/tmdb.md</c> foresees, a hundred and twenty-six
    /// requests, takes about half a minute. Both are far inside the cadence
    /// <see cref="DiscoverRefreshTask"/> defaults to.
    /// </para>
    /// <para>
    /// It is a budget over a window rather than a fixed gap between requests.
    /// The two hold the same ceiling, and the budget spends the wait where the
    /// ceiling is actually reached: six shelves pay one wait rather than six.
    /// </para>
    /// </remarks>
    public const int RequestsPerWindow = 4;

    /// <summary>
    /// The window a budget is counted over.
    /// </summary>
    /// <remarks>
    /// One second, because that is the unit the one source implemented states
    /// its own ceiling in, and a window in the same unit as the published
    /// number is one nobody has to convert to compare.
    /// </remarks>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    private readonly Dictionary<MetadataSource, Queue<DateTimeOffset>> _asked =
        new Dictionary<MetadataSource, Queue<DateTimeOffset>>();

    /// <summary>
    /// How long the caller must hold off before asking this source again.
    /// </summary>
    /// <param name="source">The source about to be asked.</param>
    /// <param name="now">What the clock reads.</param>
    /// <returns>
    /// How much longer to wait, which is <see cref="TimeSpan.Zero"/> where the
    /// budget for the current window is not spent.
    /// </returns>
    /// <remarks>
    /// The window is half-open, so a request made exactly one window ago is out
    /// of it. That direction is the one that keeps <see cref="Asked"/> and this
    /// agreeing: a caller that waits exactly what this returns and then records
    /// its request finds the request it was waiting on already dropped, so the
    /// wait it was told to serve is the whole of the wait rather than the first
    /// of two.
    /// </remarks>
    public TimeSpan Waiting(MetadataSource source, DateTimeOffset now)
    {
        if (!_asked.TryGetValue(source, out var window))
        {
            return TimeSpan.Zero;
        }

        Forget(window, now);

        if (window.Count < RequestsPerWindow)
        {
            return TimeSpan.Zero;
        }

        return (window.Peek() + Window) - now;
    }

    /// <summary>
    /// Records that this source is being asked now.
    /// </summary>
    /// <param name="source">The source being asked.</param>
    /// <param name="at">What the clock read as the request began.</param>
    /// <remarks>
    /// Called for the request that is about to be made rather than for the
    /// answer that came back, because what a budget counts is requests and a
    /// request that failed spent one just the same.
    ///
    /// NO TEST SEPARATES THE TWO ORDERINGS, and that is a bound on what the
    /// suite proves here rather than a claim it does. The three ways a source
    /// gives nothing come back as answers, so a refusal is recorded either way,
    /// and the one case that differs is an adapter that THROWS: recorded
    /// afterwards, its request is not counted. That exception leaves the run by
    /// design, so observing the difference needs a second run against the same
    /// refresh instance with a source that throws once, which is more apparatus
    /// than the case is worth today. The line above is the honest ordering, and
    /// nothing in the suite holds it there.
    /// </remarks>
    public void Asked(MetadataSource source, DateTimeOffset at)
    {
        if (!_asked.TryGetValue(source, out var window))
        {
            window = new Queue<DateTimeOffset>(RequestsPerWindow);
            _asked[source] = window;
        }

        Forget(window, at);
        window.Enqueue(at);
    }

    /// <summary>
    /// Drops the requests that are out of the window ending now.
    /// </summary>
    /// <param name="window">The instants recorded for one source, oldest first.</param>
    /// <param name="now">What the clock reads.</param>
    private static void Forget(Queue<DateTimeOffset> window, DateTimeOffset now)
    {
        var oldest = now - Window;

        while (window.Count > 0 && window.Peek() <= oldest)
        {
            window.Dequeue();
        }
    }
}
