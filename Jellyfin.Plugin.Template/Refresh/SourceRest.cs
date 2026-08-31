using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;

namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// How long a source that refused is left alone before it is asked again.
/// </summary>
/// <remarks>
/// <para>
/// #78's third and fourth conditions. A source refuses in two of the four ways
/// <see cref="SourceOutcome"/> sets out, and both of them mean the same thing to
/// whoever is about to ask a second question: asking now makes it worse. The
/// adapter says so of itself and decides nothing about when to ask again, so
/// this is where that decision lives, and it lives beside the thing that drives
/// a refresh because a rate budget belongs to a source rather than to a shelf.
/// </para>
/// <para>
/// NOTHING HERE WAITS. A rest is an instant this compares a clock against, not
/// a sleep, so a run that meets a resting source finishes at once with that
/// shelf keeping what it had, and the retry is the next run rather than a
/// second attempt inside this one. That is what makes the whole of it testable
/// against the clock a test advances, which is #78's fifth condition, and it is
/// also the honest shape: a refresh that slept for the six hours the fourth
/// condition allows would hold the server's scheduler for six hours.
/// </para>
/// <para>
/// A wait the source stated is taken as it stands rather than being widened,
/// because a source that named a number has answered the question this type
/// otherwise has to guess at. Where it named none - which the reading in
/// <c>docs/source-api/tmdb.md</c> records as the ordinary case for the one
/// source implemented, since that source's documentation names no rate-limit
/// response header at all - the rest doubles from <see cref="FirstRest"/> per
/// refusal in a row and stops at <see cref="LongestRest"/>.
/// </para>
/// <para>
/// The giving up is what the fourth condition asks for beyond the backoff, and
/// it overrides a stated wait rather than deferring to it. A source that has
/// refused <see cref="Tries"/> times in a row while naming a short wait each
/// time is one this plugin has taken at its word four times and been refused
/// four times, so the fifth attempt is left for <see cref="LongestRest"/> and
/// the operator is told. Reporting that is the caller's, because what an
/// operator reads is a run's business and this type holds no logger.
/// </para>
/// <para>
/// The state lives for as long as the process and no longer, which is the same
/// bound <c>CatalogueRefresh</c>'s count of failures in a row carries and for
/// the same reason: writing it down is a store, and where a shelf's run state
/// is kept is #92. A server that restarts asks every source again, which errs
/// towards one extra request rather than towards a source left alone through a
/// restart nobody connected to it.
/// </para>
/// </remarks>
public sealed class SourceRest
{
    /// <summary>
    /// How many refusals in a row this plugin takes before it stops taking the
    /// source at its word.
    /// </summary>
    /// <remarks>
    /// Four, so that a source having a bad ten minutes is asked again three
    /// times before it is left for <see cref="LongestRest"/>, and a source that
    /// is refusing everything is not asked more than four times whatever it
    /// says about how soon it may be asked again. That is the bound the fourth
    /// condition asks for: without it, a source answering "wait one second"
    /// forever is asked forever.
    /// </remarks>
    public const int Tries = 4;

    /// <summary>
    /// How long a source that refused without naming a wait is left alone the
    /// first time.
    /// </summary>
    /// <remarks>
    /// Five minutes rather than a number nearer the source's own budget,
    /// because the refusal that carries no wait is the one this plugin knows
    /// least about. A refusal for rate with no header and a source that timed
    /// out arrive here as the same value, and a short rest after either is a
    /// second request into a condition nobody has measured.
    /// </remarks>
    public static readonly TimeSpan FirstRest = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The longest a source is left alone, and what it is left alone for once
    /// this plugin has given up on it.
    /// </summary>
    /// <remarks>
    /// Six hours, which is under the cadence a scheduled refresh runs at often
    /// enough that a source coming back is noticed within a day, and long
    /// enough that a source refusing every request costs it four requests a day
    /// from this server rather than one per run.
    /// </remarks>
    public static readonly TimeSpan LongestRest = TimeSpan.FromHours(6);

    private readonly Dictionary<MetadataSource, Rest> _resting = new Dictionary<MetadataSource, Rest>();

    /// <summary>
    /// How much longer this source is being left alone, or null where it may be
    /// asked.
    /// </summary>
    /// <param name="source">The source about to be asked.</param>
    /// <param name="now">What the clock reads.</param>
    /// <returns>
    /// What is left of the rest, or null where there is no rest or it has run
    /// out.
    /// </returns>
    /// <remarks>
    /// The boundary is inclusive of the instant the rest runs out: a rest until
    /// noon is over at noon. A source is asked again one moment early rather
    /// than one moment late nowhere here, because the instant is computed from
    /// the wait the source named and comparing it the other way would ask one
    /// tick inside the window the source refused.
    /// </remarks>
    public TimeSpan? RestingFor(MetadataSource source, DateTimeOffset now)
    {
        if (!_resting.TryGetValue(source, out var rest))
        {
            return null;
        }

        if (now >= rest.Until)
        {
            return null;
        }

        return rest.Until - now;
    }

    /// <summary>
    /// The refusal that put this source to rest, so a shelf that was not asked
    /// reports why rather than reporting nothing.
    /// </summary>
    /// <param name="source">The source that was not asked.</param>
    /// <returns>The answer the source last gave.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown where the source is not resting. A caller reading this without
    /// having been told the source is resting would be handing a shelf a
    /// refusal nobody made.
    /// </exception>
    /// <remarks>
    /// The source's own last answer rather than one composed here. A shelf that
    /// was not asked was not refused either, and inventing a refusal for it
    /// would put a message in front of an operator that no source ever said.
    /// What is true of such a shelf is that it was not refreshed for the reason
    /// the source last gave, and that is exactly the value this returns.
    /// </remarks>
    public SourceAnswer Standing(MetadataSource source)
    {
        if (!_resting.TryGetValue(source, out var rest))
        {
            throw new InvalidOperationException(
                "A source that is not resting has no standing refusal, and composing one here would report a refusal no source made.");
        }

        return rest.Answer;
    }

    /// <summary>
    /// Records that this source refused, and says how long it is left alone for.
    /// </summary>
    /// <param name="source">The source that refused.</param>
    /// <param name="answer">What it said instead of answering.</param>
    /// <param name="now">What the clock reads.</param>
    /// <returns>The rest that was taken.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the answer is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown where the answer is not one of the two refusals. A source that
    /// answered and a source that has not been set up are not sources to leave
    /// alone: the first has nothing wrong with it, and the second is a fault in
    /// what built the shelf rather than in the source, which is what
    /// <see cref="SourceOutcome.NotConfigured"/> says of itself.
    /// </exception>
    public SourceRestTaken Refused(MetadataSource source, SourceAnswer answer, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.Outcome is not (SourceOutcome.RateLimited or SourceOutcome.TemporarilyFailed))
        {
            throw new ArgumentOutOfRangeException(
                nameof(answer),
                answer.Outcome,
                "Only a source that refused is left alone. An answer and a source that has not been set up are neither a refusal nor a reason to stop asking.");
        }

        _resting.TryGetValue(source, out var before);

        var refusals = Math.Min(before.Refusals + 1, Tries);
        var gaveUp = refusals >= Tries;
        var rest = gaveUp ? LongestRest : Backoff(answer, refusals);

        _resting[source] = new Rest(refusals, now + rest, answer);

        return new SourceRestTaken(rest, now + rest, refusals, gaveUp);
    }

    /// <summary>
    /// Records that this source answered, so the next refusal starts the
    /// backoff again rather than continuing the last one.
    /// </summary>
    /// <param name="source">The source that answered.</param>
    /// <remarks>
    /// The count is of refusals IN A ROW, so one answer clears it. A count that
    /// survived an answer would leave a source that fails once a week on the
    /// longest rest after a month of working, which is a plugin that stops
    /// asking a source that is fine.
    /// </remarks>
    public void Answered(MetadataSource source) => _resting.Remove(source);

    /// <summary>
    /// The rest a refusal earns, before the giving up is applied.
    /// </summary>
    /// <param name="answer">What the source said.</param>
    /// <param name="refusals">How many refusals in a row this is.</param>
    /// <returns>How long to leave the source alone.</returns>
    /// <remarks>
    /// A stated wait is taken as it stands and is not doubled, because doubling
    /// a number the source chose would be this plugin deciding it knows the
    /// source's budget better than the source does. What the repeated refusals
    /// buy against a source that keeps naming a short wait is the giving up
    /// above rather than a longer wait here.
    ///
    /// The doubling is computed by shifting rather than by
    /// <c>Math.Pow</c> so that no floating point value stands between a
    /// declared duration and the instant a source is asked again, and the shift
    /// cannot run away because <paramref name="refusals"/> is capped at
    /// <see cref="Tries"/> before it arrives.
    /// </remarks>
    private static TimeSpan Backoff(SourceAnswer answer, int refusals)
    {
        if (answer.RetryAfter is { } stated)
        {
            return stated > LongestRest ? LongestRest : stated;
        }

        var doubled = FirstRest * (1L << (refusals - 1));

        return doubled > LongestRest ? LongestRest : doubled;
    }

    /// <summary>
    /// What is known about a source that refused.
    /// </summary>
    /// <param name="Refusals">How many refusals in a row it has given.</param>
    /// <param name="Until">The instant it may be asked again.</param>
    /// <param name="Answer">The refusal it last gave.</param>
    private readonly record struct Rest(int Refusals, DateTimeOffset Until, SourceAnswer Answer);
}
