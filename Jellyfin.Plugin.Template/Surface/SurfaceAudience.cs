namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// Who a surface is drawn for, as far as the server needs to be told.
/// </summary>
/// <remarks>
/// Two members and not the server's five. The three between them are bands of
/// one country's film rating system, and this plugin does not classify anything
/// into them: what it shows is whatever the shelves hold, which is a decision
/// about the shelves rather than about the surface. Filtering what a server
/// does not want shown is #93 and who sees the surface at all is #57, and
/// neither is expressible as one rating on the whole surface.
/// </remarks>
public enum SurfaceAudience
{
    /// <summary>
    /// No audience. What an unset field reads as, and refused by
    /// <see cref="SurfaceDescription"/> rather than carried.
    /// </summary>
    None = 0,

    /// <summary>
    /// Everybody the operator lets in.
    /// </summary>
    General = 1,

    /// <summary>
    /// Adults only.
    /// </summary>
    Adult = 2
}
