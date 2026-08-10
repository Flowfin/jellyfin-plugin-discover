namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// Which picture of the surface itself a client is asking for.
/// </summary>
/// <remarks>
/// The surface's own artwork rather than a title's. A title's artwork stays at
/// the source and is never copied here, which is #62.
///
/// Three members out of the server's much longer list, and the three are the
/// ones a library tile is drawn from. Adding a member means having a picture to
/// put behind it.
/// </remarks>
public enum SurfaceImageKind
{
    /// <summary>
    /// No kind. What an unset field reads as, and answered with nothing.
    /// </summary>
    None = 0,

    /// <summary>
    /// The main picture, which is what most clients draw on a library tile.
    /// </summary>
    Primary = 1,

    /// <summary>
    /// The wide picture some clients prefer for a tile.
    /// </summary>
    Thumb = 2,

    /// <summary>
    /// The picture drawn behind the page.
    /// </summary>
    Backdrop = 3
}
