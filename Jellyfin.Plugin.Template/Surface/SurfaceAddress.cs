using System;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// Where in the surface a level or an entry sits, in this plugin's own spelling.
/// </summary>
/// <remarks>
/// The server asks for one level at a time and tells the plugin which one by
/// handing back a value the plugin gave it earlier, so this is the only thing
/// that survives a round trip through the server and through a client. It is a
/// type rather than a bare string because the absent value means the top level
/// rather than a missing one, and a string that means something when it is null
/// is a string every caller has to remember a rule about.
///
/// Nothing here says how an address is spelled. What a shelf's address is made
/// of is #54, and what a title's is made of is #60, which has to survive a
/// refresh because the server hashes an item's identity out of it.
/// </remarks>
public readonly record struct SurfaceAddress
{
    private readonly string? _value;

    private SurfaceAddress(string value) => _value = value;

    /// <summary>
    /// Gets the top level, which is what the server asks for when it names no parent.
    /// </summary>
    /// <remarks>
    /// This is <c>default</c>, so an address nobody assigned is the root rather
    /// than an address that throws when it is read. That is deliberate: the
    /// server really does ask for the root by supplying nothing.
    /// </remarks>
    public static SurfaceAddress Root => default;

    /// <summary>
    /// Gets a value indicating whether this is the top level.
    /// </summary>
    public bool IsRoot => _value is null;

    /// <summary>
    /// Gets the value that identifies this level, for an address that is not the root.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this is the root. The root has no value, and returning an
    /// empty string for it would let a caller pass the root somewhere only a
    /// named level belongs and find out later.
    /// </exception>
    public string Value => _value ?? throw new InvalidOperationException(
        $"The root level has no address value. Ask {nameof(IsRoot)} before reading this.");

    /// <summary>
    /// Makes an address out of a value.
    /// </summary>
    /// <param name="value">The value that identifies the level.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is null, empty or whitespace. A blank address is
    /// what the root already is, and two spellings of the root is how a request
    /// for the top level arrives looking like a request for a shelf.
    /// </exception>
    public static SurfaceAddress Of(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"A blank address is indistinguishable from the root. Use {nameof(SurfaceAddress)}.{nameof(Root)} for the top level.",
                nameof(value));
        }

        return new SurfaceAddress(value);
    }

    /// <inheritdoc />
    public override string ToString() => _value ?? "(root)";
}
