using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The deadline, the rest and the backoff, driven through the handler a registration put in front of this plugin's client.
/// </summary>
/// <remarks>
/// <para>
/// #45's fourth condition, on the reading taken on 2026-09-05: the three
/// behaviours are met only where the assertions drive through the injected
/// handler the second condition built. Assertions that reach them by another
/// route prove the numbers and not the wiring, and the wiring is what a
/// substitute handler exists to prove.
/// </para>
/// <para>
/// What that changes, per behaviour, is not the same thing. The deadline was
/// asserted over a transport function a test supplied, one layer inside the
/// client, so what was unproven is that the bound survives the client at all.
/// The rest and the backoff were asserted over a double of the source
/// interface, with no adapter, no client and no handler anywhere in the
/// arrangement, so what was unproven is that a refusal a source states on the
/// wire becomes the refusal the rest is computed from. Both are proven here
/// against a run that carries the real adapter over the container's client.
/// </para>
/// <para>
/// Nothing sleeps and nothing waits. The deadline is named as
/// <see cref="TimeSpan.Zero"/> by the constructor the fourth condition needed,
/// the request it fires against is one the handler never answers, and every
/// interval afterwards passes by advancing <see cref="ClockATestAdvances"/>.
/// Nothing opens a socket, needs a real endpoint, touches a trust store, reads
/// an environment variable or turns a verification switch off.
/// </para>
/// </remarks>
public class TheDeadlineAndTheRestThroughTheInjectedHandlerTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _noon =
        new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The wait the answer states, which is what the rest is expected to be taken from.
    /// </summary>
    /// <remarks>
    /// Half an hour, so it is nowhere near the five minutes a refusal with no
    /// stated wait rests for. A test whose interval happened to match that
    /// default would pass whether the stated wait was read or ignored.
    /// </remarks>
    private static readonly TimeSpan _statedWait = TimeSpan.FromMinutes(30);

    /// <summary>
    /// A request the source never answers is given up on at the deadline, through the client the container holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The address is learned from a refusal rather than written out, for the
    /// reason the second condition's own test gives: a test that pasted the
    /// address would go on passing if the adapter started asking somewhere
    /// else. It is learned on a container of its own so that the counts
    /// asserted below are the calls this test made and no others.
    /// </para>
    /// <para>
    /// The outcome carries no message, which is the adapter's own rule for a
    /// deadline and is worth asserting here rather than only where the transport
    /// was a function: nothing was said, because nothing answered, and a message
    /// invented at this layer would be this plugin reporting a refusal the
    /// source never made.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task ARequestThatIsNeverAnsweredIsGivenUpOnAtTheDeadlineTheAdapterWasBuiltWith()
    {
        var query = new SourceQuery("trending", DiscoverTitleKind.Movie, null, null);
        var address = await TheAddressFor(query).ConfigureAwait(true);

        var inFront = new AHandlerThatRefusesWhatNoTestSetUp();
        var anywhereElse = new AHandlerThatRefusesWhatNoTestSetUp();

        using var provider = AHandlerThatRefusesWhatNoTestSetUp.AContainerHolding(inFront, anywhereElse);

        inFront.NeverAnswer(address);

        var adapter = AHandlerThatRefusesWhatNoTestSetUp.AnAdapterOver(
            provider,
            new ClockATestAdvances(_noon),
            SourceLocale.Unstated,
            TimeSpan.Zero);

        var answer = await adapter.FetchAsync(query, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(SourceOutcome.TemporarilyFailed, answer.Outcome);
        Assert.Null(answer.SourceMessage);
        Assert.Empty(answer.Titles);

        Assert.Equal(address, Assert.Single(inFront.Asked));
        Assert.Empty(anywhereElse.Asked);
    }

    /// <summary>
    /// A refusal the source stated on the wire stops the rest of the run asking it again.
    /// </summary>
    /// <remarks>
    /// The retry half, where the refusal is a status code and a header rather
    /// than a value a double was handed. Three shelves rather than two, so a run
    /// that stopped after the second request rather than after the first is
    /// still red, and all three read from one source so that a rest belonging to
    /// the source rather than to a shelf is what is being asserted.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task ARefusalOnTheWireStopsTheRestOfTheRunAskingTheSameSource()
    {
        var folder = Folder("handler-rest-one-run");
        Remove(folder);
        try
        {
            var shelves = new[] { Row(ShelfQuestion.Trending), Row(ShelfQuestion.Popular), Row(ShelfQuestion.TopRated) };

            var inFront = new AHandlerThatRefusesWhatNoTestSetUp();
            var anywhereElse = new AHandlerThatRefusesWhatNoTestSetUp();

            using var provider = AHandlerThatRefusesWhatNoTestSetUp.AContainerHolding(inFront, anywhereElse);

            foreach (var shelf in shelves)
            {
                inFront.Answer(
                    await TheAddressFor(shelf.Ask()).ConfigureAwait(true),
                    HttpStatusCode.TooManyRequests,
                    "too many requests",
                    _statedWait);
            }

            var clock = new ClockATestAdvances(_noon);
            var run = await Refresh(provider, clock, Store(folder))
                .RunAsync(shelves, progress: null, CancellationToken.None)
                .ConfigureAwait(true);

            Assert.Single(inFront.Asked);
            Assert.Empty(anywhereElse.Asked);

            Assert.Equal(3, run.Shelves.Count);
            Assert.All(run.Shelves, result => Assert.Equal(ShelfRefreshOutcome.PreviousKept, result.Outcome));
            Assert.All(run.Shelves, result => Assert.Equal(SourceOutcome.RateLimited, result.SourceOutcome));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The wait the source stated on the wire is how long it is left alone, and the next run after it asks again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The backoff half. What it adds to the test above is the boundary: a run
    /// one minute inside the stated wait asks nothing, and a run one minute past
    /// it asks. Both are reached by advancing the clock rather than by waiting,
    /// which is why the two are distinguishable at all.
    /// </para>
    /// <para>
    /// The wait travels as a response header, so it is the half of a refusal a
    /// transport function cannot carry. A source stating half an hour and a
    /// source stating nothing are read by this plugin as half an hour and as
    /// five minutes, and only the arrangement below can tell which of the two
    /// the run acted on.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task TheWaitTheSourceStatedIsHowLongItIsLeftAlone()
    {
        var folder = Folder("handler-rest-backoff");
        Remove(folder);
        try
        {
            var shelf = Row(ShelfQuestion.Trending);

            var inFront = new AHandlerThatRefusesWhatNoTestSetUp();
            var anywhereElse = new AHandlerThatRefusesWhatNoTestSetUp();

            using var provider = AHandlerThatRefusesWhatNoTestSetUp.AContainerHolding(inFront, anywhereElse);

            inFront.Answer(
                await TheAddressFor(shelf.Ask()).ConfigureAwait(true),
                HttpStatusCode.TooManyRequests,
                "too many requests",
                _statedWait);

            var clock = new ClockATestAdvances(_noon);
            var refresh = Refresh(provider, clock, Store(folder));

            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None).ConfigureAwait(true);

            Assert.Single(inFront.Asked);

            clock.Advance(_statedWait - TimeSpan.FromMinutes(1));
            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None).ConfigureAwait(true);

            Assert.Single(inFront.Asked);

            clock.Advance(TimeSpan.FromMinutes(2));
            await refresh.RunAsync(new[] { shelf }, progress: null, CancellationToken.None).ConfigureAwait(true);

            Assert.Equal(2, inFront.Asked.Count);
            Assert.Empty(anywhereElse.Asked);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The address this plugin builds for one question, read off a handler that refused it.
    /// </summary>
    /// <param name="query">What a shelf asks its source.</param>
    /// <returns>The address the adapter composed.</returns>
    /// <remarks>
    /// On a container of its own, so nothing it asks is counted among the calls
    /// a test is asserting about. What makes it the right address rather than a
    /// guess is that the adapter composed it: the refusal names what the client
    /// was about to send, which is the same reading the second condition's test
    /// already relies on.
    /// </remarks>
    private static async Task<Uri> TheAddressFor(SourceQuery query)
    {
        var learning = new AHandlerThatRefusesWhatNoTestSetUp();

        using var provider = AHandlerThatRefusesWhatNoTestSetUp.AContainerHolding(
            learning,
            new AHandlerThatRefusesWhatNoTestSetUp());

        var adapter = AHandlerThatRefusesWhatNoTestSetUp.AnAdapterOver(
            provider,
            new ClockATestAdvances(_noon),
            SourceLocale.Unstated);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.FetchAsync(query, CancellationToken.None)).ConfigureAwait(true);

        return Assert.Single(learning.Asked);
    }

    private static CatalogueRefresh Refresh(IServiceProvider provider, ClockATestAdvances clock, CatalogueDocumentStore store) =>
        new CatalogueRefresh(
            new[] { AHandlerThatRefusesWhatNoTestSetUp.AnAdapterOver(provider, clock, SourceLocale.Unstated) },
            store,
            null,
            clock,
            new PauseATestWatches(),
            new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>());

    private static Shelf Row(ShelfQuestion question) => new Shelf
    {
        DisplayName = "A row of films",
        Question = question,
        Kind = DiscoverTitleKind.Movie,
        Source = MetadataSource.Tmdb,
        Cap = 5
    };

    private static CatalogueDocumentStore Store(string folder) =>
        new CatalogueDocumentStore(
            new CatalogueDirectory(folder),
            new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());

    private static string Folder(string name) => Path.Combine(Path.GetTempPath(), TestFolders, name);

    private static void Remove(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
