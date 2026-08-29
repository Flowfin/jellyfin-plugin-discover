using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Seam;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A receiver that takes the want and keeps the message itself.
/// </summary>
/// <remarks>
/// <see cref="SinkThatRecordsWhatItWasHanded"/> writes a line naming the want
/// identifier, which answers "was it handed over" and cannot answer "what did it
/// receive". The replay marker on #335 is a field a receiver reads rather than a
/// call it counts, so proving it crossed needs the message rather than a note
/// that one arrived.
///
/// Hand-written and synchronous for the reasons #49 settled, the same as every
/// other sink here.
/// </remarks>
internal sealed class SinkThatKeepsTheWantsItWasHanded : IWantReceiver
{
    private readonly List<Want> _received = new List<Want>();

    /// <summary>
    /// Gets what this sink was handed, in the order it was handed it.
    /// </summary>
    public IReadOnlyList<Want> Received => _received;

    /// <inheritdoc />
    public Task<bool> ReceiveAsync(Want want, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(want);

        _received.Add(want);

        return Task.FromResult(true);
    }
}
