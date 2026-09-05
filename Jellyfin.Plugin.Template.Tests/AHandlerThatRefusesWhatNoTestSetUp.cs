using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Sources;
using Jellyfin.Plugin.Template.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The primary handler a test registers in front of this plugin's named client, refusing every address it was not set up for.
/// </summary>
/// <remarks>
/// <para>
/// #45's second condition on the reading taken on 2026-09-04. The injection
/// point is one named client, and the one thing a test replaces is that
/// client's primary handler; this is what a test replaces it with. Nothing here
/// opens a socket, so a call that reaches this handler is one that would have
/// left the machine and did not.
/// </para>
/// <para>
/// It refuses rather than answering an address nobody named, which is the same
/// argument <see cref="ATransportThatRefusesWhatNoTestSetUp"/> makes at itself
/// and is not repeated here: a double that answered anything would hide the
/// accidental live call the whole arrangement exists against. What is new is
/// where it sits. That one stands in front of the adapter's own transport
/// function, one layer inside the client; this one stands where the runtime's
/// own handler would, so the request it is handed is the request the client
/// composed, headers and address included, and what it returns goes back
/// through the client's own reading of a response.
/// </para>
/// <para>
/// It names <see cref="HttpRequestMessage"/>, which is why
/// `no-network-outside-source-adapter` excepts this one file by path. The rule's
/// subject is every tracked C# file but an adapter's, and until this landed a
/// handler could not be written anywhere in this project at all, which is what
/// the transport double above says at itself as its reason for standing
/// somewhere else. The exception is this path rather than a shape, so a second
/// handler is a second entry written on purpose.
/// </para>
/// <para>
/// What it does not do. It is not a property of the suite: nothing obliges a
/// test to register it, and a test building a client without it reaches a host
/// with nothing here noticing. That half of #46 is the gate running the suite
/// with no route out, and it is not this file either.
/// </para>
/// </remarks>
internal sealed class AHandlerThatRefusesWhatNoTestSetUp : HttpMessageHandler
{
    /// <summary>
    /// The credential <see cref="AnAdapterOver(System.IServiceProvider, Jellyfin.Plugin.Template.Time.IClock, Jellyfin.Plugin.Template.Sources.SourceLocale)"/> and its neighbour hand the adapter.
    /// </summary>
    /// <remarks>
    /// Named rather than written twice, because a test asserting that it does
    /// not travel in the address has to be looking for the same string the
    /// adapter was given. It is not a key: nothing that reaches this handler
    /// leaves the machine, so what it says is what a reader should take it for.
    /// </remarks>
    public const string TheCredentialItSupplies = "a-credential-no-source-would-answer";

    private readonly Dictionary<Uri, Func<HttpResponseMessage>> _replies = new Dictionary<Uri, Func<HttpResponseMessage>>();

    private readonly HashSet<Uri> _silent = new HashSet<Uri>();

    private readonly List<Uri> _asked = new List<Uri>();

    private readonly List<HeadersOnOneRequest> _sent = new List<HeadersOnOneRequest>();

    /// <summary>
    /// Gets every address this handler was asked for, in the order it was asked, refused ones included.
    /// </summary>
    /// <remarks>
    /// The refused ones are in it for the same reason they are in the transport
    /// double's list: a test counting attempts is counting calls the code chose
    /// to make, and one that was refused is still one of those.
    /// </remarks>
    public IReadOnlyList<Uri> Asked => _asked;

    /// <summary>
    /// Gets the headers of every request this handler was handed, in the order it was handed them.
    /// </summary>
    /// <remarks>
    /// The three this plugin puts on a call, read where a real endpoint would
    /// read them. They are copied out rather than kept as the request, because a
    /// request is disposed by the client once the call is over and a test
    /// reading one afterwards would be reading something already released.
    /// </remarks>
    public IReadOnlyList<HeadersOnOneRequest> Sent => _sent;

    /// <summary>
    /// An adapter whose client comes from a container this handler was put into.
    /// </summary>
    /// <param name="provider">The container a plugin registrator filled.</param>
    /// <param name="clock">What the instant on each record is read from.</param>
    /// <param name="locale">Which language to ask in and which region to ask about.</param>
    /// <returns>The adapter, over the factory the container holds.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// It lives here rather than in the test that uses it because of the rule
    /// this file is excepted from. Naming the factory interface is refused in
    /// every tracked C# file but an adapter's and this one, so the composition
    /// that puts a substitute in front of an adapter has to sit in the same file
    /// as the substitute. Putting it here keeps the exception at one path
    /// instead of two, which is what the rule asks for at itself.
    /// </para>
    /// <para>
    /// A credential is supplied because an adapter with none declines to ask
    /// before it reaches a client at all, and what this arrangement is for is
    /// the call. It is not a key: the handler below answers what a test set up
    /// and refuses everything else, so nothing this string could be would reach
    /// a source.
    /// </para>
    /// </remarks>
    public static TmdbSourceAdapter AnAdapterOver(IServiceProvider provider, IClock clock, SourceLocale locale)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new TmdbSourceAdapter(
            provider.GetRequiredService<IHttpClientFactory>(),
            TheCredentialItSupplies,
            clock,
            locale);
    }

    /// <summary>
    /// An adapter whose client comes from a container this handler was put into, with a deadline the test names.
    /// </summary>
    /// <param name="provider">The container a plugin registrator filled.</param>
    /// <param name="clock">What the instant on each record is read from.</param>
    /// <param name="locale">Which language to ask in and which region to ask about.</param>
    /// <param name="deadline">How long one request may take before it is given up on.</param>
    /// <returns>The adapter, over the factory the container holds.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <remarks>
    /// #45's fourth condition. The overload above leaves the deadline at the
    /// adapter's own default, which is longer than the whole suite takes to run,
    /// so a test about the deadline reached through it would be a test that
    /// waits. Naming <see cref="TimeSpan.Zero"/> makes the case the bound exists
    /// for - a request that has not answered - decidable rather than waited out.
    /// </remarks>
    public static TmdbSourceAdapter AnAdapterOver(IServiceProvider provider, IClock clock, SourceLocale locale, TimeSpan deadline)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new TmdbSourceAdapter(
            provider.GetRequiredService<IHttpClientFactory>(),
            TheCredentialItSupplies,
            clock,
            locale,
            deadline);
    }

    /// <summary>
    /// A container filled the way the server fills one, with a substitute in front of this plugin's client and another in front of every other.
    /// </summary>
    /// <param name="inFront">What answers this plugin's own named client.</param>
    /// <param name="anywhereElse">What answers the default client, so a call made through the wrong one is refused rather than made.</param>
    /// <returns>The container.</returns>
    /// <remarks>
    /// It sits beside the substitute for the same reason
    /// <see cref="AnAdapterOver(IServiceProvider, IClock, SourceLocale)"/> does,
    /// and it moved here when a second test file needed it: a copy in each would
    /// be two arrangements claiming to be the server's, and the one a test was
    /// not reading would be the one that drifted.
    /// </remarks>
    public static ServiceProvider AContainerHolding(
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

    /// <summary>
    /// Says what this handler answers one address with.
    /// </summary>
    /// <param name="address">The address a test expects the client to ask for.</param>
    /// <param name="status">The status code that comes back.</param>
    /// <param name="body">The body that comes back.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="address"/> or <paramref name="body"/> is null.</exception>
    /// <remarks>
    /// The answer is composed per call rather than held, because a response
    /// carries content that is read once and a second request for one address is
    /// a case a test is entitled to set up.
    /// </remarks>
    public void Answer(Uri address, HttpStatusCode status, string body)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(body);

        _replies[address] = () => new HttpResponseMessage(status) { Content = new StringContent(body) };
    }

    /// <summary>
    /// Says what this handler answers one address with, and how long the answer says to wait before asking again.
    /// </summary>
    /// <param name="address">The address a test expects the client to ask for.</param>
    /// <param name="status">The status code that comes back.</param>
    /// <param name="body">The body that comes back.</param>
    /// <param name="retryAfter">The wait the answer states, as a number of seconds.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="address"/> or <paramref name="body"/> is null.</exception>
    /// <remarks>
    /// The header rather than a field on a reply record, because that is the
    /// half of a refusal a transport double cannot carry: the wait a source
    /// states arrives on the response the client reads, and whether this plugin
    /// reads it is a property of the client and of the adapter together.
    /// </remarks>
    public void Answer(Uri address, HttpStatusCode status, string body, TimeSpan retryAfter)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(body);

        _replies[address] = () =>
        {
            var reply = new HttpResponseMessage(status) { Content = new StringContent(body) };
            reply.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
            return reply;
        };
    }

    /// <summary>
    /// Says that this handler takes one address and never answers it.
    /// </summary>
    /// <param name="address">The address a test expects the client to ask for.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="address"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// What a deadline is for, and the only arrangement in which the deadline
    /// firing is a decision rather than a race. A handler that answered would
    /// leave which of the two completed first up to the machine; a request that
    /// cannot complete until its token is cancelled means the timer is the only
    /// thing that can finish, on a loaded runner as on an idle one.
    /// </para>
    /// <para>
    /// It waits on the token rather than on the clock, so nothing here sleeps
    /// and no wall-clock interval is spent. The address is recorded as asked
    /// before the silence begins, because a call that left through the client
    /// and was never answered is still a call this plugin chose to make.
    /// </para>
    /// </remarks>
    public void NeverAnswer(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);

        _silent.Add(address);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when no test set up an answer for the address the client asked
    /// for. The message carries the address that was attempted and the ones that
    /// were set up, because the two are what separate a call nobody meant from a
    /// call whose address came out wrong.
    /// </exception>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var address = request.RequestUri ?? throw new InvalidOperationException("A request reached this handler with no address on it.");

        _asked.Add(address);
        _sent.Add(new HeadersOnOneRequest(
            First(request, "Authorization"),
            First(request, "User-Agent"),
            First(request, "Accept")));

        if (_silent.Contains(address))
        {
            return Silence(cancellationToken);
        }

        if (_replies.TryGetValue(address, out var reply))
        {
            return Task.FromResult(reply());
        }

        throw new InvalidOperationException(Refusal(address));
    }

    /// <summary>
    /// A request that finishes only when whoever made it stops waiting.
    /// </summary>
    /// <param name="cancellationToken">The token the client handed this handler.</param>
    /// <returns>A task that never carries a response.</returns>
    /// <remarks>
    /// The completion source is settled by the registration and by nothing
    /// else, and the registration is released when it is, so a test that set an
    /// address to be unanswered leaves neither a timer nor a live callback
    /// behind it.
    /// </remarks>
    private static async Task<HttpResponseMessage> Silence(CancellationToken cancellationToken)
    {
        var never = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        using (cancellationToken.Register(
            static waiting => ((TaskCompletionSource<HttpResponseMessage>)waiting!).TrySetCanceled(),
            never))
        {
            return await never.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The first value a request carries under one header name, or the empty string where it carries none.
    /// </summary>
    /// <param name="request">The request the client composed.</param>
    /// <param name="name">The header to read.</param>
    /// <returns>The value, or the empty string.</returns>
    /// <remarks>
    /// The empty string rather than null, so an assertion about what a header
    /// says reads as a comparison rather than as a null check, and an absent
    /// header fails on the value it was expected to have.
    /// </remarks>
    private static string First(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault() ?? string.Empty
            : string.Empty;

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
                ? "Nothing was set up on this handler at all."
                : "What was set up: " + string.Join(", ", _replies.Keys.Select(set => set.AbsoluteUri).OrderBy(set => set, StringComparer.Ordinal)) + ".");
}
