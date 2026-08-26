using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Seam;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What the seam does with no sink behind it, with one, and with three.
/// </summary>
/// <remarks>
/// No server and no sibling. The receivers are hand-written sinks in this
/// project, which is what the interface being this plugin's own buys: a test can
/// stand on the other side of the seam without a second plugin existing.
///
/// The bound is named by the test rather than waited out. `no-sleep-in-a-test`
/// refuses a wait, and a boundary reached by waiting cannot be reached exactly:
/// a bound of zero puts the case this type is about, a receiver that has not
/// answered, on the near side of the bound with no time passing at all.
/// </remarks>
public class WantHandoverTests
{
    private static readonly string[] _oneSinkWasHanded = { "sink was handed want-1" };

    private static readonly string[] _threeSinksWereHanded =
    {
        "first was handed want-1",
        "second was handed want-1",
        "third was handed want-1"
    };

    private static Want AWant(string identifier) => new Want
    {
        Identity = new DiscoverTitleIdentity(new[] { new ProviderIdentifier(MetadataSource.Tmdb, "329865") }),
        Kind = DiscoverTitleKind.Movie,
        Name = "Arrival",
        ReleaseYear = 2016,
        AskingUser = new Guid("2f1d0f9a-6f66-4a51-9a3f-9f5a6e2c1b74"),
        WantIdentifier = identifier
    };

    /// <summary>
    /// With nothing implementing the seam the plugin is complete rather than
    /// degraded.
    /// </summary>
    /// <remarks>
    /// This is the state almost every install is in and it is the one a null
    /// check gets accidentally right. Asserting the outcome rather than the
    /// absence of an exception is what separates the two: a handover that
    /// returned <see cref="WantHandoverOutcome.NotAccepted"/> here would also
    /// throw nothing, and it would tell an operator that a sibling refused a want
    /// on a server that has no sibling.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task WithNoReceiverTheWantIsCompleteRatherThanFailed()
    {
        var handover = new WantHandover(
            Array.Empty<IWantReceiver>(),
            new LoggerThatRecordsWhatIsWritten<WantHandover>());

        var outcome = await handover.OfferAsync(AWant("want-1"), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.NoReceiver, outcome);
        Assert.Empty(handover.Receivers);
    }

    /// <summary>
    /// One receiver is handed the want, and its answer is the outcome.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task OneReceiverIsHandedTheWantAndItsAnswerIsTheOutcome()
    {
        var log = new CallLog();

        var handover = new WantHandover(
            new IWantReceiver[] { new SinkThatRecordsWhatItWasHanded(log, "sink", accepts: true) },
            new LoggerThatRecordsWhatIsWritten<WantHandover>());

        var outcome = await handover.OfferAsync(AWant("want-1"), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.Accepted, outcome);
        Assert.Equal(_oneSinkWasHanded, log.Calls);
    }

    /// <summary>
    /// Three receivers are each handed the same want.
    /// </summary>
    /// <remarks>
    /// The definition of more than one implementation, asserted rather than
    /// described: every receiver gets the message, and none of them is chosen
    /// over another. The calls are compared as a set, because no order is
    /// promised and a test asserting one would be asserting a property this type
    /// deliberately does not have.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ThreeReceiversAreEachHandedTheSameWant()
    {
        var log = new CallLog();

        var handover = new WantHandover(
            new IWantReceiver[]
            {
                new SinkThatRecordsWhatItWasHanded(log, "first", accepts: false),
                new SinkThatRecordsWhatItWasHanded(log, "second", accepts: false),
                new SinkThatRecordsWhatItWasHanded(log, "third", accepts: false)
            },
            new LoggerThatRecordsWhatIsWritten<WantHandover>());

        var outcome = await handover.OfferAsync(AWant("want-1"), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.NotAccepted, outcome);
        Assert.Equal(new HashSet<string>(_threeSinksWereHanded), new HashSet<string>(log.Calls));
    }

    /// <summary>
    /// One receiver accepting is the want accepted, however many refused.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task OneAcceptanceAmongRefusalsIsAnAcceptance()
    {
        var log = new CallLog();

        var handover = new WantHandover(
            new IWantReceiver[]
            {
                new SinkThatRecordsWhatItWasHanded(log, "first", accepts: false),
                new SinkThatRecordsWhatItWasHanded(log, "second", accepts: true),
                new SinkThatRecordsWhatItWasHanded(log, "third", accepts: false)
            },
            new LoggerThatRecordsWhatIsWritten<WantHandover>());

        var outcome = await handover.OfferAsync(AWant("want-1"), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.Accepted, outcome);
        Assert.Equal(3, log.Calls.Count);
    }

    /// <summary>
    /// A receiver that refuses is reported, and refusing is not an error.
    /// </summary>
    /// <remarks>
    /// The contract says a receiver built against a version it does not know
    /// refuses rather than misreading, and that a refusal is not an error on this
    /// side. Both halves are asserted: nothing is thrown, and the line an
    /// operator would read names the receiver rather than only counting.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARefusalIsReportedAndIsNotAnError()
    {
        var logger = new LoggerThatRecordsWhatIsWritten<WantHandover>();

        var handover = new WantHandover(
            new IWantReceiver[] { new SinkThatRecordsWhatItWasHanded(new CallLog(), "sink", accepts: false) },
            logger);

        var outcome = await handover.OfferAsync(AWant("want-1"), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.NotAccepted, outcome);
        Assert.Contains(logger.Lines, line => line.Contains("want-1", StringComparison.Ordinal)
            && line.Contains(nameof(SinkThatRecordsWhatItWasHanded), StringComparison.Ordinal));
    }

    /// <summary>
    /// A receiver that throws does not reach the caller and does not stop the
    /// others.
    /// </summary>
    /// <remarks>
    /// Both halves matter and only one of them is obvious. That nothing is
    /// thrown at the caller is the condition as written; that the two healthy
    /// sinks were still handed the want is what stops the fix being a try/catch
    /// around the whole loop, which would swallow the failure and drop every
    /// receiver after the one that threw.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASinkThatThrowsDoesNotReachTheCallerOrTheOtherSinks()
    {
        var log = new CallLog();
        var logger = new LoggerThatRecordsWhatIsWritten<WantHandover>();

        var handover = new WantHandover(
            new IWantReceiver[]
            {
                new SinkThatThrowsWhenHanded(log, "broken"),
                new SinkThatRecordsWhatItWasHanded(log, "healthy", accepts: true),
                new SinkThatRecordsWhatItWasHanded(log, "also-healthy", accepts: false)
            },
            logger);

        var outcome = await handover.OfferAsync(AWant("want-1"), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.Accepted, outcome);
        Assert.Equal(3, log.Calls.Count);
        Assert.Contains(log.Calls, call => call.StartsWith("healthy", StringComparison.Ordinal));
        Assert.Contains(logger.Lines, line => line.Contains("threw", StringComparison.Ordinal));
    }

    /// <summary>
    /// A receiver that never answers does not stall the gesture.
    /// </summary>
    /// <remarks>
    /// The sink's task is completed by nothing, so this test finishes only
    /// because the handover stops waiting. With the bound removed the assertion
    /// below is never reached and the run hangs rather than reddening, which is
    /// the failure this condition is about wearing its own shape.
    ///
    /// The healthy sink is here for the same reason as in the test above: it
    /// proves the other receivers were served rather than skipped, and it makes
    /// the outcome a positive assertion rather than the absence of a hang.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASinkThatNeverAnswersDoesNotStallTheGesture()
    {
        var log = new CallLog();
        var logger = new LoggerThatRecordsWhatIsWritten<WantHandover>();

        var handover = new WantHandover(
            new IWantReceiver[]
            {
                new SinkThatNeverAnswers(log, "stuck"),
                new SinkThatRecordsWhatItWasHanded(log, "healthy", accepts: true)
            },
            logger,
            TimeSpan.Zero);

        var outcome = await handover.OfferAsync(AWant("want-1"), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.Accepted, outcome);
        Assert.Equal(2, log.Calls.Count);
        Assert.Contains(logger.Lines, line => line.Contains("had not answered", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every receiver being stuck is a want nobody took, not a want that hung.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task EveryReceiverStuckIsAWantNobodyTook()
    {
        var log = new CallLog();

        var handover = new WantHandover(
            new IWantReceiver[] { new SinkThatNeverAnswers(log, "stuck"), new SinkThatNeverAnswers(log, "also-stuck") },
            new LoggerThatRecordsWhatIsWritten<WantHandover>(),
            TimeSpan.Zero);

        var outcome = await handover.OfferAsync(AWant("want-1"), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.NotAccepted, outcome);
        Assert.Equal(2, log.Calls.Count);
    }

    /// <summary>
    /// A receiver answering with no task at all is reported rather than
    /// dereferenced.
    /// </summary>
    /// <remarks>
    /// Not reachable from a receiver written in C# with nullable references on,
    /// which is why it is easy to leave out and why leaving it out costs a
    /// <see cref="NullReferenceException"/> raised inside this plugin for
    /// somebody else's mistake.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASinkThatAnswersWithNoTaskIsReportedRatherThanDereferenced()
    {
        var logger = new LoggerThatRecordsWhatIsWritten<WantHandover>();

        var handover = new WantHandover(
            new IWantReceiver[] { new SinkThatAnswersWithNothing(new CallLog(), "empty") },
            logger);

        var outcome = await handover.OfferAsync(AWant("want-1"), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.NotAccepted, outcome);
        Assert.Contains(logger.Lines, line => line.Contains("no task at all", StringComparison.Ordinal));
    }

    /// <summary>
    /// A caller that cancelled gets an answer rather than an exception.
    /// </summary>
    /// <remarks>
    /// A gesture abandoned while a sibling is thinking about it is an ordinary
    /// thing on a server that is shutting down, and it is not a fault of either
    /// plugin.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ACancelledCallerGetsAnAnswerRatherThanAnException()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var handover = new WantHandover(
            new IWantReceiver[] { new SinkThatNeverAnswers(new CallLog(), "stuck") },
            new LoggerThatRecordsWhatIsWritten<WantHandover>());

        var outcome = await handover.OfferAsync(AWant("want-1"), cancelled.Token);

        Assert.Equal(WantHandoverOutcome.NotAccepted, outcome);
    }

    /// <summary>
    /// A handover with no want is this plugin's own fault and is thrown.
    /// </summary>
    /// <remarks>
    /// The asymmetry the type states: a fault on this side of the seam is a
    /// defect and is raised, and a fault on the other side is an ordinary state
    /// and is absorbed.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AHandoverWithNoWantIsRefused()
    {
        var handover = new WantHandover(
            Array.Empty<IWantReceiver>(),
            new LoggerThatRecordsWhatIsWritten<WantHandover>());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handover.OfferAsync(null!, CancellationToken.None));
    }

    /// <summary>
    /// A negative bound is refused rather than read as a shorter wait.
    /// </summary>
    [Fact]
    public void ANegativeBoundIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WantHandover(
                Array.Empty<IWantReceiver>(),
                new LoggerThatRecordsWhatIsWritten<WantHandover>(),
                TimeSpan.FromSeconds(-1)));
    }

    /// <summary>
    /// The bound a server runs with is stated and is not zero.
    /// </summary>
    /// <remarks>
    /// The tests above name their own bound, so nothing in them would notice a
    /// default that had become zero and made every handover on a real server
    /// give up before any receiver could answer.
    /// </remarks>
    [Fact]
    public void TheDefaultBoundIsAStatedNumberAndNotZero()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), WantHandover.DefaultBound);
    }

    /// <summary>
    /// Nothing in the plugin implements the seam.
    /// </summary>
    /// <remarks>
    /// The absence half of "complete with nothing behind it", held as a property
    /// rather than as a habit. An implementation added to this assembly would
    /// make the plugin its own sibling and would turn every test above into a
    /// test of a receiver this plugin ships.
    /// </remarks>
    [Fact]
    public void NothingInThePluginImplementsTheSeam()
    {
        var implementations = typeof(Plugin).Assembly
            .GetTypes()
            .Where(type => typeof(IWantReceiver).IsAssignableFrom(type) && !type.IsInterface)
            .ToArray();

        Assert.Empty(implementations);
    }
}
