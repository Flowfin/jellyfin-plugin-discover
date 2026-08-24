using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// Everything the discover surface has to answer, in this plugin's own vocabulary.
/// </summary>
/// <remarks>
/// A discover page is a server channel, decided in #51, and the server's
/// channel interface is what that decision produced. This interface is the same
/// set of questions asked in words this plugin chose, and
/// <c>DiscoverSurfaceAdapter</c> is the one place the two vocabularies meet.
///
/// The point of the seam is not tidiness. Without it the server's item shape,
/// its folder kinds and its paging query spread into the shelf code, the
/// catalogue code and the seam to a sibling, and every test that touches any of
/// them drags the server's assembly graph in behind it. That is what #49 needs
/// not to be true, and it is what `no-channel-type-outside-surface` refuses.
///
/// Implementing this is #53's work, and it is where the values in
/// <see cref="Description"/> and <see cref="Capabilities"/> are chosen.
/// </remarks>
public interface IDiscoverSurface
{
    /// <summary>
    /// Gets what the surface calls itself.
    /// </summary>
    SurfaceDescription Description { get; }

    /// <summary>
    /// Gets what the surface tells the server it can do.
    /// </summary>
    SurfaceCapabilities Capabilities { get; }

    /// <summary>
    /// Gets the kinds of picture this surface has of itself.
    /// </summary>
    /// <remarks>
    /// Asked separately from <see cref="Image(SurfaceImageKind)"/> because the
    /// server asks what there is before asking for any of it, and answering the
    /// first question by producing every picture would read them all to count
    /// them.
    /// </remarks>
    IReadOnlyList<SurfaceImageKind> ImageKinds { get; }

    /// <summary>
    /// Says whether the surface exists at all for one user.
    /// </summary>
    /// <param name="userId">Who is asking.</param>
    /// <returns>
    /// <see langword="true"/> when the surface is theirs to browse.
    /// </returns>
    /// <remarks>
    /// Deciding this per user is #57. Seeing the surface is not the same as
    /// being allowed to ask for a title, which is #98 and is a separate answer.
    /// </remarks>
    bool IsAvailableTo(Guid userId);

    /// <summary>
    /// Gets one picture of the surface.
    /// </summary>
    /// <param name="kind">Which picture.</param>
    /// <returns>The picture, or null where this surface has none of that kind.</returns>
    SurfaceImage? Image(SurfaceImageKind kind);

    /// <summary>
    /// Answers one user's request for one level of the surface.
    /// </summary>
    /// <param name="request">Which level, for whom, and how much of it.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>What that level holds.</returns>
    /// <remarks>
    /// A request for a level this surface does not recognise is answered with
    /// <see cref="SurfaceListing.NoSuchLevel"/> rather than by throwing,
    /// because an address from an older version whose shelf no longer exists is
    /// an ordinary thing for a client to send. A level this surface does
    /// recognise and which holds nothing is
    /// <see cref="SurfaceListing.EmptyLevel"/> instead, and the two differ in
    /// the total rather than in the entries. That is #54, answered: a client
    /// that cannot tell the two apart draws a shelf that is gone as a shelf
    /// standing empty, and waits for it to fill.
    /// </remarks>
    Task<SurfaceListing> ListAsync(SurfaceLevelRequest request, CancellationToken cancellationToken);
}
