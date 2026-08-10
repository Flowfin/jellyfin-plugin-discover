using System;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// One thing in one level of the surface: a shelf to open, or a title to look at.
/// </summary>
/// <remarks>
/// Made through <see cref="Shelf(SurfaceAddress, string)"/> and
/// <see cref="Of(SurfaceAddress, DiscoverTitle)"/> rather than by setting
/// fields, so the two shapes cannot be half filled in. A shelf with a title
/// hanging off it and a title with a name of its own are both states this type
/// simply has no way to reach.
///
/// A title entry carries a <see cref="DiscoverTitle"/> whole rather than a copy
/// of the fields the surface happens to draw today. What a title carries is
/// #55, and every field added there arrives here without this type changing.
/// </remarks>
public sealed class SurfaceEntry
{
    private SurfaceEntry(SurfaceAddress address, SurfaceEntryKind kind, string name, DiscoverTitle? title)
    {
        Address = address;
        Kind = kind;
        Name = name;
        Title = title;
    }

    /// <summary>
    /// Gets where this entry sits, which is what the server hands back when a user opens it.
    /// </summary>
    public SurfaceAddress Address { get; }

    /// <summary>
    /// Gets what this entry is.
    /// </summary>
    public SurfaceEntryKind Kind { get; }

    /// <summary>
    /// Gets what a client shows for this entry.
    /// </summary>
    /// <remarks>
    /// For a title this is the title's own name and is not composed with
    /// anything else, for the reason <see cref="DiscoverTitle"/> gives: a
    /// composed string is one a later comparison has to take apart again.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets the title this entry stands for, or null when this entry is a shelf.
    /// </summary>
    public DiscoverTitle? Title { get; }

    /// <summary>
    /// Makes an entry for a shelf.
    /// </summary>
    /// <param name="address">Where the shelf sits.</param>
    /// <param name="name">What a client shows for it, which is the operator's name for the shelf.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the address is the root, or when the name is null, empty or
    /// whitespace. The root is the level shelves are listed in rather than a
    /// shelf, and a shelf with a blank name is a row a user cannot tell from a
    /// broken one.
    /// </exception>
    public static SurfaceEntry Shelf(SurfaceAddress address, string name)
    {
        RefuseTheRoot(address);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "A shelf with a blank name would be drawn as an empty row that a user cannot tell from a broken one.",
                nameof(name));
        }

        return new SurfaceEntry(address, SurfaceEntryKind.Shelf, name, null);
    }

    /// <summary>
    /// Makes an entry for a title.
    /// </summary>
    /// <param name="address">Where the title sits, which #60 fixes so it survives a refresh.</param>
    /// <param name="title">The title.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the address is the root.</exception>
    public static SurfaceEntry Of(SurfaceAddress address, DiscoverTitle title)
    {
        ArgumentNullException.ThrowIfNull(title);
        RefuseTheRoot(address);

        return new SurfaceEntry(address, SurfaceEntryKind.Title, title.Name, title);
    }

    private static void RefuseTheRoot(SurfaceAddress address)
    {
        if (address.IsRoot)
        {
            throw new ArgumentException(
                "The root is the level entries are listed in, so nothing in a level can be addressed as it. An entry addressed as the root would be a folder that contains itself.",
                nameof(address));
        }
    }
}
