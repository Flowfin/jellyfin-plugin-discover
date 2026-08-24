using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Sources;

namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// How long this plugin may keep what a source answered with, as one value with
/// the sources it was checked against.
/// </summary>
/// <remarks>
/// A source's terms cap this and do not choose it. TMDB's clause 1.C caps it at
/// six months, which <see cref="IMetadataSource.RetentionCeiling"/> carries as a
/// hundred and eighty days, and what is actually kept is a shorter number chosen
/// against freshness: the shorter it is, the more often this plugin calls out.
/// Ninety days is that number, answered on issue #2 on 2026-08-24 as half the
/// shortest reading of the cap, and <c>docs/sources/tmdb.md</c> carries the
/// reasoning where the clause it sits under is.
///
/// The type exists so that the number and the check on it are one thing. A
/// duration held as a bare <see cref="TimeSpan"/> beside a ceiling nobody
/// compared it against is the failure this is for: the value that breaches a
/// source's terms compiles, reads plausibly, and is found by whoever reads the
/// terms next rather than by anything that runs.
/// </remarks>
public sealed class CatalogueRetention
{
    private CatalogueRetention(TimeSpan duration)
    {
        Duration = duration;
    }

    /// <summary>
    /// Gets the retention this plugin ships with, before an operator says otherwise.
    /// </summary>
    /// <remarks>
    /// Ninety days. Not derived from any ceiling: a default computed as a
    /// fraction of whatever the active sources allow would move the moment a
    /// second source with a shorter clause was added, and an operator would
    /// find their catalogue expiring sooner for a reason nothing told them.
    /// </remarks>
    public static TimeSpan Default { get; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Gets how long a fetched record may be kept.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Reads a configured duration, refusing one no active source allows.
    /// </summary>
    /// <param name="duration">How long records may be kept.</param>
    /// <param name="activeSources">The sources this server is set up to ask.</param>
    /// <returns>The retention, checked against every source handed over.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="activeSources"/> is null, or holds a null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the duration is zero or negative, or when it is longer than
    /// some active source allows. The message names that source and its
    /// ceiling, because "too long" without them leaves an operator guessing
    /// which of the sources they set up is the one objecting.
    /// </exception>
    public static CatalogueRetention Of(TimeSpan duration, IReadOnlyCollection<IMetadataSource> activeSources)
    {
        ArgumentNullException.ThrowIfNull(activeSources);

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "A catalogue retention is how long a fetched record may be kept, so it is a positive duration. Keeping nothing is not spelled as a retention of zero: it is not fetching.");
        }

        foreach (var source in activeSources)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(activeSources));

            if (duration > source.RetentionCeiling)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    duration,
                    $"A retention of {duration.TotalDays} days is longer than {source.Source} allows, which is {source.RetentionCeiling.TotalDays} days. The ceiling is that source's terms rather than this plugin's preference, so the number has to come down or that source has to be turned off.");
            }
        }

        return new CatalogueRetention(duration);
    }

    /// <summary>
    /// Says whether a record fetched at one instant may still be kept and served at another.
    /// </summary>
    /// <param name="fetchedAt">When the source answered, which is <see cref="DiscoverTitle.FetchedAt"/>.</param>
    /// <param name="now">The instant being asked about.</param>
    /// <returns>True while the record is inside the retention, false once it is past it.</returns>
    /// <remarks>
    /// The boundary is inclusive, because the condition this answers is that a
    /// record OLDER than the retention is not served, and a record exactly as
    /// old as the retention is not older than it. One tick past is, and both
    /// sides are asserted rather than left to whichever comparison somebody
    /// typed.
    ///
    /// It is asked rather than scheduled, so a server that was off for a year
    /// gets the same answer on its first request as it would have got every day
    /// it was running. A sweep still has to remove what this refuses to serve,
    /// because not serving something is not the same as not keeping it, and
    /// that half is the rest of #68.
    /// </remarks>
    public bool Holds(DateTimeOffset fetchedAt, DateTimeOffset now) => now - fetchedAt <= Duration;
}
