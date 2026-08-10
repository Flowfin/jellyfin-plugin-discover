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
    /// Gets the empty level, which is what a request for a level this surface does not recognise answers with.
    /// </summary>
    /// <remarks>
    /// A named value rather than a new empty listing at each call site, because
    /// "the level is empty" and "there is no such level" are answered the same
    /// way on purpose, per #54, and a reader should be able to see that the two
    /// call sites really do return one thing.
    /// </remarks>
    public static SurfaceListing Empty { get; } = new SurfaceListing(_nothing, 0);

    /// <summary>
    /// Gets what the level holds, in the order a client is to draw it.
    /// </summary>
    public IReadOnlyList<SurfaceEntry> Entries => _entries;

    /// <summary>
    /// Gets how many entries the level holds in total, or null where this surface does not know.
    /// </summary>
    public int? TotalCount { get; }
}
