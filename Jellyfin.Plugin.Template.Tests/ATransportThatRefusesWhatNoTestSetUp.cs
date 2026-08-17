using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Sources;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The thing an adapter is given instead of a way out of the machine, refusing every address a test did not set up.
/// </summary>
/// <remarks>
/// A test that wants an answer says which address it is an answer to. An
/// address nobody named is a call the test did not intend, and the refusal
/// names it, so the failure reads as "this asked the source for X" rather than
/// as a null reference somewhere downstream.
///
/// The refusal is the point rather than a defensive default. The failure this
/// exists against is one accidental live call: it passes on the machine that
/// wrote it, it spends somebody else's rate budget, and it turns a red run into
/// a question about the internet. A transport that answered a question nobody
/// set up would hide exactly that, because the test would go green whether the
/// address was the intended one or not.
///
/// It names no type from <c>System.Net.Http</c>, and that is what makes it
/// writable here at all. `no-network-outside-source-adapter` refuses those
/// names in every tracked `*.cs` but an adapter's, so a double built by
/// overriding a message handler cannot live in this project. What it stands in
/// front of instead is the seam <see cref="SourceTransportReply"/> exists for:
/// an adapter takes a function from an address to a reply, wraps the real one
/// inside its own file, and a test supplies this.
///
/// What it does not do, stated here because the sentence a reader wants to take
/// away is stronger than the one that is true. It refuses the calls made
/// through the adapters a test hands it to. It is not a property of the suite:
/// nothing obliges a test to use it, and a test constructing an adapter over
/// the transport that really talks would reach a host with nothing here
/// noticing. That half of #46 is the gate running the suite with no route out,
/// and it is not this file.
/// </remarks>
internal sealed class ATransportThatRefusesWhatNoTestSetUp
{
    private readonly Dictionary<Uri, SourceTransportReply> _replies = new Dictionary<Uri, SourceTransportReply>();

    private readonly List<Uri> _asked = new List<Uri>();

    /// <summary>
    /// Gets every address this transport was asked for, in the order it was asked, refused ones included.
    /// </summary>
    /// <remarks>
    /// The refused ones are in it on purpose. A test asserting that a failure
    /// stopped rather than being retried is counting attempts, and an attempt
    /// that was refused is still an attempt the code chose to make.
    /// </remarks>
    public IReadOnlyList<Uri> Asked => _asked;

    /// <summary>
    /// Says what this transport answers one address with.
    /// </summary>
    /// <param name="address">The address a test expects to be asked for.</param>
    /// <param name="reply">What comes back from it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="address"/> is null.</exception>
    public void Answer(Uri address, SourceTransportReply reply)
    {
        ArgumentNullException.ThrowIfNull(address);

        _replies[address] = reply;
    }

    /// <summary>
    /// Carries one request, or refuses it because no test said what it answers with.
    /// </summary>
    /// <param name="address">Where the adapter wanted to ask.</param>
    /// <param name="cancellationToken">What stops it.</param>
    /// <returns>The reply a test set up for that address.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="address"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no test set up an answer for that address. The message
    /// carries the address that was attempted and the ones that were set up,
    /// because the two are what separate a call nobody meant from a call whose
    /// address came out wrong.
    /// </exception>
    public Task<SourceTransportReply> SendAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        cancellationToken.ThrowIfCancellationRequested();

        _asked.Add(address);

        if (_replies.TryGetValue(address, out var reply))
        {
            return Task.FromResult(reply);
        }

        throw new InvalidOperationException(Refusal(address));
    }

    /// <summary>
    /// What a refusal says.
    /// </summary>
    /// <param name="address">The address that was attempted.</param>
    /// <returns>The message the failure carries.</returns>
    private string Refusal(Uri address) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "No test set up an answer for {0}, so this would have left the machine. {1}",
            address.AbsoluteUri,
            _replies.Count == 0
                ? "Nothing was set up on this transport at all."
                : "What was set up: " + string.Join(", ", _replies.Keys.Select(set => set.AbsoluteUri).OrderBy(set => set, StringComparer.Ordinal)) + ".");
}
