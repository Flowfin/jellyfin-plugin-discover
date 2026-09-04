using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;
using Jellyfin.Plugin.Template.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Every outbound call this plugin makes goes through one handler a test replaces.
/// </summary>
/// <remarks>
/// <para>
/// #45's second condition, on the reading taken on 2026-09-04: one injection
/// POINT rather than one handler instance. The point is a single named client
/// this plugin registers, and the one thing a test replaces is that client's
/// primary handler.
/// </para>
/// <para>
/// What every other test of the adapter substitutes is the transport function
/// one layer inside the client, which was the only seam available while
/// `no-network-outside-source-adapter` refused a handler double anywhere in this
/// project. Those tests are unchanged and this does not replace them: they judge
/// what the adapter makes of a reply, and this judges that the reply came
/// through the client the registration configured. The two answer different
/// questions and the second is the one the condition asks.
/// </para>
/// <para>
/// Nothing here opens a socket, needs a real endpoint, touches a trust store,
/// reads an environment variable or turns a verification switch off. The
/// substitution is a registration in a container built in this process, which is
/// the whole of what the condition asks for in place of the shortcut #45
/// refuses.
/// </para>
/// </remarks>
public class OneInjectedHandlerInFrontOfEveryCallTests
{
    /// <summary>
    /// The instant the clock these adapters are given reads.
    /// </summary>
    private static readonly DateTimeOffset _fetched = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A call the plugin makes reaches the handler a test put in front of it, and one nobody set up does not leave the machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both directions in one arrangement, because the address is the thing they
    /// share: the refusal is what names the address the adapter built, and the
    /// answer is set up at that same address rather than at one written out
    /// here. A test that pasted the address would go on passing if the adapter
    /// started asking somewhere else.
    /// </para>
    /// <para>
    /// The second handler is the drift guard and it is the reason this test can
    /// be trusted. The plugin's client is named, and a factory answers a name
    /// nobody configured with a DEFAULT client rather than refusing, so an
    /// adapter that lost its name would quietly call out through the runtime's
    /// own handler and reach a source. This registers a second substitute on the
    /// default client with nothing set up on it at all, so that spelling ends in
    /// a refusal here instead of a request leaving the runner.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task ACallGoesThroughTheHandlerARegistrationPutInFrontAndOneNobodySetUpIsRefused()
    {
        var inFront = new AHandlerThatRefusesWhatNoTestSetUp();
        var anywhereElse = new AHandlerThatRefusesWhatNoTestSetUp();

        using var provider = AContainerHolding(inFront, anywhereElse);

        var adapter = AHandlerThatRefusesWhatNoTestSetUp.AnAdapterOver(
            provider,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);

        var query = new SourceQuery("trending", DiscoverTitleKind.Movie, null, null);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.FetchAsync(query, CancellationToken.None)).ConfigureAwait(true);

        var asked = Assert.Single(inFront.Asked);

        Assert.Contains(asked.AbsoluteUri, refused.Message, StringComparison.Ordinal);
        Assert.Empty(anywhereElse.Asked);

        inFront.Answer(asked, HttpStatusCode.OK, TmdbFixtures.Body(TmdbFixtures.MoviePage));

        var answer = await adapter.FetchAsync(query, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.Equal(2, answer.Titles.Count);
        Assert.Equal("A Film That Does Not Exist", answer.Titles[0].Name);

        Assert.Equal(2, inFront.Asked.Count);
        Assert.Empty(anywhereElse.Asked);
    }

    /// <summary>
    /// The request that reaches the handler is the one the plugin composed, credential and identity included.
    /// </summary>
    /// <remarks>
    /// The half a transport function cannot show. What that seam carries is an
    /// address; what a handler is handed is the request the client built, so the
    /// headers this plugin puts on a call are readable at the place a real
    /// endpoint would read them. The terms this adapter was written against
    /// require the application to identify itself, which is what the user agent
    /// is for, and the credential travelling in a header rather than in the
    /// address is the part #45's own list names as no secret going where it
    /// should not.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task TheRequestTheHandlerIsHandedCarriesTheHeadersThisPluginPutOnIt()
    {
        var inFront = new AHandlerThatRefusesWhatNoTestSetUp();

        using var provider = AContainerHolding(inFront, new AHandlerThatRefusesWhatNoTestSetUp());

        var adapter = AHandlerThatRefusesWhatNoTestSetUp.AnAdapterOver(
            provider,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);

        var query = new SourceQuery("trending", DiscoverTitleKind.Movie, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.FetchAsync(query, CancellationToken.None)).ConfigureAwait(true);

        var sent = Assert.Single(inFront.Sent);

        Assert.StartsWith("Bearer ", sent.Authorization, StringComparison.Ordinal);
        Assert.StartsWith("Jellyfin.Plugin.Template/", sent.UserAgent, StringComparison.Ordinal);
        Assert.Equal("application/json", sent.Accept);

        Assert.DoesNotContain(
            AHandlerThatRefusesWhatNoTestSetUp.TheCredentialItSupplies,
            Assert.Single(inFront.Asked).AbsoluteUri,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A container filled the way the server fills one, with a substitute in front of this plugin's client and another in front of every other.
    /// </summary>
    /// <param name="inFront">What answers this plugin's own named client.</param>
    /// <param name="anywhereElse">What answers the default client, so a call made through the wrong one is refused rather than made.</param>
    /// <returns>The container.</returns>
    private static ServiceProvider AContainerHolding(
        AHandlerThatRefusesWhatNoTestSetUp inFront,
        AHandlerThatRefusesWhatNoTestSetUp anywhereElse)
    {
        var services = new ServiceCollection();

        // What the server has already put in the container before it calls a
        // plugin's registrator, the same two every other test of this
        // registrator names and for the same reasons.
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(ServerLibraryAdapterStandIn.RefusingEveryCall());

        new PluginServiceRegistrator().RegisterServices(services, new ServerApplicationHostThatRefusesEveryCall());

        // The substitution the condition asks for. One registration, read by
        // the filter the registrator put on its own named client, and no test
        // names that client's name: a test naming it would pass while the
        // adapter asked for another.
        services.AddSingleton<HttpMessageHandler>(inFront);

        // The drift guard, and the reason nothing here can reach a source. The
        // default client is what a factory hands back for a name nobody
        // configured, so an adapter that lost its own name lands on this and is
        // refused rather than making the call.
        services.AddHttpClient(string.Empty).ConfigurePrimaryHttpMessageHandler(() => anywhereElse);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
