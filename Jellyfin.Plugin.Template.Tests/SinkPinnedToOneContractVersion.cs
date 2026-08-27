using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Seam;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A receiver that reads the contract version first and refuses what it cannot read.
/// </summary>
/// <remarks>
/// The rule in
/// `docs/decisions/0004-what-crosses-the-seam-to-a-requests-plugin.md` written as
/// a sink, because that is the only side the rule is executed on: a receiver
/// reads the number first, a number it does not know is higher than any it knows
/// because the numbering only grows, and it refuses the message rather than
/// reading the fields it recognises.
///
/// One sink covers both directions this issue asks about. Pinned to the first
/// version it is a receiver built before this plugin moved; pinned to a later one
/// it is a receiver built against a contract this plugin has not reached. Two
/// classes would be one behaviour written twice, and the second copy is where the
/// two would come apart.
/// </remarks>
internal sealed class SinkPinnedToOneContractVersion : IWantReceiver
{
    private readonly CallLog _log;
    private readonly string _name;
    private readonly int _understands;

    /// <summary>
    /// Initializes a new instance of the <see cref="SinkPinnedToOneContractVersion"/> class.
    /// </summary>
    /// <param name="log">Where the call is recorded.</param>
    /// <param name="name">How this sink spells itself in the log.</param>
    /// <param name="understands">The one contract version this receiver was built against.</param>
    public SinkPinnedToOneContractVersion(CallLog log, string name, int understands)
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

        return Task.FromResult(want.ContractVersion == _understands);
    }
}
