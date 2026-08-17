using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;
using Jellyfin.Plugin.Template.Tests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What happens when something in this suite asks for an address no test said an answer for.
/// </summary>
/// <remarks>
/// The guard is <see cref="ATransportThatRefusesWhatNoTestSetUp"/> and these are
/// the tests that watch it bite. A guard nobody has seen refuse anything is a
/// claim, and the claim this one carries is the expensive kind: that no run of
/// this suite quietly spends a third party's rate budget.
///
/// Every case below drives the real adapter rather than the transport on its
/// own, because the address that would leave the machine is one the adapter
/// composes. A test calling the transport with an address it typed itself would
/// prove the dictionary lookup and nothing about the code that asks.
///
/// The bound is on the file the guard is in and is not softened here: this
/// refuses the calls made through an adapter a test handed it to, and it is not
/// a property of the suite as a whole.
/// </remarks>
public class ACallNoTestSetUpIsRefusedTests
{
    /// <summary>
    /// The instant the clock these adapters are given reads.
    /// </summary>
    /// <remarks>
    /// Fixed rather than read from the machine, for the reason
    /// <c>no-wall-clock</c> exists: a test reading the machine's clock is a
    /// test whose subject moves.
    /// </remarks>
    private static readonly DateTimeOffset _fetched = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The address the shipped adapter composes for the question every case here asks.
    /// </summary>
    /// <remarks>
    /// Written out rather than taken from the adapter, so a change to the
    /// address it builds fails these tests instead of moving both sides
    /// together and asserting that a value equals itself.
    /// </remarks>
    private static readonly Uri _trendingFilms = new Uri("https://api.themoviedb.org/3/trending/movie/week?page=1", UriKind.Absolute);

    /// <summary>
    /// The question every case here asks.
    /// </summary>
    /// <remarks>
    /// One of the three names the shipped adapter has a question for, so the
    /// address it composes is a real one rather than the null a name outside
    /// that set falls to. A name it does not recognise never reaches a
    /// transport at all, which would make every case below pass without the
    /// guard existing.
    /// </remarks>
    private static readonly SourceQuery _trending = new SourceQuery("trending", DiscoverTitleKind.Movie, null, null);

    /// <summary>
    /// A call nobody set up does not go out, and the failure says where it was going.
    /// </summary>
    /// <remarks>
    /// The message is asserted rather than only the type. A refusal that did
    /// not name the address leaves whoever meets it with a stack trace through
    /// an adapter and no way to tell an accidental live call from one whose
    /// address came out wrong, which are the two failures worth telling apart.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AnAddressNoTestSetUpIsRefusedAndNamed()
    {
        var transport = new ATransportThatRefusesWhatNoTestSetUp();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Adapter(transport).FetchAsync(_trending, CancellationToken.None)).ConfigureAwait(true);

        Assert.Contains(_trendingFilms.AbsoluteUri, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("would have left the machine", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The refusal says what was set up, so an address that came out wrong reads as one.
    /// </summary>
    /// <remarks>
    /// The near-miss this is for is a one-character change to a path or a page
    /// number. Without the second half of the message, a test that set up the
    /// right answer for the wrong address fails with the same words as a test
    /// that set nothing up, and the two want opposite repairs.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARefusalNamesWhatWasSetUpAsWellAsWhatWasAsked()
    {
        var transport = new ATransportThatRefusesWhatNoTestSetUp();

        transport.Answer(
            new Uri("https://api.themoviedb.org/3/trending/movie/week?page=2", UriKind.Absolute),
            new SourceTransportReply(200, TmdbFixtures.Body(TmdbFixtures.EmptyPage), null));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Adapter(transport).FetchAsync(_trending, CancellationToken.None)).ConfigureAwait(true);

        Assert.Contains("page=1", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("page=2", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An address a test did set up is answered, so the guard refuses a question rather than every question.
    /// </summary>
    /// <remarks>
    /// The leg that keeps the two above honest. A transport that threw whatever
    /// it was handed would pass both of them and would make every other use of
    /// this double impossible, and the failure would look like the guard
    /// working.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AnAddressATestSetUpIsAnswered()
    {
        var transport = new ATransportThatRefusesWhatNoTestSetUp();

        transport.Answer(_trendingFilms, new SourceTransportReply(200, TmdbFixtures.Body(TmdbFixtures.MoviePage), null));

        var answer = await Adapter(transport).FetchAsync(_trending, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.Equal(2, answer.Titles.Count);
        Assert.Equal(new[] { _trendingFilms }, transport.Asked);
    }

    /// <summary>
    /// A refused address is still an address something asked for.
    /// </summary>
    /// <remarks>
    /// What the record is for. A later issue asserts that a failure was not
    /// retried, which is a count of attempts, and an attempt that was refused
    /// is one the code chose to make. A transport that recorded only the
    /// answered ones would report zero for a run that asked ten times.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARefusedAddressIsRecordedAsHavingBeenAsked()
    {
        var transport = new ATransportThatRefusesWhatNoTestSetUp();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Adapter(transport).FetchAsync(_trending, CancellationToken.None)).ConfigureAwait(true);

        Assert.Equal(new[] { _trendingFilms }, transport.Asked);
    }

    /// <summary>
    /// The shipped adapter, given the guard instead of a way out of the machine.
    /// </summary>
    /// <param name="transport">What it asks through.</param>
    /// <returns>An adapter that reaches nothing.</returns>
    private static TmdbSourceAdapter Adapter(ATransportThatRefusesWhatNoTestSetUp transport) =>
        new(transport.SendAsync, configured: true, new ClockATestAdvances(_fetched));
}
