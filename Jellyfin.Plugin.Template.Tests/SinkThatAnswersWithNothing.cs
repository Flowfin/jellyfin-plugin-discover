using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Seam;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A receiver that answers with no task at all.
/// </summary>
/// <remarks>
/// What a receiver compiled against a different shape of this interface, or
/// written in a language whose null is not the compiler's, hands back. It is not
/// reachable from C# without the suppression below, and it is exactly the value
/// that turns a seam into a <see cref="NullReferenceException"/> this plugin
/// gets blamed for.
/// </remarks>
internal sealed class SinkThatAnswersWithNothing : IWantReceiver
{
    private readonly CallLog _log;
    private readonly string _name;

    /// <summary>
    /// Initializes a new instance of the <see cref="SinkThatAnswersWithNothing"/> class.
    /// </summary>
    /// <param name="log">Where the call is recorded.</param>
    /// <param name="name">How this sink spells itself in the log.</param>
    public SinkThatAnswersWithNothing(CallLog log, string name)
    {
        _log = log;
        _name = name;
    }

    /// <inheritdoc />
    public Task<bool> ReceiveAsync(Want want, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(want);

        _log.Record($"{_name} was handed {want.WantIdentifier}");

        return null!;
    }
}
