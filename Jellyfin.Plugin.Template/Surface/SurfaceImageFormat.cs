namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// What kind of picture the bytes of a surface image are.
/// </summary>
/// <remarks>
/// Carried rather than sniffed from the bytes. A format guessed from a header
/// is a guess that is wrong for exactly the file somebody added by hand, and
/// the client is where it goes wrong.
/// </remarks>
public enum SurfaceImageFormat
{
    /// <summary>
    /// No format. What an unset field reads as, and refused by
    /// <see cref="SurfaceImage"/> rather than carried.
    /// </summary>
    None = 0,

    /// <summary>
    /// PNG.
    /// </summary>
    Png = 1,

    /// <summary>
    /// JPEG.
    /// </summary>
    Jpeg = 2
}
