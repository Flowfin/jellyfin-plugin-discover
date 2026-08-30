namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// What one run did to one shelf.
/// </summary>
/// <remarks>
/// Five answers rather than a success and a failure, because a shelf that was
/// not asked, a shelf that was asked and answered, and a shelf whose source
/// could not answer leave the catalogue in three different states and an
/// operator with three different things to do.
///
/// The pair this exists for is <see cref="Refreshed"/> against
/// <see cref="PreviousKept"/>. #79 asks that a failed fetch never replace good
/// data, and that is only expressible where the two are different outcomes of
/// one run: a document that was replaced and a document that was left alone are
/// indistinguishable afterwards by looking at the directory, because the one
/// that was left alone is exactly the file that was there before.
/// </remarks>
public enum ShelfRefreshOutcome
{
    /// <summary>
    /// No outcome. What an unset field reads as, and never what a run carries,
    /// because every result is made through one of
    /// <see cref="ShelfRefreshResult"/>'s named constructors.
    /// </summary>
    None = 0,

    /// <summary>
    /// The source answered and this shelf's document now holds what it gave.
    /// </summary>
    /// <remarks>
    /// A source that answered with nothing lands here rather than under
    /// <see cref="PreviousKept"/>. An empty answer is a shelf that has nothing
    /// on it today, which is a fact worth storing, and #63's third condition is
    /// about telling that apart from a shelf nobody has asked yet.
    /// </remarks>
    Refreshed = 1,

    /// <summary>
    /// The shelf is turned off, so it was not asked, not stored and not counted
    /// against anything.
    /// </summary>
    /// <remarks>
    /// #85's fourth condition, in the half a refresh owns. The flag is on the
    /// shelf record; this is the first thing in the tree that reads it.
    /// </remarks>
    TurnedOff = 2,

    /// <summary>
    /// The source could not answer, so whatever this shelf held it still holds.
    /// </summary>
    /// <remarks>
    /// The three ways a source says it has no answer - not set up, rate
    /// limited, temporarily failed - all arrive here, and which of them it was
    /// is on <see cref="ShelfRefreshResult.SourceOutcome"/> rather than being
    /// collapsed into this member. #78 acts on one of the three and #92 shows
    /// an operator all of them, so a single "failed" would be a distinction
    /// thrown away at the one moment it is known.
    /// </remarks>
    PreviousKept = 3,

    /// <summary>
    /// The run was stopped before this shelf was asked, so nothing was fetched
    /// and nothing was written.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="PreviousKept"/> because nothing went wrong.
    /// An operator who cancelled a refresh and then reads that four shelves
    /// failed has been told about a fault that is their own instruction.
    /// </remarks>
    Cancelled = 4
}
