using System;
using System.IO;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A payload that reaches a writer a few bytes at a time, and that can stop
/// part way through.
/// </summary>
/// <remarks>
/// Two tests need this and they need opposite things from it. One interrupts a
/// write at the byte level, which is what the store's atomicity is about: a
/// stream that hands over some of its bytes and then fails is the disk filling
/// or the source dying mid-refresh, and asserting that the previous document
/// survives it is a different assertion from asserting that the code calls a
/// move.
///
/// The other needs two writes to overlap. A payload that arrives whole in one
/// read gives the scheduler nothing to interleave, so a race that exists would
/// not show; handing it over in small pieces is what makes the unguarded case
/// actually produce a mixture rather than only being able to.
///
/// The piece size and the point of failure are given by the test rather than
/// drawn, so a run says the same thing twice.
/// </remarks>
internal sealed class ContentThatArrivesInPieces : Stream
{
    private readonly byte[] _content;
    private readonly int _pieceSize;
    private readonly int _bytesBeforeItStops;
    private int _handedOver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentThatArrivesInPieces"/>
    /// class.
    /// </summary>
    /// <param name="content">The bytes to hand over.</param>
    /// <param name="pieceSize">How many bytes one read hands over at most.</param>
    /// <param name="bytesBeforeItStops">
    /// How many bytes are handed over before the stream fails, or a negative
    /// number for a stream that hands over all of them.
    /// </param>
    public ContentThatArrivesInPieces(byte[] content, int pieceSize, int bytesBeforeItStops)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfLessThan(pieceSize, 1);

        _content = content;
        _pieceSize = pieceSize;
        _bytesBeforeItStops = bytesBeforeItStops;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _content.Length;

    /// <summary>
    /// Gets or sets how many bytes have been handed over so far. Setting it is
    /// refused, because a stream this is standing in for is read once and
    /// forwards.
    /// </summary>
    public override long Position
    {
        get => _handedOver;
        set => throw new NotSupportedException("This stream is read once, forwards.");
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (_bytesBeforeItStops >= 0 && _handedOver >= _bytesBeforeItStops)
        {
            throw new IOException("The write was interrupted after " + _handedOver.ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes.");
        }

        var remaining = _content.Length - _handedOver;
        if (remaining == 0)
        {
            return 0;
        }

        var handingOver = Math.Min(Math.Min(count, _pieceSize), remaining);
        if (_bytesBeforeItStops >= 0)
        {
            handingOver = Math.Min(handingOver, _bytesBeforeItStops - _handedOver);
        }

        Array.Copy(_content, _handedOver, buffer, offset, handingOver);
        _handedOver += handingOver;
        return handingOver;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException("This stream is read once, forwards.");

    /// <inheritdoc/>
    public override void SetLength(long value)
        => throw new NotSupportedException("This stream is read only.");

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("This stream is read only.");
}
