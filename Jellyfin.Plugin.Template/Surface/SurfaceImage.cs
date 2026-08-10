using System;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// One picture of the surface itself, as bytes and what they are.
/// </summary>
/// <remarks>
/// Bytes rather than a stream or a path. A stream makes every caller the owner
/// of something to dispose and makes a second read of the same image a question
/// about position; a path makes the picture a file on the machine the plugin
/// happens to be running on. These pictures are the plugin's own and are small
/// enough to hold, so the simplest of the three is also the one with the fewest
/// ways to be wrong.
/// </remarks>
public sealed class SurfaceImage
{
    private readonly SurfaceImageFormat _format;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurfaceImage"/> class.
    /// </summary>
    /// <param name="format">What kind of picture the bytes are.</param>
    /// <param name="bytes">The picture.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the format is <see cref="SurfaceImageFormat.None"/>, which
    /// is what an unset field reads as.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when there are no bytes. An image with nothing in it is what a
    /// surface with no image already says by returning null, and two spellings
    /// of the same absence is one a client draws as a broken picture.
    /// </exception>
    public SurfaceImage(SurfaceImageFormat format, ReadOnlyMemory<byte> bytes)
    {
        if (format is not (SurfaceImageFormat.Png or SurfaceImageFormat.Jpeg))
        {
            throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                "A surface image says what kind of picture it is. None is what an unset field reads as.");
        }

        if (bytes.IsEmpty)
        {
            throw new ArgumentException(
                "An image with no bytes is an absent image spelled a second way. A surface with no image of that kind returns null instead.",
                nameof(bytes));
        }

        _format = format;
        Bytes = bytes;
    }

    /// <summary>
    /// Gets what kind of picture the bytes are.
    /// </summary>
    public SurfaceImageFormat Format => _format;

    /// <summary>
    /// Gets the picture.
    /// </summary>
    public ReadOnlyMemory<byte> Bytes { get; }
}
