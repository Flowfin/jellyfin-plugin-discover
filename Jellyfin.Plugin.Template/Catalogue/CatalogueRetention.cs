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
    /// The retention a run actually applies, which is the configured one or a
    /// source's ceiling where that is shorter.
    /// </summary>
    /// <param name="configured">How long an operator says records may be kept.</param>
    /// <param name="activeSources">The sources this server is set up to ask.</param>
    /// <param name="cappedBy">
    /// The source whose ceiling decided the answer, or null where the configured
    /// duration stands as it is.
    /// </param>
    /// <returns>The retention in force, which is never longer than any active source allows.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="activeSources"/> is null, or holds a null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the configured duration is zero or negative, and when a
    /// source declares a ceiling that is. A source permitting no caching at all
    /// is not expressible as a retention, and an adapter declaring one is a
    /// defect in that adapter rather than a state a run should quietly adopt.
    /// </exception>
    /// <remarks>
    /// THIS IS NOT <see cref="Of"/> AND THE DIFFERENCE IS WHICH QUESTION IS
    /// BEING ASKED. <see cref="Of"/> judges a number an operator typed and
    /// refuses one no active source allows, which is #68's third condition and
    /// belongs at the save. This answers what a run does, and a run always has
    /// to do something: a source whose terms are stricter than the configured
    /// number cannot leave this plugin with no retention at all, because the
    /// records are already on disk and the terms already apply to them.
    ///
    /// So it shortens rather than refuses, and it says which source shortened
    /// it. That is the same reason <see cref="Default"/> is not derived from a
    /// ceiling and does not contradict it: the default is a choice about
    /// freshness that stays put, and this is a cap the terms impose on top of
    /// it, which the caller reports rather than absorbs.
    ///
    /// The route that makes the shortening ordinary rather than exceptional is
    /// a second source added after a number was saved. The save that was legal
    /// against one source is not re-judged when another arrives, and the
    /// records fetched under the first are still on disk.
    /// </remarks>
    public static CatalogueRetention InForce(
        TimeSpan configured,
        IReadOnlyCollection<IMetadataSource> activeSources,
        out IMetadataSource? cappedBy)
    {
        ArgumentNullException.ThrowIfNull(activeSources);

        if (configured <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configured),
                configured,
                "A catalogue retention is how long a fetched record may be kept, so it is a positive duration. Keeping nothing is not spelled as a retention of zero: it is not fetching.");
        }

        var duration = configured;
        cappedBy = null;

        foreach (var source in activeSources)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(activeSources));

            if (source.RetentionCeiling <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(activeSources),
                    source.RetentionCeiling,
                    $"{source.Source} declares a retention ceiling of {source.RetentionCeiling.TotalDays} days, which permits no caching at all. That is not a retention this plugin can hold records under, and an adapter declaring one is a defect in that adapter rather than a state a run adopts quietly.");
            }

            if (source.RetentionCeiling < duration)
            {
                duration = source.RetentionCeiling;
                cappedBy = source;
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

    /// <summary>
    /// The records of a stored document that may still be kept and served.
    /// </summary>
    /// <param name="titles">What the document was read as holding.</param>
    /// <param name="now">The instant being asked about.</param>
    /// <returns>
    /// The same records in the same order, minus the ones past the retention,
    /// and the same instance where every one of them is still held.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the titles are null, or hold a null.</exception>
    /// <remarks>
    /// One place asking <see cref="Holds"/> over a set rather than every caller
    /// writing its own loop, which is #68's second condition in the half both
    /// of its verbs share. Not serving a record and not keeping it are two
    /// actions on one answer, and a sweep with a comparison of its own is how
    /// the two come to disagree: the surface would draw what the sweep had
    /// decided to delete, or keep what it would not draw.
    ///
    /// The order is the document's. What decides the order titles are shown in
    /// is <see cref="DiscoverTitleOrder"/> applied where a shelf is written, so
    /// re-sorting here would be a second answer to a question already answered.
    ///
    /// Returning the same instance where nothing expired is what lets a caller
    /// tell "nothing to do" from "everything survived" by reference rather than
    /// by counting, and a caller that counts instead gets the same answer.
    /// </remarks>
    public IReadOnlyList<DiscoverTitle> StillHeld(IReadOnlyList<DiscoverTitle> titles, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(titles);

        List<DiscoverTitle>? held = null;

        for (var index = 0; index < titles.Count; index++)
        {
            var title = titles[index];
            ArgumentNullException.ThrowIfNull(title, nameof(titles));

            if (Holds(title.FetchedAt, now))
            {
                held?.Add(title);

                continue;
            }

            if (held is null)
            {
                held = new List<DiscoverTitle>(titles.Count);

                for (var earlier = 0; earlier < index; earlier++)
                {
                    held.Add(titles[earlier]);
                }
            }
        }

        return held ?? titles;
    }
}
