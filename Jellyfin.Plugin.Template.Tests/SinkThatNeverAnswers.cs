using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Seam;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A receiver that takes the want and never answers.
/// </summary>
/// <remarks>
/// The task it returns is completed by nothing, which is a hang with no wait in
/// it: `no-sleep-in-a-test` refuses a delay here, and a delay would be the wrong
/// shape anyway. A sibling stuck on a lock, on a socket or in a loop is
/// indistinguishable from this, and none of those finishes because a test waited
/// longer.
/// </remarks>
internal sealed class SinkThatNeverAnswers : IWantReceiver
{
    private readonly TaskCompletionSource<bool> _neverCompleted = new TaskCompletionSource<bool>();
    private readonly CallLog _log;
    private readonly string _name;

    /// <summary>
    /// Initializes a new instance of the <see cref="SinkThatNeverAnswers"/> class.
    /// </summary>
    /// <param name="log">Where the call is recorded.</param>
    /// <param name="name">How this sink spells itself in the log.</param>
    public SinkThatNeverAnswers(CallLog log, string name)
    {
        _log = log;
        _name = name;
    }

    /// <inheritdoc />
    public Task<bool> ReceiveAsync(Want want, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(want);

        _log.Record($"{_name} was handed {want.WantIdentifier}");

        return _neverCompleted.Task;
    }
}
