using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// What one level of the surface holds, as the answer to one request for it.
/// </summary>
/// <remarks>
/// The count is separate from the entries so a level answered as a page can
/// still say how large it is, and a null count is this surface saying it does
/// not know rather than saying zero.
/// <para>
/// Who reads either is worth stating, because it is not a client. The server
/// builds its query for a channel level with a user, a sort and a folder and
/// with no start index and no limit, so no page is ever asked for; and it
/// answers the level out of the library rather than out of what the channel
/// returned, so the count never leaves this assembly. Both hold at the two
/// targeted lines and the commands are on #54. What the field is for here is
/// this plugin's own callers, and the pair of named answers below, which tell
/// a level that holds nothing from one that is not there.
/// </para>
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
    /// paging fault, and it is refused where the value is made rather than
    /// carried onward, because a listing that says fewer than it holds is
    /// incoherent whoever reads it. Nobody outside this plugin does, per the
    /// remark on the type.
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
    /// the level is empty. <see cref="NoSuchLevel"/> is the other half of the
    /// pair and says the level is not one this surface has. How far that
    /// difference travels is written at the other half.
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
    /// return.
    /// <para>
    /// The difference stops at this plugin. The server takes a total from a
    /// channel and answers a level out of the library instead of out of what
    /// the channel returned, on both targeted lines, so the one field these
    /// two values differ in reaches nobody outside this assembly. What that
    /// costs an operator is a row on <c>docs/limits.md</c> and the commands
    /// behind the reading are on #54. Nothing here reads the server's source,
    /// so this paragraph is a claim rather than something a run refuses, and
    /// it is the paragraph to re-derive before it is relied on.
    /// </para>
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
