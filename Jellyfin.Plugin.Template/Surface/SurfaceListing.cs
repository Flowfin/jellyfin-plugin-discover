using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// What one level of the surface holds, as the answer to one request for it.
/// </summary>
/// <remarks>
/// The count is separate from the entries because a request can ask for a page
/// of a level, and a client drawing a scrollbar needs to know how much is
/// behind the page it was given. A null count is this surface saying it does
/// not know rather than saying zero, and those are answers a client draws
/// differently.
/// </remarks>
public sealed class SurfaceListing
{
    private static readonly SurfaceEntry[] _nothing = Array.Empty<SurfaceEntry>();

    private readonly SurfaceEntry[] _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurfaceListing"/> class.
    /// </summary>
    /// <param name="entries">What the level holds, in the order a client is to draw it.</param>
    /// <param name="totalCount">
    /// How many entries the level holds in total, or null where this surface
    /// does not know. Where the entries are the whole level rather than a page
    /// of it, this is their count.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null, or holds a null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the count is negative, or is smaller than the number of
    /// entries handed over. A total smaller than the page it describes is a
    /// paging fault, and a client asked to draw it shows a scrollbar that ends
    /// before the rows do.
    /// </exception>
    public SurfaceListing(IEnumerable<SurfaceEntry> entries, int? totalCount)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries = entries.ToArray();

        foreach (var entry in _entries)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(entries));
        }

        if (totalCount is { } count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(totalCount));
            ArgumentOutOfRangeException.ThrowIfLessThan(count, _entries.Length, nameof(totalCount));
        }

        TotalCount = totalCount;
    }

    /// <summary>
    /// Gets the answer for a level this surface recognises and which holds nothing.
    /// </summary>
    /// <remarks>
    /// The total is zero, which is this surface saying it knows the level and
    /// the level is empty. That is the half of the pair a client draws as a
    /// shelf standing empty rather than as a shelf that has gone, and
    /// <see cref="NoSuchLevel"/> is the other half.
    /// </remarks>
    public static SurfaceListing EmptyLevel { get; } = new SurfaceListing(_nothing, 0);

    /// <summary>
    /// Gets the answer for a level this surface does not recognise.
    /// </summary>
    /// <remarks>
    /// No entries and a null total, which is this surface saying it does not
    /// know the level rather than saying the level holds nothing. The two were
    /// one value until #54 was answered, and one value cannot tell a shelf that
    /// is configured and empty from an address whose shelf has been removed, so
    /// a test for the second case passed on whatever the first case happened to
    /// return and a client had nothing to draw the difference from.
    /// <para>
    /// What separates them is the total and nothing else, so the bound is worth
    /// stating: a level this surface recognises always states its total, which
    /// is what makes a null total readable as "no such level" here. A surface
    /// that answered null for a page of a level it does know would spend the
    /// distinction, which is why <see cref="EmptyLevel"/> and this value are
    /// the two answers rather than a convention each call site re-invents.
    /// </para>
    /// </remarks>
    public static SurfaceListing NoSuchLevel { get; } = new SurfaceListing(_nothing, null);

    /// <summary>
    /// Gets what the level holds, in the order a client is to draw it.
    /// </summary>
    public IReadOnlyList<SurfaceEntry> Entries => _entries;

    /// <summary>
    /// Gets how many entries the level holds in total, or null where this surface does not know.
    /// </summary>
    public int? TotalCount { get; }
}
