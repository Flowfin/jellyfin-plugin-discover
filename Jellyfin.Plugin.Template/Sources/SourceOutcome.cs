namespace Jellyfin.Plugin.Template.Sources;

/// <summary>
/// What came back from asking one source for one page of titles.
/// </summary>
/// <remarks>
/// Four answers rather than a result and a failure, because each one leaves the
/// caller with different work and collapsing any pair of them makes a later
/// issue unimplementable. <see cref="Answered"/> with nothing in it is a source
/// that was asked and had nothing, which is a legitimately empty shelf and the
/// state #63 turns on. <see cref="NotConfigured"/> is a source that was asked
/// and should not have been, and nothing is wrong. <see cref="RateLimited"/>
/// and <see cref="TemporarilyFailed"/> differ in what a retry may do, which is
/// #78, and in what an operator is told, which is #92.
///
/// The pair that matters most is <see cref="Answered"/> with no titles against
/// the two failures. #79 asks that a failed fetch never replace good data, and
/// that is not expressible at all if a rate limit, a timeout and a genuinely
/// empty answer arrive at the refresh as the same empty list: keeping the
/// previous contents on an empty answer leaves a stale shelf forever, and
/// dropping them on a failure is the behaviour #79 exists against.
/// </remarks>
public enum SourceOutcome
{
    /// <summary>
    /// No outcome. What an unset field reads as, and never what
    /// <see cref="SourceAnswer"/> carries, because every answer is made through
    /// one of its named constructors.
    /// </summary>
    None = 0,

    /// <summary>
    /// The source was asked and answered. The titles are what it gave, and an
    /// empty set means it had none rather than that anything went wrong.
    /// </summary>
    Answered = 1,

    /// <summary>
    /// The source has nothing to answer with because it has not been set up.
    /// Nothing is wrong and nothing is retried; the shelf that asked should not
    /// have been asked.
    /// </summary>
    NotConfigured = 2,

    /// <summary>
    /// The source refused because it is being asked too often. Asking again
    /// sooner than it allows makes it worse rather than better, so this is the
    /// one outcome that carries how long to wait.
    /// </summary>
    RateLimited = 3,

    /// <summary>
    /// The source could not answer this time, for a reason that may not be true
    /// next time: a timeout, a connection that failed, an error the source
    /// reported about itself.
    /// </summary>
    TemporarilyFailed = 4
}
