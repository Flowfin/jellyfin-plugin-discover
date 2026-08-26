using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Template.Seam;

/// <summary>
/// The one place a want is offered to whatever implements the seam.
/// </summary>
/// <remarks>
/// The receivers come from the server's container. Nothing here searches for
/// them, loads an assembly by name or reads a configured type: a sibling plugin
/// registers an <see cref="IWantReceiver"/> in its own registrator, the server
/// builds one container out of every plugin's registrations, and this type is
/// handed whatever that container holds. Zero of them is the state most servers
/// are in and is supported by construction rather than by a null check that
/// happens to work: an empty sequence is what the container answers with, and
/// the answer is <see cref="WantHandoverOutcome.NoReceiver"/>.
///
/// MORE THAN ONE IS DEFINED BEHAVIOUR AND THIS IS THE DEFINITION. Every receiver
/// is offered the same want, all of them concurrently, and none of them learns
/// about another. There is no order and none is promised, no receiver's answer
/// changes what another is offered, and the want is accepted where at least one
/// of them acknowledged it. That is the only definition that survives two
/// siblings written by two people who have never read each other's code: any
/// rule picking a winner would make one of them silently dead on a server that
/// installed both.
///
/// WHAT A FAILING RECEIVER COSTS THE PERSON WHO ASKED IS BOUNDED, and that is
/// this type's whole job on the caller's side. A receiver that throws is caught
/// where it is called, a receiver that never answers is waited for no longer
/// than <see cref="DefaultBound"/>, and both leave the other receivers and the
/// caller untouched. Neither is an error on this side: the contract is one way
/// with no delivery guarantee, and a want that reached nobody is the local
/// list's, which is #97.
/// </remarks>
public sealed class WantHandover
{
    private readonly IWantReceiver[] _receivers;
    private readonly ILogger<WantHandover> _logger;
    private readonly TimeSpan _bound;

    /// <summary>
    /// Initializes a new instance of the <see cref="WantHandover"/> class.
    /// </summary>
    /// <param name="receivers">Whatever the server's container holds, which is usually nothing.</param>
    /// <param name="logger">Where a receiver that refused, threw or did not answer is reported.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    public WantHandover(IEnumerable<IWantReceiver> receivers, ILogger<WantHandover> logger)
        : this(receivers, logger, DefaultBound)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WantHandover"/> class with a chosen bound.
    /// </summary>
    /// <param name="receivers">Whatever the server's container holds, which is usually nothing.</param>
    /// <param name="logger">Where a receiver that refused, threw or did not answer is reported.</param>
    /// <param name="bound">How long the caller waits for the receivers before giving up on them.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="receivers"/> or <paramref name="logger"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bound"/> is negative.</exception>
    /// <remarks>
    /// The second constructor exists so a test can name the bound rather than
    /// wait for it. A test that reached the boundary by waiting is a test that
    /// measures the machine it ran on, which `no-sleep-in-a-test` refuses, and a
    /// bound of <see cref="TimeSpan.Zero"/> makes the case this type is about -
    /// a receiver that never answers - reachable in no time at all.
    /// </remarks>
    public WantHandover(IEnumerable<IWantReceiver> receivers, ILogger<WantHandover> logger, TimeSpan bound)
    {
        ArgumentNullException.ThrowIfNull(receivers);
        ArgumentNullException.ThrowIfNull(logger);

        if (bound < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bound),
                bound,
                "A negative bound is not a shorter wait, it is one no receiver could ever meet.");
        }

        _receivers = receivers.Where(receiver => receiver is not null).ToArray();
        _logger = logger;
        _bound = bound;
    }

    /// <summary>
    /// Gets how long a want handed to a receiver is waited for.
    /// </summary>
    /// <remarks>
    /// Five seconds, and the number is chosen against what it protects rather
    /// than against what a receiver needs. What is on the other end of this wait
    /// is a person who made a gesture, so the bound is the longest a gesture may
    /// appear to hang; a receiver that has not answered in five seconds inside
    /// the same process is not going to make it feel immediate however much
    /// longer it is given.
    ///
    /// It bounds the WAIT and not the receiver's work. A receiver still running
    /// when it passes is left running: this plugin stops watching, it does not
    /// stop the receiver, and the receiver's own token is the caller's rather
    /// than a shorter one. Nothing here could enforce a bound on a receiver that
    /// ignores its token, so claiming one would be a promise made out of a
    /// number.
    /// </remarks>
    public static TimeSpan DefaultBound => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets what the container gave this handover to offer wants to.
    /// </summary>
    /// <remarks>
    /// Read by the suite so that "with no sink, with one, and with three" is a
    /// property of one arrangement rather than three tests that each assume
    /// their own.
    /// </remarks>
    public IReadOnlyList<IWantReceiver> Receivers => _receivers;

    /// <summary>
    /// Offers one want to every receiver the container holds.
    /// </summary>
    /// <param name="want">What was wanted, and by whom.</param>
    /// <param name="cancellationToken">Stops waiting for the receivers.</param>
    /// <returns>What became of the want.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="want"/> is null.</exception>
    /// <remarks>
    /// Throws for a want it was handed nothing for, and for nothing a receiver
    /// did. That asymmetry is the point: a fault on this side of the seam is
    /// this plugin's own and is a defect, and a fault on the other side is an
    /// ordinary state of a server with somebody else's plugin on it.
    /// </remarks>
    public async Task<WantHandoverOutcome> OfferAsync(Want want, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(want);

        if (_receivers.Length == 0)
        {
            return WantHandoverOutcome.NoReceiver;
        }

        var offers = new Task<bool>[_receivers.Length];

        for (var index = 0; index < _receivers.Length; index++)
        {
            offers[index] = OfferToOne(_receivers[index], want, cancellationToken);
        }

        // Waited for as one set rather than one after another, so the bound is
        // what the person who asked waits in total. Offering them in turn would
        // multiply the wait by however many siblings a server happens to carry,
        // which is a number this plugin does not control.
        var everyone = Task.WhenAll(offers);

        // The timer is linked to the caller's token and cancelled again as soon
        // as the receivers are all in, so a handover that answered in a
        // millisecond does not leave a five-second timer behind it on a server
        // that is handing over all evening.
        using var boundSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await Task.WhenAny(everyone, Task.Delay(_bound, boundSource.Token)).ConfigureAwait(false);

        await boundSource.CancelAsync().ConfigureAwait(false);

        var accepted = false;

        for (var index = 0; index < offers.Length; index++)
        {
            var offer = offers[index];

            if (!offer.IsCompleted)
            {
                // Left running on purpose. Nothing here observes it again, and
                // OfferToOne cannot fault, so an answer arriving after the bound
                // is dropped rather than becoming an unobserved exception.
                _logger.LogWarning(
                    "The want {WantIdentifier} was handed to {Receiver} and it had not answered within {Bound}. Nothing is retried and the handover is not held open for it.",
                    want.WantIdentifier,
                    _receivers[index].GetType().FullName,
                    _bound);

                continue;
            }

            // Already completed, checked one line above, so this awaits nothing.
            // Written as an await rather than as a read of the result because
            // OfferToOne is what makes that safe, and an await says so where a
            // property read would only look like an oversight.
            accepted |= await offer.ConfigureAwait(false);
        }

        return accepted ? WantHandoverOutcome.Accepted : WantHandoverOutcome.NotAccepted;
    }

    /// <summary>
    /// Offers the want to one receiver, and answers for it whatever it does.
    /// </summary>
    /// <remarks>
    /// The returned task never faults and never cancels. Everything a receiver
    /// can do to a caller is turned into a false here, which is what makes the
    /// loop above able to abandon one of these without leaving an exception
    /// nobody observed.
    /// </remarks>
    private async Task<bool> OfferToOne(IWantReceiver receiver, Want want, CancellationToken cancellationToken)
    {
        try
        {
            var answer = receiver.ReceiveAsync(want, cancellationToken);

            if (answer is null)
            {
                // An interface returning a null task is a receiver written
                // against a shape this one does not have. It is reported like a
                // throw rather than awaited, because awaiting it is the
                // NullReferenceException this plugin would then be blamed for.
                _logger.LogWarning(
                    "The want {WantIdentifier} was handed to {Receiver} and it answered with no task at all. It is treated as not accepted.",
                    want.WantIdentifier,
                    receiver.GetType().FullName);

                return false;
            }

            var accepted = await answer.ConfigureAwait(false);

            if (!accepted)
            {
                _logger.LogInformation(
                    "The want {WantIdentifier} was handed to {Receiver} and it did not take it. A refusal is not an error here: a receiver built against a contract version it does not know refuses rather than misreading, and the same message would be refused again.",
                    want.WantIdentifier,
                    receiver.GetType().FullName);
            }

            return accepted;
        }
        catch (OperationCanceledException)
        {
            // The caller's token, reaching the receiver. Not this receiver's
            // fault and not worth a warning on a shutdown.
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception failure)
        {
            // Deliberately everything. What is on the other side of this call is
            // somebody else's plugin, and the whole promise of this seam is that
            // it cannot break the gesture. A narrower catch here is a list of the
            // exceptions siblings have thrown so far.
            _logger.LogWarning(
                failure,
                "The want {WantIdentifier} was handed to {Receiver} and it threw. The want is not retried and the other receivers are unaffected.",
                want.WantIdentifier,
                receiver.GetType().FullName);

            return false;
        }
#pragma warning restore CA1031
    }
}
