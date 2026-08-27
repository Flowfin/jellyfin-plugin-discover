using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Template.Seam;

/// <summary>
/// What a requests plugin implements to be handed a want.
/// </summary>
/// <remarks>
/// The extension point, decided in #95. An interface this plugin declares and a
/// sibling implements, resolved from the server's container, which is the
/// mechanism #18 already established as the way anything reaches this plugin.
///
/// The alternative, calling a sibling's HTTP API, is rejected and the reason
/// belongs here rather than in a note nobody opens: it needs a URL, a
/// credential and a retry policy for something running in the same process, and
/// it makes this plugin depend on the other one being reachable rather than
/// merely present.
///
/// Nothing in this repository references a sibling assembly and nothing needs
/// to. The interface is this plugin's own and lives in this tree, so a receiver
/// compiles against this assembly and this assembly compiles against nothing of
/// the receiver's. `no-sibling-plugin-reference` refuses the other direction as
/// tracked text and `AssemblyReferencesTests` refuses it in what is built.
///
/// Zero implementations is the ordinary state rather than a degraded one. With
/// nothing behind the seam the plugin is complete: <see cref="WantHandover"/>
/// answers <see cref="WantHandoverOutcome.NoReceiver"/> and the want is the
/// local list's, which is #97.
/// </remarks>
public interface IWantReceiver
{
    /// <summary>
    /// Takes one want.
    /// </summary>
    /// <param name="want">What was wanted, and by whom.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>
    /// <see langword="true"/> where the receiver accepted the want,
    /// <see langword="false"/> where it did not.
    /// </returns>
    /// <remarks>
    /// The handover is one way and at one moment. What comes back is the
    /// optional acknowledgement in the contract note and nothing else: this
    /// plugin learns whether the message was taken and never what happened to it
    /// afterwards. Anything a receiver would want to say back is a second
    /// contract and a different interface rather than a wider return value here.
    ///
    /// Answering <see langword="false"/> is a normal path rather than an error.
    /// A receiver built against a contract version it does not know refuses the
    /// message rather than reading the fields it recognises, and a receiver that
    /// refuses is behind rather than broken. Retrying the same message produces
    /// the same refusal, so it is not retried for that reason.
    ///
    /// Throwing and never answering are also normal paths as far as the caller
    /// is concerned. <see cref="WantHandover"/> is where both are absorbed, and
    /// the bound it waits within is stated there. A receiver is not obliged to
    /// be quick, only to accept that this plugin stops waiting.
    /// </remarks>
    Task<bool> ReceiveAsync(Want want, CancellationToken cancellationToken);
}
