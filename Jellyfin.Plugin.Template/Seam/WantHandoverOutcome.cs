namespace Jellyfin.Plugin.Template.Seam;

/// <summary>
/// What became of one want at the seam.
/// </summary>
/// <remarks>
/// Three answers rather than a boolean, because the case this plugin most has to
/// keep separate is the one a boolean collapses: a server with no requests
/// plugin installed is not a server where the handover failed. The first is the
/// ordinary state of a plugin that is complete alone; the second is something an
/// operator may want to know about.
/// </remarks>
public enum WantHandoverOutcome
{
    /// <summary>
    /// No outcome. What an unset field reads as, and never returned.
    /// </summary>
    None = 0,

    /// <summary>
    /// Nothing implements the seam on this server.
    /// </summary>
    /// <remarks>
    /// The complete state rather than a degraded one, and the state most
    /// installs are in. What happens to the want is the local list in #97, which
    /// records every want whether or not a receiver took it, so this outcome
    /// costs the user nothing.
    /// </remarks>
    NoReceiver = 1,

    /// <summary>
    /// At least one receiver acknowledged the want within the bound.
    /// </summary>
    /// <remarks>
    /// At least one rather than all, because the receivers are independent and
    /// no receiver knows about another. One acknowledgement is the whole of what
    /// this plugin can truthfully report: it says a receiver took the message,
    /// and it says nothing about what happens to the want afterwards, which
    /// there is no route back to learn.
    /// </remarks>
    Accepted = 2,

    /// <summary>
    /// Receivers were present and none of them acknowledged the want.
    /// </summary>
    /// <remarks>
    /// One state for four causes, deliberately: refused, threw, still working
    /// when the bound passed, or the caller cancelled. They are one state
    /// because this plugin does the same thing in all four, which is nothing.
    /// There is no retry, no queue and no delivery guarantee, and a refusal is
    /// not an error on this side.
    ///
    /// Which of the four it was is not lost. It is logged per receiver where it
    /// happens, so an operator looking at a sibling that refuses everything sees
    /// the refusals rather than a count.
    /// </remarks>
    NotAccepted = 3
}
