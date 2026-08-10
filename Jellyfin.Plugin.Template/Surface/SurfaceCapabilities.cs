using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// What the surface tells the server it can do.
/// </summary>
/// <remarks>
/// Only what this plugin has an answer for. The server's own feature record
/// carries several more, among them downloading and a sort toggle, and every
/// one of them is something this surface does not offer rather than something
/// nobody has got to. Leaving them out of this type is what stops a later
/// reader taking a default for a decision; where they are set is one place, in
/// the adapter, with a line each.
/// </remarks>
public sealed class SurfaceCapabilities
{
    private readonly DiscoverTitleKind[] _titleKinds = Array.Empty<DiscoverTitleKind>();
    private readonly int? _maximumPageSize;

    /// <summary>
    /// Gets the kinds of title this surface puts in front of a user. Never empty.
    /// </summary>
    /// <remarks>
    /// The server draws a movie and a series differently and materialises them
    /// as different things, so a surface that offers only one of them says so
    /// rather than letting a client find out per item.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the set is empty or holds
    /// <see cref="DiscoverTitleKind.None"/>. A surface offering no kind of
    /// title is one with nothing to show, which is a configuration state rather
    /// than a capability.
    /// </exception>
    public required IReadOnlyList<DiscoverTitleKind> TitleKinds
    {
        get => _titleKinds;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            var kinds = value.Distinct().ToArray();

            if (kinds.Length == 0 || kinds.Contains(DiscoverTitleKind.None))
            {
                throw new ArgumentException(
                    "A surface offering no kind of title has nothing to show. An empty set is not the same as a shelf that happens to be empty today, which is #63.",
                    nameof(value));
            }

            _titleKinds = kinds;
        }
    }

    /// <summary>
    /// Gets the most entries the surface will answer one request with, or null for no limit of its own.
    /// </summary>
    /// <remarks>
    /// A ceiling on one answer rather than on the catalogue. How many titles
    /// this plugin may write into the operator's library database at all is
    /// #58, and it is a different number in a different place.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is zero or negative. A page size of nothing is a
    /// surface that answers every request with an empty level, which reads to
    /// every client as a plugin that is broken.
    /// </exception>
    public int? MaximumPageSize
    {
        get => _maximumPageSize;
        init
        {
            if (value is { } size)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size, nameof(value));
            }

            _maximumPageSize = value;
        }
    }
}
