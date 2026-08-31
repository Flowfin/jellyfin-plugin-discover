using System;
using Jellyfin.Plugin.Template.Sources;

namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// What one run did to one shelf, with the reason beside it.
/// </summary>
/// <remarks>
/// Made through one of the named constructors below and never through a
/// constructor of its own, so a result cannot be built that says the previous
/// contents were kept and also reports titles written, or that says a shelf was
/// refreshed and carries a source failure. Which fields are meaningful follows
/// from <see cref="Outcome"/>, and the constructors are what keep the two from
/// disagreeing. <see cref="SourceAnswer"/> is the same shape one layer down and
/// is where that argument is written.
///
/// The shelf is named by its display name and by its document rather than
/// carried as a record. A result outlives the run that made it and is read by
/// an operator's page, and a shelf definition that had changed in between would
/// make the result describe a shelf that no longer exists; the document name is
/// what a reader can go and look at on the disk.
/// </remarks>
public sealed class ShelfRefreshResult
{
    private ShelfRefreshResult(
        string shelfName,
        string documentName,
        ShelfRefreshOutcome outcome,
        SourceOutcome sourceOutcome,
        int titlesWritten,
        string? sourceMessage,
        int consecutiveFailures)
    {
        ShelfName = shelfName;
        DocumentName = documentName;
        Outcome = outcome;
        SourceOutcome = sourceOutcome;
        TitlesWritten = titlesWritten;
        SourceMessage = sourceMessage;
        ConsecutiveFailures = consecutiveFailures;
    }

    /// <summary>
    /// Gets what the shelf is called, which is what an operator sees.
    /// </summary>
    public string ShelfName { get; }

    /// <summary>
    /// Gets the document this shelf's titles are kept in.
    /// </summary>
    public string DocumentName { get; }

    /// <summary>
    /// Gets what this run did to the shelf.
    /// </summary>
    public ShelfRefreshOutcome Outcome { get; }

    /// <summary>
    /// Gets how the source answered, or <see cref="SourceOutcome.None"/> where
    /// it was not asked.
    /// </summary>
    /// <remarks>
    /// None here is a source that was never asked rather than a source that
    /// answered with nothing, and the two are the states #63 exists to keep
    /// apart. A shelf that is off and a shelf the run never reached both carry
    /// it.
    /// </remarks>
    public SourceOutcome SourceOutcome { get; }

    /// <summary>
    /// Gets how many titles were written into this shelf's document.
    /// </summary>
    /// <remarks>
    /// Zero for every outcome but <see cref="ShelfRefreshOutcome.Refreshed"/>,
    /// and that is by construction rather than by a check: none of the other
    /// three constructors takes a count. Zero on a refreshed shelf is a source
    /// that answered with nothing, which is a document holding an empty list
    /// rather than a document that was not written.
    /// </remarks>
    public int TitlesWritten { get; }

    /// <summary>
    /// Gets what the source said about the failure in its own words, or null
    /// where it said nothing.
    /// </summary>
    /// <remarks>
    /// #79's third condition asks that an operator be shown the source's own
    /// message where there is one, so it is carried from the answer rather than
    /// replaced by a sentence this plugin composed about what it thought had
    /// happened. It is a third party's text and nothing here makes it safe to
    /// render, which is the paragraph <see cref="SourceAnswer.SourceMessage"/>
    /// carries and is not repeated as a second copy.
    /// </remarks>
    public string? SourceMessage { get; }

    /// <summary>
    /// Gets how many runs in a row this shelf's source has failed, counting
    /// this one.
    /// </summary>
    /// <remarks>
    /// #79's fourth condition, which asks that a source that has failed
    /// repeatedly be reported differently from one that failed once, so a
    /// standing misconfiguration does not read as a blip. A number rather than
    /// a flag, because where a reader draws the line between the two is theirs
    /// and a flag would be this type taking it.
    ///
    /// One on the first failure and zero on every other outcome. A source that
    /// answered has not failed; a shelf that is turned off and a shelf a run
    /// never reached were not asked, and reporting either as a failure would
    /// tell an operator about a fault that is their own instruction or their
    /// own cancellation.
    ///
    /// A source reporting that it has not been set up is not counted either,
    /// and that is a decision rather than an oversight.
    /// <see cref="SourceOutcome.NotConfigured"/> says in its own words that
    /// nothing is wrong and nothing is retried, so counting it would climb on
    /// every server that has configured no source and make the one number that
    /// separates a standing fault from a blip read as a standing fault
    /// everywhere.
    /// </remarks>
    public int ConsecutiveFailures { get; }

    /// <summary>
    /// The result of a shelf whose source answered and whose document was replaced.
    /// </summary>
    /// <param name="shelfName">What the shelf is called.</param>
    /// <param name="documentName">The document its titles are kept in.</param>
    /// <param name="titlesWritten">How many titles went into it.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">Thrown when either name is absent or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is negative.</exception>
    public static ShelfRefreshResult Refreshed(string shelfName, string documentName, int titlesWritten)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfName);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        ArgumentOutOfRangeException.ThrowIfNegative(titlesWritten);

        return new ShelfRefreshResult(
            shelfName,
            documentName,
            ShelfRefreshOutcome.Refreshed,
            SourceOutcome.Answered,
            titlesWritten,
            sourceMessage: null,
            consecutiveFailures: 0);
    }

    /// <summary>
    /// The result of a shelf that is turned off.
    /// </summary>
    /// <param name="shelfName">What the shelf is called.</param>
    /// <param name="documentName">The document its titles are kept in.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">Thrown when either name is absent or blank.</exception>
    public static ShelfRefreshResult TurnedOff(string shelfName, string documentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfName);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        return new ShelfRefreshResult(
            shelfName,
            documentName,
            ShelfRefreshOutcome.TurnedOff,
            SourceOutcome.None,
            titlesWritten: 0,
            sourceMessage: null,
            consecutiveFailures: 0);
    }

    /// <summary>
    /// The result of a shelf whose source could not answer.
    /// </summary>
    /// <param name="shelfName">What the shelf is called.</param>
    /// <param name="documentName">The document its titles are kept in.</param>
    /// <param name="answer">What the source said instead of answering.</param>
    /// <param name="consecutiveFailures">
    /// How many runs in a row this shelf's source has failed, counting this
    /// one, or zero where the source reported that it has not been set up.
    /// </param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">Thrown when either name is absent or blank.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the answer is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the answer is one the source did give, because a shelf whose
    /// source answered is refreshed rather than kept and building this result
    /// from it would record a failure that did not happen.
    /// </exception>
    public static ShelfRefreshResult PreviousKept(
        string shelfName,
        string documentName,
        SourceAnswer answer,
        int consecutiveFailures)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfName);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentOutOfRangeException.ThrowIfNegative(consecutiveFailures);

        if (answer.Outcome is SourceOutcome.Answered or SourceOutcome.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(answer),
                answer.Outcome,
                "A shelf keeps what it had because its source did not answer. An answer is a refresh, and None is what an unset field reads as.");
        }

        if (answer.Outcome is SourceOutcome.NotConfigured && consecutiveFailures != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(consecutiveFailures),
                consecutiveFailures,
                "A source that has not been set up has not failed. Counting it would climb on every server that configured no source, and the number that separates a standing fault from a blip would read as a standing fault everywhere.");
        }

        return new ShelfRefreshResult(
            shelfName,
            documentName,
            ShelfRefreshOutcome.PreviousKept,
            answer.Outcome,
            titlesWritten: 0,
            answer.SourceMessage,
            consecutiveFailures);
    }

    /// <summary>
    /// The result of a shelf the run was stopped before reaching.
    /// </summary>
    /// <param name="shelfName">What the shelf is called.</param>
    /// <param name="documentName">The document its titles are kept in.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">Thrown when either name is absent or blank.</exception>
    public static ShelfRefreshResult Cancelled(string shelfName, string documentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shelfName);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        return new ShelfRefreshResult(
            shelfName,
            documentName,
            ShelfRefreshOutcome.Cancelled,
            SourceOutcome.None,
            titlesWritten: 0,
            sourceMessage: null,
            consecutiveFailures: 0);
    }

    /// <summary>
    /// The result of a shelf whose kept document had records past the retention.
    /// </summary>
    /// <param name="previous">The result the sweep replaces, which says why the shelf was not refreshed.</param>
    /// <param name="titlesKept">How many of its records were still held and were written back.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="previous"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the count is negative, or when the result being replaced is
    /// not one that left a document standing. A refreshed shelf's document was
    /// written by this run out of what a source has just answered, so nothing
    /// in it can be past the retention, and a cancelled shelf was not reached
    /// at all.
    /// </exception>
    /// <remarks>
    /// Derived from the result it replaces rather than built from parts, which
    /// is what keeps the sweep from erasing the reason. An operator reading a
    /// page wants both halves of the sentence: the source has not answered for
    /// however many runs, AND what the shelf was holding has now gone. Rebuilt
    /// from a shelf name and a count, this would answer only the second.
    /// </remarks>
    public static ShelfRefreshResult Expired(ShelfRefreshResult previous, int titlesKept)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentOutOfRangeException.ThrowIfNegative(titlesKept);

        if (previous.Outcome is not (ShelfRefreshOutcome.PreviousKept or ShelfRefreshOutcome.TurnedOff))
        {
            throw new ArgumentOutOfRangeException(
                nameof(previous),
                previous.Outcome,
                "A sweep only reaches a shelf whose document this run left standing, which is one whose source did not answer and one that is turned off. Any other outcome either wrote the document in this run or never looked at it.");
        }

        return new ShelfRefreshResult(
            previous.ShelfName,
            previous.DocumentName,
            ShelfRefreshOutcome.Expired,
            previous.SourceOutcome,
            titlesKept,
            previous.SourceMessage,
            previous.ConsecutiveFailures);
    }
}
