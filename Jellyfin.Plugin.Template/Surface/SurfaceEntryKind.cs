namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// What one thing in a level is.
/// </summary>
/// <remarks>
/// Two members, because the shape a user browses is one folder per shelf with
/// titles inside it and nothing deeper, which is #54. A third member would be a
/// third level arriving without that decision being revisited.
/// </remarks>
public enum SurfaceEntryKind
{
    /// <summary>
    /// No kind. What an unset field reads as, and refused by
    /// <see cref="SurfaceEntry"/> rather than carried.
    /// </summary>
    None = 0,

    /// <summary>
    /// A shelf, which a user opens to find titles.
    /// </summary>
    Shelf = 1,

    /// <summary>
    /// A title, which is the thing the server does not have.
    /// </summary>
    Title = 2
}
