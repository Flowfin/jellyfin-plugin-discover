using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// What this plugin's discover surface calls itself and what it tells the server it can do.
/// </summary>
/// <remarks>
/// The values here are chosen rather than defaulted, which is #53. Each one
/// carries the reason it is that value beside it, because a field left at
/// whatever a constructor produces reads afterwards exactly like a decision and
/// nothing distinguishes the two.
///
/// Registration is the other half of the same issue and is in
/// <c>PluginServiceRegistrator</c>. Without it this class is a plugin that
/// loads, logs nothing and does not exist as far as any client is concerned,
/// which is the failure #53 is written against and the one no test in this
/// repository can see.
///
/// What a level holds is #54 and is not here. The levels are answered empty
/// until it lands, which is the same answer this surface gives an address it
/// does not recognise.
/// </remarks>
public sealed class DiscoverSurface : IDiscoverSurface
{
    /// <summary>
    /// The kinds of title the surface puts in front of a user.
    /// </summary>
    /// <remarks>
    /// Both, because the catalogue record carries both and the adapter has a
    /// server shape for each. A shelf that offers only films is a decision
    /// about that shelf, which is #86, rather than a narrowing of what the
    /// surface can carry, and stating one kind here would make adding the other
    /// a change to what every client was told.
    /// </remarks>
    private static readonly DiscoverTitleKind[] _kinds = new[]
    {
        DiscoverTitleKind.Movie,
        DiscoverTitleKind.Series
    };

    /// <summary>
    /// The pictures the surface has of itself, which are none.
    /// </summary>
    /// <remarks>
    /// Empty because there is no artwork for this surface in the tree. The
    /// alternative available today is the plugin's own logo, and #53 asks that
    /// the surface's name, description and images be its own rather than the
    /// plugin's repeated, so borrowing it would meet the condition by breaking
    /// what the condition is about. A client draws a tile with no picture as a
    /// tile with no picture, which is visible; a borrowed one is not.
    /// </remarks>
    private static readonly SurfaceImageKind[] _pictures = Array.Empty<SurfaceImageKind>();

    private static readonly SurfaceDescription _description = new SurfaceDescription
    {
        // What a user reads on the library tile. It is also load bearing beyond
        // display: the server hashes an item's identity out of this name and
        // the external identifier together, per #60, so it is fixed from the
        // first release rather than adjustable afterwards.
        Name = "Discover",

        // What the surface says about itself. It says what a user gets and what
        // they do not, because a discover page that reads as a library is one
        // people try to play. Where the notice a source requires is rendered is
        // #76, and this is one of the two places named there.
        Summary = "Films and series this server does not have, so you can see what is out there. Nothing here is playable.",

        // The token the server compares to decide whether to reconsider what it
        // kept. Constant because nothing this surface answers changes yet:
        // every level is empty until #54, and deriving the token from the
        // catalogue is #61. A token derived from nothing would move for reasons
        // that are not changes.
        CatalogueVersion = "0",

        // Everybody the operator lets in. This plugin classifies nothing, and
        // one band on the whole surface is not where the two real controls
        // live: what may be fetched at all is #93 and who sees the surface is
        // #57. Declaring the adult band here instead would hide the surface
        // behind a server setting while leaving both of those undone.
        Audience = SurfaceAudience.General

        // No home page. There is nowhere to send a reader that says anything
        // about this surface, and the source's own site is the source's rather
        // than this plugin's. Absent is the honest answer until there is a
        // page, which is #111.
    };

    private static readonly SurfaceCapabilities _capabilities = new SurfaceCapabilities
    {
        TitleKinds = _kinds,

        // No ceiling of the surface's own, and this is the value with the
        // clearest cost either way. The server derives whether a client may
        // filter the surface at all from whether this is set, on both lines
        // this repository builds against:
        //
        //     git grep -n 'CanFilter = ' v10.11.11 v12.0-rc4 -- src/Jellyfin.LiveTv/Channels/ChannelManager.cs
        //     v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:574:                CanFilter = !features.MaxPageSize.HasValue,
        //     v12.0-rc4:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:572:                CanFilter = !features.MaxPageSize.HasValue,
        //
        // So any number here turns filtering off for every client, and the only
        // things that would justify paying that are a source page size and a
        // per-shelf cap. Neither exists: the source's own reference does not
        // state how many results a page returns, which docs/source-api/tmdb.md
        // records, and the cap is #58. Honouring the paging the server asks for
        // is #61 and is a separate promise from declaring a ceiling on it.
        MaximumPageSize = null
    };

    /// <inheritdoc />
    public SurfaceDescription Description => _description;

    /// <inheritdoc />
    public SurfaceCapabilities Capabilities => _capabilities;

    /// <inheritdoc />
    public IReadOnlyList<SurfaceImageKind> ImageKinds => _pictures;

    /// <inheritdoc />
    /// <remarks>
    /// Every user, which is not #57's answer and is not offered as one. #57
    /// decides this per user against a configuration that does not exist, and
    /// what makes the permissive direction safe meanwhile is that there is
    /// nothing behind the surface: every level is empty until #54 and nothing
    /// fetches anything until M6. It stops being safe the moment the catalogue
    /// holds a title, so #57 lands before anything does.
    /// </remarks>
    public bool IsAvailableTo(Guid userId) => true;

    /// <inheritdoc />
    /// <remarks>
    /// Nothing, for every kind, because <see cref="ImageKinds"/> offers none.
    /// The server asks for what it likes rather than only for what was offered,
    /// so this answers rather than throwing.
    /// </remarks>
    public SurfaceImage? Image(SurfaceImageKind kind) => null;

    /// <inheritdoc />
    /// <remarks>
    /// The empty level for every address, including the root. What a level
    /// holds is #54: one folder per shelf at the top and titles inside. Until
    /// that lands there are no shelves to list, and answering empty is what
    /// this surface already owes an address it does not recognise, so the two
    /// cases are one answer rather than a placeholder that has to be found and
    /// removed later.
    /// </remarks>
    public Task<SurfaceListing> ListAsync(SurfaceLevelRequest request, CancellationToken cancellationToken)
    {
        _ = request.Validated();

        return Task.FromResult(SurfaceListing.Empty);
    }
}
