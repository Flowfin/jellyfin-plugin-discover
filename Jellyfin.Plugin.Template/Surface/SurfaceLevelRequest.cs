using System;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// One user asking for one level of the surface.
/// </summary>
/// <remarks>
/// Everything the surface is allowed to know about the ask, and nothing else.
/// The server's own query carries a sort field as well, and it is deliberately
/// not here: the order is the plugin's, per #54, and #91 is where a stable one
/// is decided. Carrying a sort this surface does not honour would let a caller
/// believe it had asked for something.
/// </remarks>
/// <param name="Parent">
/// Which level is being asked for. <see cref="SurfaceAddress.Root"/> is the top
/// level, which is what the server asks for when a user opens the surface.
/// </param>
/// <param name="UserId">
/// Who is asking. Carried because whether the surface exists for a user at all
/// is #57 and whether a shelf depends on what they watched is #90, and both are
/// per user.
/// </param>
/// <param name="StartIndex">
/// How many entries to skip, or null for the beginning. Paging is #61.
/// </param>
/// <param name="Limit">
/// How many entries to return at most, or null for however many the level
/// holds.
/// </param>
public readonly record struct SurfaceLevelRequest(
    SurfaceAddress Parent,
    Guid UserId,
    int? StartIndex,
    int? Limit)
{
    /// <summary>
    /// Refuses a request that could not be answered.
    /// </summary>
    /// <returns>The same request, so a caller can validate and use it in one step.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the start index or the limit is negative. Both arrive from
    /// outside this plugin, and a negative one is a fault rather than a request
    /// for nothing.
    /// </exception>
    public SurfaceLevelRequest Validated()
    {
        if (StartIndex is { } start)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start, nameof(StartIndex));
        }

        if (Limit is { } limit)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(limit, nameof(Limit));
        }

        return this;
    }
}
