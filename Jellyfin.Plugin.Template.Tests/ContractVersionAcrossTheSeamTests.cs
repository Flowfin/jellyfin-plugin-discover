using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Seam;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What this plugin does when a receiver was built against another version of the
/// contract.
/// </summary>
/// <remarks>
/// The rule is in
/// `docs/decisions/0004-what-crosses-the-seam-to-a-requests-plugin.md` under #101
/// and it is executed on the receiver's side, so what these tests hold is this
/// side's half of it: that a receiver keeping the rule and a receiver breaking it
/// produce the same thing here, which is an answer rather than an exception at the
/// moment somebody pressed something.
///
/// A message at a version this build does not write is constructed rather than
/// waited for. There is one version today, so the only way to stand on the far
/// side of a version change is to write the number the plugin will write after
/// one, and `Want` takes it because a caller supplying it is the case a receiver
/// meets.
/// </remarks>
public class ContractVersionAcrossTheSeamTests
{
    private static Want AWant(int contractVersion) => new Want
    {
        Identity = new DiscoverTitleIdentity(new[] { new ProviderIdentifier(MetadataSource.Tmdb, "329865") }),
        Kind = DiscoverTitleKind.Movie,
        Name = "Arrival",
        ReleaseYear = 2016,
        AskingUser = new Guid("2f1d0f9a-6f66-4a51-9a3f-9f5a6e2c1b74"),
        WantIdentifier = "tmdb:329865:2f1d0f9a",
        ContractVersion = contractVersion
    };

    /// <summary>
    /// A receiver pinned to the first version takes what this build writes.
    /// </summary>
    /// <remarks>
    /// The half of the tolerance that is easy to leave untested because it is the
    /// case that works. It is here so the two tests below are read as a rule
    /// rather than as a receiver that refuses everything.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AReceiverPinnedToTheFirstVersionTakesWhatThisBuildWrites()
    {
        var log = new CallLog();

        var handover = new WantHandover(
            new IWantReceiver[] { new SinkPinnedToOneContractVersion(log, "pinned-to-1", understands: 1) },
            new LoggerThatRecordsWhatIsWritten<WantHandover>());

        var outcome = await handover.OfferAsync(AWant(WantContract.CurrentVersion), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.Accepted, outcome);
        Assert.Contains(log.Calls, call => call.EndsWith("at version 1", StringComparison.Ordinal));
    }

    /// <summary>
    /// A receiver built before the contract moved refuses, and refusing is an
    /// answer rather than a failure.
    /// </summary>
    /// <remarks>
    /// This is the condition in its own words: an older receiver, a message it
    /// cannot read, and this plugin carrying on. What it must not do is throw at
    /// the caller, retry, or treat the refusal as a fault of its own; a receiver
    /// that refuses is behind rather than broken, and the same message would be
    /// refused again.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AReceiverBuiltBeforeTheContractMovedRefusesAndIsNotAnError()
    {
        var log = new CallLog();
        var logger = new LoggerThatRecordsWhatIsWritten<WantHandover>();

        var handover = new WantHandover(
            new IWantReceiver[] { new SinkPinnedToOneContractVersion(log, "pinned-to-1", understands: 1) },
            logger);

        var outcome = await handover.OfferAsync(AWant(WantContract.CurrentVersion + 1), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.NotAccepted, outcome);
        Assert.Single(log.Calls);
        Assert.Contains(logger.Lines, line => line.Contains("did not take it", StringComparison.Ordinal));
    }

    /// <summary>
    /// A receiver built against a later contract refuses, and nothing here adapts
    /// to it.
    /// </summary>
    /// <remarks>
    /// The stated rule rather than an exception. This plugin writes the version it
    /// was built for and reads nothing back except the acknowledgement, so a
    /// receiver ahead of it is met by exactly the behaviour a receiver behind it
    /// is met by. A reader looking for a negotiation should stop here: there is
    /// one message, one direction and one answer.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AReceiverBuiltAgainstALaterContractRefusesAndNothingHereAdaptsToIt()
    {
        var log = new CallLog();

        var handover = new WantHandover(
            new IWantReceiver[] { new SinkPinnedToOneContractVersion(log, "pinned-to-2", understands: WantContract.CurrentVersion + 1) },
            new LoggerThatRecordsWhatIsWritten<WantHandover>());

        var outcome = await handover.OfferAsync(AWant(WantContract.CurrentVersion), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.NotAccepted, outcome);
        Assert.Single(log.Calls);
    }

    /// <summary>
    /// A receiver that throws on a version it does not know still reaches nobody.
    /// </summary>
    /// <remarks>
    /// The rule says refuse and this receiver throws instead, which is what a
    /// stranger implementing a contract does some of the time. The condition this
    /// is about asks that the case be handled by a stated rule rather than by an
    /// exception at the moment a user presses a button, and the receiver keeping
    /// the rule is not something this plugin can arrange. What it can arrange is
    /// that both spellings end in the same answer here.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AReceiverThatThrowsOnAVersionItDoesNotKnowEndsTheSameWayAsOneThatRefuses()
    {
        var log = new CallLog();
        var logger = new LoggerThatRecordsWhatIsWritten<WantHandover>();

        var handover = new WantHandover(
            new IWantReceiver[] { new SinkThatThrowsOnAVersionItDoesNotKnow(log, "strict", understands: WantContract.CurrentVersion + 1) },
            logger);

        var outcome = await handover.OfferAsync(AWant(WantContract.CurrentVersion), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.NotAccepted, outcome);
        Assert.Single(log.Calls);
        Assert.Contains(logger.Lines, line => line.Contains("threw", StringComparison.Ordinal));
    }

    /// <summary>
    /// One receiver refusing on its version does not cost the receiver that could
    /// read the message.
    /// </summary>
    /// <remarks>
    /// The case a server with two siblings on it is actually in, and the reason
    /// tolerance is a property of the handover rather than of either receiver. A
    /// version disagreement is not a reason to stop offering.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AVersionDisagreementWithOneReceiverDoesNotCostAnother()
    {
        var log = new CallLog();

        var handover = new WantHandover(
            new IWantReceiver[]
            {
                new SinkPinnedToOneContractVersion(log, "ahead", understands: WantContract.CurrentVersion + 1),
                new SinkThatThrowsOnAVersionItDoesNotKnow(log, "strict-and-ahead", understands: WantContract.CurrentVersion + 1),
                new SinkPinnedToOneContractVersion(log, "current", understands: WantContract.CurrentVersion)
            },
            new LoggerThatRecordsWhatIsWritten<WantHandover>());

        var outcome = await handover.OfferAsync(AWant(WantContract.CurrentVersion), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.Accepted, outcome);
        Assert.Equal(3, log.Calls.Count);
    }

    /// <summary>
    /// Nothing on the seam offers a receiver a way to state its version back.
    /// </summary>
    /// <remarks>
    /// The absence the stated rule rests on, held by something rather than by the
    /// note alone. The moment the interface grows a member a receiver answers with
    /// a number, this plugin has a version to read and the rule stops being "there
    /// is no negotiation" without anybody editing the sentence that says so.
    ///
    /// What is counted is the members of the interface and what the one of them
    /// answers with, so a second member of any shape fails this rather than only
    /// the shape somebody thought of.
    /// </remarks>
    [Fact]
    public void NothingOnTheSeamLetsAReceiverStateItsVersionBack()
    {
        var members = typeof(IWantReceiver).GetMembers(BindingFlags.Public | BindingFlags.Instance);

        var handover = Assert.Single(members.OfType<MethodInfo>());

        Assert.Equal(nameof(IWantReceiver.ReceiveAsync), handover.Name);
        Assert.Equal(typeof(Task<bool>), handover.ReturnType);
        Assert.Empty(members.OfType<PropertyInfo>());
    }
}
