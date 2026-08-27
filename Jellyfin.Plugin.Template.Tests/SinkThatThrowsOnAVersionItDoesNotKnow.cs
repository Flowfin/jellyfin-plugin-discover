using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Seam;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A receiver that reads the contract version and throws on one it does not know.
/// </summary>
/// <remarks>
/// The rule says a receiver refuses such a message. This sink implements the rule
/// wrongly, on purpose, because a contract written for strangers is kept by some
/// of them and not by others, and the version at which that matters is the one
/// where a user presses something.
///
/// It throws the shape a receiver reaches for first, an argument exception naming
/// the field it could not read, rather than a type invented for this test.
/// </remarks>
internal sealed class SinkThatThrowsOnAVersionItDoesNotKnow : IWantReceiver
{
    private readonly CallLog _log;
    private readonly string _name;
    private readonly int _understands;

    /// <summary>
    /// Initializes a new instance of the <see cref="SinkThatThrowsOnAVersionItDoesNotKnow"/> class.
    /// </summary>
    /// <param name="log">Where the call is recorded.</param>
    /// <param name="name">How this sink spells itself in the log.</param>
    /// <param name="understands">The one contract version this receiver was built against.</param>
    public SinkThatThrowsOnAVersionItDoesNotKnow(CallLog log, string name, int understands)
    {
        _log = log;
        _name = name;
        _understands = understands;
    }

    /// <inheritdoc />
    public Task<bool> ReceiveAsync(Want want, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(want);

        _log.Record(string.Format(
            CultureInfo.InvariantCulture,
            "{0} was handed {1} at version {2}",
            _name,
            want.WantIdentifier,
            want.ContractVersion));

        if (want.ContractVersion != _understands)
        {
            throw new ArgumentOutOfRangeException(
                nameof(want),
                want.ContractVersion,
                "This receiver was built against another version of the contract.");
        }

        return Task.FromResult(true);
    }
}
