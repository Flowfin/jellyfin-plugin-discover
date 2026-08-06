using System;

namespace Jellyfin.Plugin.Template.Time;

/// <summary>
/// The only thing in this plugin that answers what time it is.
/// </summary>
/// <remarks>
/// Half of what this plugin will do is time: a cache that expires, a retention
/// limit, a refresh cadence, a backoff, a rate window. Read where it stands,
/// the wall clock makes every one of those testable only by sleeping, and a
/// test that sleeps is a test that gets deleted the first time the runner is
/// slow.
///
/// The freshness a user sees is already the sum of two timers rather than one,
/// because the server caches what a channel returned for a fixed period of its
/// own. Reasoning about that sum needs a clock a test can hold still.
///
/// The invariant <c>no-wall-clock</c> refuses a direct read anywhere but the
/// one implementation that supplies this.
/// </remarks>
public interface IClock
{
    /// <summary>
    /// Gets the current instant, in UTC.
    /// </summary>
    /// <remarks>
    /// UTC and not local time, because every value this plugin stores or
    /// compares outlives the session that wrote it, and a server that changes
    /// offset twice a year would otherwise move a stored expiry under the code
    /// that reads it.
    /// </remarks>
    DateTimeOffset UtcNow { get; }
}
