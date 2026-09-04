using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Sources;
using Jellyfin.Plugin.Template.Time;
using Microsoft.Extensions.DependencyInjection;

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
    /// The credential <see cref="AnAdapterOver"/> hands the adapter.
    /// </summary>
    /// <remarks>
    /// Named rather than written twice, because a test asserting that it does
    /// not travel in the address has to be looking for the same string the
    /// adapter was given. It is not a key: nothing that reaches this handler
    /// leaves the machine, so what it says is what a reader should take it for.
    /// </remarks>
    public const string TheCredentialItSupplies = "a-credential-no-source-would-answer";

    private readonly Dictionary<Uri, Func<HttpResponseMessage>> _replies = new Dictionary<Uri, Func<HttpResponseMessage>>();

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

        if (_replies.TryGetValue(address, out var reply))
        {
            return Task.FromResult(reply());
        }

        throw new InvalidOperationException(Refusal(address));
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
