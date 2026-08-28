namespace Jellyfin.Plugin.Template.Wants;

/// <summary>
/// Where a want stands on the server that recorded it.
/// </summary>
/// <remarks>
/// #97 needs a state rather than an append-only line, and that follows from the
/// gesture rather than from a preference. The gesture is the favourite flag,
/// answered as question 2 on #2 on 2026-08-24, and a user can unset it, so
/// undoing is a real event this list has to represent.
///
/// A withdrawal is not a deletion. The list's first condition is that it is
/// complete rather than a fallback, so a want that was asked for and taken back
/// is something the operator asked to see; removing the row on un-favourite
/// would make the list disagree with its own first line. That is why this
/// enumeration exists at all instead of a want simply leaving the register.
/// </remarks>
public enum LocalWantState
{
    /// <summary>
    /// What an unset field reads as, and never a state a recorded want is in.
    /// </summary>
    /// <remarks>
    /// Zero is the default of every enumeration in .NET, so a member is spent on
    /// the unset case rather than letting the first real state inherit it. A
    /// register row defaulting into <see cref="Asked"/> would be a want nobody
    /// asked for, shown to the operator as one somebody did.
    /// </remarks>
    None = 0,

    /// <summary>
    /// Somebody asked for this title and has not taken it back.
    /// </summary>
    Asked = 1,

    /// <summary>
    /// Somebody asked for this title and then withdrew.
    /// </summary>
    /// <remarks>
    /// The row stays. What a withdrawal costs the other side of the seam is
    /// #99's and is settled there, that a repeat is the receiver's cue to
    /// ignore; nothing on the seam carries a cancellation today, so this state
    /// is what this server knows and not what it told anybody.
    /// </remarks>
    Withdrawn = 2
}
