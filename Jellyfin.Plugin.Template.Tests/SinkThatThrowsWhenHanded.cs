using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Seam;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A receiver that throws instead of answering.
/// </summary>
/// <remarks>
/// It records the call before it throws, so a test can tell "it was never
/// offered the want" from "it was offered it and blew up", which are the two
/// ways the assertion below could pass for the wrong reason.
/// </remarks>
internal sealed class SinkThatThrowsWhenHanded : IWantReceiver
{
    private readonly CallLog _log;
    private readonly string _name;

    /// <summary>
    /// Initializes a new instance of the <see cref="SinkThatThrowsWhenHanded"/> class.
    /// </summary>
    /// <param name="log">Where the call is recorded.</param>
    /// <param name="name">How this sink spells itself in the log.</param>
    public SinkThatThrowsWhenHanded(CallLog log, string name)
    {
        _log = log;
        _name = name;
    }

    /// <inheritdoc />
    public Task<bool> ReceiveAsync(Want want, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(want);

        _log.Record($"{_name} was handed {want.WantIdentifier}");

        throw new InvalidOperationException("A sibling that does not work on this server.");
    }
}
