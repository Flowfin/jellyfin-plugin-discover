using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Sources;

/// <summary>
/// What one source gave back when it was asked one question.
/// </summary>
/// <remarks>
/// Made through one of the four named constructors below and never through a
/// constructor of its own, so an answer cannot be built that says it failed and
/// carries titles, or says it answered and carries a wait. Which fields are
/// meaningful follows from <see cref="Outcome"/>, and the constructors are what
/// keep the two from disagreeing.
///
/// Absence is a null here for the same reason it is one on
/// <see cref="DiscoverTitle"/>. A source that gave no message is not a source
/// that gave an empty one, and only one of the two is worth showing an operator.
/// </remarks>
public sealed class SourceAnswer
{
    private static readonly DiscoverTitle[] _nothing = Array.Empty<DiscoverTitle>();

    private readonly DiscoverTitle[] _titles;

    private SourceAnswer(
        SourceOutcome outcome,
        DiscoverTitle[] titles,
        int? totalCount,
        TimeSpan? retryAfter,
        string? sourceMessage)
    {
        Outcome = outcome;
        _titles = titles;
        TotalCount = totalCount;
        RetryAfter = retryAfter;
        SourceMessage = sourceMessage;
    }

    /// <summary>
    /// Gets which of the four answers this is.
    /// </summary>
    public SourceOutcome Outcome { get; }

    /// <summary>
    /// Gets the titles the source gave, in the order it listed them.
    /// </summary>
    /// <remarks>
    /// Empty for every outcome but <see cref="SourceOutcome.Answered"/>, and
    /// that is by construction rather than by a check: none of the three other
    /// constructors takes a title. The order is the source's own and is not an
    /// order a shelf may draw, which is #91.
    /// </remarks>
    public IReadOnlyList<DiscoverTitle> Titles => _titles;

    /// <summary>
    /// Gets how many titles the source says match the question in total, or null where it did not say.
    /// </summary>
    /// <remarks>
    /// Null and zero are different answers. A source that reported no total is
    /// one a caller cannot page through without asking again; a source that
    /// reported zero has said there is nothing behind this page.
    /// </remarks>
    public int? TotalCount { get; }

    /// <summary>
    /// Gets how long the source said to wait, or null where it said nothing.
    /// </summary>
    /// <remarks>
    /// Only ever set on <see cref="SourceOutcome.RateLimited"/>. Null there
    /// means the source refused without saying for how long, which is a
    /// different case from a source that named a wait: what a caller does with
    /// each is #78's backoff rather than this type's.
    /// </remarks>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Gets what the source said about the failure in its own words, or null where it said nothing.
    /// </summary>
    /// <remarks>
    /// Carried because #79 asks that an operator be shown the source's own
    /// message where there is one, and a message this plugin composed instead
    /// would describe what this plugin thought had happened.
    ///
    /// It is a third party's text and nothing here makes it safe to render. It
    /// is not bounded in length and it is not inspected, because a bound chosen
    /// here would be a number with no argument behind it and an inspection
    /// would be this type deciding what #82 decides. Where it reaches an
    /// operator's page, `no-unescaped-render-in-config-page` is what refuses
    /// treating it as markup, and that rule reads the page rather than this
    /// field.
    /// </remarks>
    public string? SourceMessage { get; }

    /// <summary>
    /// The answer of a source that was asked and had not been set up.
    /// </summary>
    /// <returns>An answer carrying no titles and no failure.</returns>
    /// <remarks>
    /// Nothing is wrong and nothing is retried. A caller seeing this has asked
    /// a source it had no business asking, which is a fault in what built the
    /// shelf rather than in the source.
    /// </remarks>
    public static SourceAnswer NotConfigured() =>
        new(SourceOutcome.NotConfigured, _nothing, totalCount: null, retryAfter: null, sourceMessage: null);

    /// <summary>
    /// The answer of a source that refused because it is being asked too often.
    /// </summary>
    /// <param name="retryAfter">
    /// How long the source said to wait, or null where it said nothing.
    /// </param>
    /// <param name="sourceMessage">
    /// What the source said, or null where it said nothing. Blank is stored as
    /// null.
    /// </param>
    /// <returns>An answer carrying the wait and no titles.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the wait is negative. A negative wait would be read by a
    /// backoff as permission to ask again at once, which is the one thing a
    /// rate limit is telling it not to do.
    /// </exception>
    public static SourceAnswer RateLimited(TimeSpan? retryAfter, string? sourceMessage)
    {
        if (retryAfter is { } wait && wait < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryAfter),
                wait,
                "A rate limit says how long to wait. A negative wait reads to a backoff as permission to ask again immediately, which is what the source refused.");
        }

        return new SourceAnswer(
            SourceOutcome.RateLimited,
            _nothing,
            totalCount: null,
            retryAfter,
            Said(sourceMessage));
    }

    /// <summary>
    /// The answer of a source that could not answer this time.
    /// </summary>
    /// <param name="sourceMessage">
    /// What the source said, or null where it said nothing, which is the usual
    /// case for a timeout or a connection that never opened. Blank is stored as
    /// null.
    /// </param>
    /// <returns>An answer carrying no titles.</returns>
    public static SourceAnswer TemporarilyFailed(string? sourceMessage) =>
        new(SourceOutcome.TemporarilyFailed, _nothing, totalCount: null, retryAfter: null, Said(sourceMessage));

    /// <summary>
    /// The answer of a source that was asked and answered.
    /// </summary>
    /// <param name="titles">
    /// What it gave, which may be nothing. Nothing here is a source that has no
    /// titles for this question rather than a source that failed.
    /// </param>
    /// <param name="totalCount">
    /// How many it says match in total, or null where it did not say.
    /// </param>
    /// <returns>An answer carrying the titles.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="titles"/> is null or holds a null. A source
    /// with nothing to give answers with an empty set, and an adapter that
    /// dropped a title it could not map leaves a shorter set rather than a hole
    /// in one.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the total is negative, or is smaller than the number of
    /// titles handed over. A total smaller than the page it describes is a
    /// paging fault, and a caller that trusted it would stop asking before it
    /// had the shelf.
    /// </exception>
    public static SourceAnswer Answered(IEnumerable<DiscoverTitle> titles, int? totalCount)
    {
        ArgumentNullException.ThrowIfNull(titles);

        var given = new List<DiscoverTitle>();

        foreach (var title in titles)
        {
            ArgumentNullException.ThrowIfNull(title, nameof(titles));
            given.Add(title);
        }

        if (totalCount is { } count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(totalCount));
            ArgumentOutOfRangeException.ThrowIfLessThan(count, given.Count, nameof(totalCount));
        }

        return new SourceAnswer(
            SourceOutcome.Answered,
            given.ToArray(),
            totalCount,
            retryAfter: null,
            sourceMessage: null);
    }

    /// <summary>
    /// Turns a message the source did not really give into the absence it is.
    /// </summary>
    /// <param name="message">What arrived.</param>
    /// <returns>The message, or null where there was nothing in it.</returns>
    private static string? Said(string? message) =>
        string.IsNullOrWhiteSpace(message) ? null : message;
}
