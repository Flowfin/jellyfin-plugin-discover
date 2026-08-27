using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Seam;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A receiver that takes the want and writes down that it was handed one.
/// </summary>
/// <remarks>
/// Hand-written rather than generated, for the reason #49 settled: what these
/// tests assert is a sequence of calls, and a fake that only answers cannot
/// support that. It answers synchronously on purpose. A receiver whose answer
/// arrives on another thread would make "did every receiver get it" a question
/// about timing, and the two tests that are about timing supply their own shapes
/// below.
/// </remarks>
internal sealed class SinkThatRecordsWhatItWasHanded : IWantReceiver
{
    private readonly CallLog _log;
    private readonly string _name;
    private readonly bool _accepts;

    /// <summary>
    /// Initializes a new instance of the <see cref="SinkThatRecordsWhatItWasHanded"/> class.
    /// </summary>
    /// <param name="log">Where the call is recorded.</param>
    /// <param name="name">How this sink spells itself in the log, so three of them are told apart.</param>
    /// <param name="accepts">What it answers.</param>
    public SinkThatRecordsWhatItWasHanded(CallLog log, string name, bool accepts)
    {
        _log = log;
        _name = name;
        _accepts = accepts;
    }

    /// <inheritdoc />
    public Task<bool> ReceiveAsync(Want want, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(want);

        _log.Record($"{_name} was handed {want.WantIdentifier}");

        return Task.FromResult(_accepts);
    }
}
