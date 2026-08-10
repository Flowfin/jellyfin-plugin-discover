using System;
using System.Globalization;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// How a title is addressed, so that the same title keeps the same address.
/// </summary>
/// <remarks>
/// The address is what the server hashes an item's identity out of, together
/// with the surface's own name, so an address that moves is an item created
/// again and a previous item orphaned with whatever a user had marked on it.
/// That is #60, and it is why a title's address is derived here rather than
/// composed wherever a listing happens to be built.
///
/// The form is the body that issued the identifier and the identifier as that
/// body spells it, separated by a colon: <c>imdb:tt2543164</c>. Nothing else is
/// in it. Not the name, which is translated and is not unique. Not the shelf,
/// so a title that moves between shelves is the same title. Not a position, a
/// page or a fetch time, all of which move for reasons that are not the title.
///
/// Which identifier stands for the title is
/// <see cref="DiscoverTitleIdentity.Primary"/>, and the precedence behind it is
/// argued there. The residual that leaves is real and is written down in
/// <c>docs/title-identity.md</c> rather than only here: an identity that later
/// gains an identifier from a higher-precedence body has a different primary
/// and therefore a different address, which is the one case where a title this
/// plugin has already shown is created again.
/// </remarks>
public static class TitleAddress
{
    /// <summary>
    /// What separates the body from the identifier it issued.
    /// </summary>
    /// <remarks>
    /// Read back at the first one only, so a body that ever spells an
    /// identifier with a colon in it still round-trips.
    /// </remarks>
    private const string Separator = ":";

    /// <summary>
    /// Makes the address of a title.
    /// </summary>
    /// <param name="identity">What makes the title that title.</param>
    /// <returns>The address, which is the same for the same identity every time.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
    public static SurfaceAddress For(DiscoverTitleIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var primary = identity.Primary;

        return SurfaceAddress.Of(TokenFor(primary.Source) + Separator + primary.Value);
    }

    /// <summary>
    /// Reads back the identifier an address was made from.
    /// </summary>
    /// <param name="address">An address the server handed back.</param>
    /// <returns>
    /// The identifier, or null where the address is not a title's. The root and
    /// a shelf's address both answer null, which is what lets one level request
    /// be told from another without a second field travelling beside the
    /// address.
    /// </returns>
    /// <remarks>
    /// Needed because the address is the only thing that survives a round trip
    /// through the server and a client: a user opening a series folder sends
    /// this back and nothing else.
    /// </remarks>
    public static ProviderIdentifier? IdentifierIn(SurfaceAddress address)
    {
        if (address.IsRoot)
        {
            return null;
        }

        var value = address.Value;
        var separator = value.IndexOf(Separator, StringComparison.Ordinal);

        if (separator <= 0 || separator == value.Length - 1)
        {
            return null;
        }

        var source = SourceFor(value[..separator]);

        if (source == MetadataSource.None)
        {
            return null;
        }

        return new ProviderIdentifier(source, value[(separator + Separator.Length)..]);
    }

    /// <summary>
    /// The word that stands for a body inside an address.
    /// </summary>
    /// <remarks>
    /// These three words are pinned. Changing one changes the address of every
    /// title that body identifies, which orphans every one of them on every
    /// server already holding them, and that is the same cost as the rename
    /// this file is about.
    /// </remarks>
    private static string TokenFor(MetadataSource source) => source switch
    {
        MetadataSource.Imdb => "imdb",
        MetadataSource.Tmdb => "tmdb",
        MetadataSource.Tvdb => "tvdb",
        _ => throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "A title's identity named {0}, which has no word to stand for it in an address. A member added to {1} is given one here in the same change.",
                source,
                nameof(MetadataSource)))
    };

    /// <summary>
    /// The body a word inside an address stands for, or none where it stands for no body.
    /// </summary>
    /// <remarks>
    /// Answers <see cref="MetadataSource.None"/> rather than throwing, because
    /// what is being read is a value a client sent back, and an address from an
    /// older version, or a shelf's, is an ordinary thing to arrive here.
    /// </remarks>
    private static MetadataSource SourceFor(string token) => token switch
    {
        "imdb" => MetadataSource.Imdb,
        "tmdb" => MetadataSource.Tmdb,
        "tvdb" => MetadataSource.Tvdb,
        _ => MetadataSource.None
    };
}
