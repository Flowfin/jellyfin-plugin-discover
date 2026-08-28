namespace Jellyfin.Plugin.Template.Wants;

/// <summary>
/// What the register did with a want it was handed.
/// </summary>
/// <remarks>
/// Returned rather than thrown, and for the same reason
/// <see cref="Jellyfin.Plugin.Template.Sources.SourceAnswer"/> reports its four
/// cases rather than throwing: a caller has to tell them apart and act
/// differently on each, and an exception carries none of that. The one that
/// matters most is <see cref="Refused"/>, because #97's fourth condition asks
/// for a stated behaviour at the bound and a user has to be told rather than
/// left believing the gesture landed.
/// </remarks>
public enum LocalWantOutcome
{
    /// <summary>
    /// What an unset field reads as, and never an answer the register gives.
    /// </summary>
    None = 0,

    /// <summary>
    /// The want was not held before and is now.
    /// </summary>
    Recorded = 1,

    /// <summary>
    /// The want was already standing and nothing changed.
    /// </summary>
    /// <remarks>
    /// The ordinary answer to a gesture this plugin saw twice, which the chosen
    /// gesture makes common: the favourite flag is the server's, so a user can
    /// set it while nothing of this plugin is running, and the list is
    /// reconciled against what the flag says rather than fed by an event this
    /// plugin is guaranteed to see. A reconciliation that answered
    /// <see cref="Recorded"/> for every row it re-read would make every pass
    /// look like a server full of new requests.
    /// </remarks>
    AlreadyStanding = 2,

    /// <summary>
    /// The want had been withdrawn and stands again.
    /// </summary>
    Reasked = 3,

    /// <summary>
    /// The register is full and this want was not taken.
    /// </summary>
    /// <remarks>
    /// The newest is refused rather than the oldest dropped, and that follows
    /// from #97's first condition rather than from a preference: the local list
    /// is complete rather than a fallback, and a list that silently drops its
    /// oldest entries is not complete. Refusing is visible to whoever asked;
    /// dropping is visible to nobody.
    /// </remarks>
    Refused = 4
}
