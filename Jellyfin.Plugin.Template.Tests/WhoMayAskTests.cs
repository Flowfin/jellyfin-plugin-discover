using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Configuration;
using Jellyfin.Plugin.Template.Seam;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Who this plugin passes a want on for, and who it refuses.
/// </summary>
/// <remarks>
/// #98. Both cases go through <see cref="WantHandover.OfferAsync"/> rather than
/// through the permission on its own, which is that issue's fourth condition and
/// is the difference between proving the rule and proving the type: a refusal
/// that a caller could reach the receivers around would satisfy an assertion on
/// <see cref="WhoMayAsk.Refuses"/> and fail the condition beside it.
///
/// What each refusal test asserts is the sink's log as well as the outcome. The
/// outcome alone is satisfied by a handover that offers the want, is refused by
/// every sink and reports the refusal as its own, which is the failure the
/// third condition names.
///
/// No server, no sibling and no configuration on disk. The list is handed over
/// as a document, which is the half of the server's own path that can be
/// asserted without a plugin instance, for the reason
/// <c>DiscoverRefreshTask.ShelvesFor</c> gives for the same shape.
/// </remarks>
public class WhoMayAskTests
{
    private static readonly string[] _theSinkWasHanded = { "sink was handed want-1" };

    private static readonly Guid _asker = new Guid("2f1d0f9a-6f66-4a51-9a3f-9f5a6e2c1b74");
    private static readonly Guid _somebodyElse = new Guid("6b2c4d80-1f39-4a0e-8f3a-2c4b1d7e9a05");

    /// <summary>
    /// A fresh install refuses nobody, and the want reaches the sink.
    /// </summary>
    /// <remarks>
    /// The permitted half of the fourth condition, and it is the default state
    /// rather than a configured one: the empty list is what a first install
    /// writes, and the answer to it is that whoever may browse may ask.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task AUserTheOperatorHasNotListedIsPassedOn()
    {
        var log = new CallLog();
        var handover = Handover(log, new PluginConfiguration());

        var outcome = await handover.OfferAsync(AWantFrom(_asker), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.Accepted, outcome);
        Assert.Equal(_theSinkWasHanded, log.Calls);
    }

    /// <summary>
    /// A listed user's want reaches no sink at all.
    /// </summary>
    /// <remarks>
    /// The refused half of the fourth condition and the whole of the third. The
    /// empty call log is the assertion that matters: it is what says the refusal
    /// happened before the seam rather than after it, and it is the one a
    /// refusal written anywhere below the receivers loop would fail.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task AListedUsersWantIsOfferedToNobody()
    {
        var log = new CallLog();
        var handover = Handover(log, Refusing(_asker));

        var outcome = await handover.OfferAsync(AWantFrom(_asker), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.RefusedHere, outcome);
        Assert.Empty(log.Calls);
    }

    /// <summary>
    /// Listing one user leaves the others alone.
    /// </summary>
    /// <remarks>
    /// The near miss. A refusal that reads the list as "somebody is listed"
    /// rather than as "this user is listed" passes both tests above and turns
    /// one operator's decision into a server where nobody may ask.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task ListingOneUserDoesNotRefuseAnother()
    {
        var log = new CallLog();
        var handover = Handover(log, Refusing(_asker));

        var outcome = await handover.OfferAsync(AWantFrom(_somebodyElse), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.Accepted, outcome);
        Assert.Equal(_theSinkWasHanded, log.Calls);
    }

    /// <summary>
    /// The operator is told which user was refused and why.
    /// </summary>
    /// <remarks>
    /// #98's second condition. A refusal an operator cannot see is the
    /// "silently dropped" half of it, and the line is asserted formatted rather
    /// than as a template because what the condition is about is what a person
    /// reading the log sees.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task ARefusalNamesTheWantTheUserAndTheReason()
    {
        var logger = new LoggerThatRecordsWhatIsWritten<WantHandover>();
        var handover = new WantHandover(
            new[] { new SinkThatRecordsWhatItWasHanded(new CallLog(), "sink", accepts: true) },
            logger,
            TimeSpan.Zero,
            () => WhoMayAsk.From(Refusing(_asker)));

        await handover.OfferAsync(AWantFrom(_asker), CancellationToken.None);

        var line = Assert.Single(logger.Lines);
        Assert.Contains("want-1", line, StringComparison.Ordinal);
        Assert.Contains(_asker.ToString(), line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lists that user", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// A list this build cannot read refuses every want, including one from a
    /// user it does not name.
    /// </summary>
    /// <remarks>
    /// The direction this fails in is the point. An entry that is not a user
    /// identifier is a refusal nothing can apply, and reading past it would
    /// honour wants the operator had refused, which is the "silently honoured"
    /// half of the second condition. The log line says which entry, because an
    /// operator's next action is to correct it.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task AnUnreadableListRefusesEverybody()
    {
        var configuration = new PluginConfiguration();
        configuration.UsersRefusedTheAsk.Add("the neighbour's account");

        var log = new CallLog();
        var logger = new LoggerThatRecordsWhatIsWritten<WantHandover>();
        var handover = new WantHandover(
            new[] { new SinkThatRecordsWhatItWasHanded(log, "sink", accepts: true) },
            logger,
            TimeSpan.Zero,
            () => WhoMayAsk.From(configuration));

        var outcome = await handover.OfferAsync(AWantFrom(_somebodyElse), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.RefusedHere, outcome);
        Assert.Empty(log.Calls);
        Assert.Contains("the neighbour's account", Assert.Single(logger.Lines), StringComparison.Ordinal);
    }

    /// <summary>
    /// With no configuration to read, no want is passed on.
    /// </summary>
    /// <remarks>
    /// The same rule reaching the other absence. A plugin with no configuration
    /// cannot say whom the operator refused, so it refuses rather than passing
    /// everything on, and the line says that is what happened rather than naming
    /// a user who is not in any list.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task WithNoConfigurationNoWantIsPassedOn()
    {
        var log = new CallLog();
        var logger = new LoggerThatRecordsWhatIsWritten<WantHandover>();
        var handover = new WantHandover(
            new[] { new SinkThatRecordsWhatItWasHanded(log, "sink", accepts: true) },
            logger,
            TimeSpan.Zero,
            () => WhoMayAsk.From(null));

        var outcome = await handover.OfferAsync(AWantFrom(_asker), CancellationToken.None);

        Assert.Equal(WantHandoverOutcome.RefusedHere, outcome);
        Assert.Empty(log.Calls);
        Assert.Contains("no configuration", Assert.Single(logger.Lines), StringComparison.Ordinal);
    }

    /// <summary>
    /// A refusal is reported as this plugin's own rather than as the absence of
    /// a sibling.
    /// </summary>
    /// <remarks>
    /// The two outcomes are both "nothing was handed over" and they answer
    /// different questions from an operator. A refusal reported as
    /// <see cref="WantHandoverOutcome.NoReceiver"/> would tell one that their
    /// own setting was a missing plugin, and it is reachable by putting the
    /// check one statement lower.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task WithNoSinkAtAllARefusalIsStillThisPluginsOwn()
    {
        var handover = new WantHandover(
            Array.Empty<IWantReceiver>(),
            new LoggerThatRecordsWhatIsWritten<WantHandover>(),
            TimeSpan.Zero,
            () => WhoMayAsk.From(Refusing(_asker)));

        Assert.Equal(
            WantHandoverOutcome.RefusedHere,
            await handover.OfferAsync(AWantFrom(_asker), CancellationToken.None));
    }

    /// <summary>
    /// The list is read at the moment a want is offered rather than when the
    /// handover was built.
    /// </summary>
    /// <remarks>
    /// The server builds this type once and runs for months, so a permission
    /// held rather than read would go on answering with whoever was listed when
    /// the container was built. An operator adding a name would then see it take
    /// effect at the next restart and nowhere in the log would say so.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task TheListIsReadPerWantRatherThanHeld()
    {
        var configuration = new PluginConfiguration();
        var log = new CallLog();
        var handover = new WantHandover(
            new[] { new SinkThatRecordsWhatItWasHanded(log, "sink", accepts: true) },
            new LoggerThatRecordsWhatIsWritten<WantHandover>(),
            TimeSpan.Zero,
            () => WhoMayAsk.From(configuration));

        Assert.Equal(
            WantHandoverOutcome.Accepted,
            await handover.OfferAsync(AWantFrom(_asker), CancellationToken.None));

        configuration.UsersRefusedTheAsk.Add(_asker.ToString());

        Assert.Equal(
            WantHandoverOutcome.RefusedHere,
            await handover.OfferAsync(AWantFrom(_asker), CancellationToken.None));
    }

    /// <summary>
    /// A save carrying an entry that is not a user identifier is refused.
    /// </summary>
    /// <remarks>
    /// The other end of the unreadable list. The refusal at the seam costs an
    /// operator every gesture on the server until they notice; this one costs
    /// them the save, which is the moment they are looking at the setting.
    /// </remarks>
    /// <param name="entry">An entry no build could read as a user.</param>
    [Theory]
    [InlineData("the neighbour's account")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void ASaveCarryingAnEntryThatIsNotAUserIsRefused(string entry)
    {
        var configuration = new PluginConfiguration();
        configuration.UsersRefusedTheAsk.Add(entry);

        Assert.Throws<ArgumentException>(
            () => WhoMayAsk.ThrowIfAnEntryIsUnreadable(configuration));
    }

    /// <summary>
    /// A save carrying identifiers is not refused.
    /// </summary>
    /// <remarks>
    /// The other direction, so the refusal above is shown to be about the entry
    /// rather than about the list being non-empty. Without it a check that threw
    /// on every list passes the theory beside it.
    /// </remarks>
    [Fact]
    public void ASaveCarryingIdentifiersIsNotRefused()
    {
        var configuration = Refusing(_asker, _somebodyElse);

        WhoMayAsk.ThrowIfAnEntryIsUnreadable(configuration);

        Assert.Equal(
            new[] { _asker, _somebodyElse }.OrderBy(user => user),
            WhoMayAsk.From(configuration).Refused.OrderBy(user => user));
    }

    /// <summary>
    /// The named empty answer refuses nobody and gives no reason to.
    /// </summary>
    /// <remarks>
    /// It is what the constructors that take no permission pass, so a change
    /// making it refuse would turn every existing handover into one that hands
    /// nothing over, silently.
    /// </remarks>
    [Fact]
    public void NobodyIsRefusedRefusesNobody()
    {
        Assert.False(WhoMayAsk.NobodyIsRefused.Refuses(_asker));
        Assert.Null(WhoMayAsk.NobodyIsRefused.RefusesEverybodyBecause);
        Assert.Empty(WhoMayAsk.NobodyIsRefused.Refused);
    }

    /// <summary>
    /// A configuration this list is read from, refusing the named users.
    /// </summary>
    /// <param name="users">Who may not ask.</param>
    /// <returns>The configuration.</returns>
    private static PluginConfiguration Refusing(params Guid[] users)
    {
        var configuration = new PluginConfiguration();

        foreach (var user in users)
        {
            configuration.UsersRefusedTheAsk.Add(user.ToString());
        }

        return configuration;
    }

    /// <summary>
    /// A handover over one sink that takes whatever it is handed.
    /// </summary>
    /// <param name="log">Where the sink writes down what it was handed.</param>
    /// <param name="configuration">The document the list is read from, once per want.</param>
    /// <returns>The handover.</returns>
    private static WantHandover Handover(CallLog log, PluginConfiguration configuration) =>
        new WantHandover(
            new[] { new SinkThatRecordsWhatItWasHanded(log, "sink", accepts: true) },
            new LoggerThatRecordsWhatIsWritten<WantHandover>(),
            TimeSpan.Zero,
            () => WhoMayAsk.From(configuration));

    /// <summary>
    /// A want from one user, spelled the same way every test here asserts it.
    /// </summary>
    /// <param name="user">Who made the gesture.</param>
    /// <returns>The want.</returns>
    private static Want AWantFrom(Guid user) => new Want
    {
        Identity = new DiscoverTitleIdentity(new[] { new ProviderIdentifier(MetadataSource.Tmdb, "329865") }),
        Kind = DiscoverTitleKind.Movie,
        Name = "Arrival",
        ReleaseYear = 2016,
        AskingUser = user,
        WantIdentifier = "want-1"
    };
}
