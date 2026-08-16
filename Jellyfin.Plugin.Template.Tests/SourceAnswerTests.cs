using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The four ways a source can answer, and what each one may carry.
/// </summary>
/// <remarks>
/// #73's third condition asks the interface to distinguish not configured, rate
/// limited, temporarily failed, and answered with nothing. The first test below
/// is that condition read as a property: the four are pairwise distinguishable,
/// and a caller that only asked whether there were titles would see three of
/// them as one.
/// </remarks>
public class SourceAnswerTests
{
    /// <summary>
    /// The instant these fixtures were fetched at.
    /// </summary>
    /// <remarks>
    /// A fixed value rather than a read of any clock, so a record built here
    /// carries the same age on every run. Nothing in this file asserts against
    /// it; it is here because the record refuses to be built without one.
    /// </remarks>
    private static readonly DateTimeOffset _fetched = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The four answers are told apart by their outcome and not by whether titles came with them.
    /// </summary>
    /// <remarks>
    /// The failure this is written against is the one #79 names. If a rate
    /// limit, a timeout and a source that genuinely had nothing all reach a
    /// refresh as an empty list, the refresh cannot keep the previous contents
    /// of the shelf that failed while replacing the shelf that is legitimately
    /// empty. Both directions are wrong and neither is recoverable later.
    ///
    /// Every one of the four carries no titles here, which is the case that
    /// makes the distinction load-bearing rather than the case that makes it
    /// obvious.
    /// </remarks>
    [Fact]
    public void TheFourAnswersAreDistinguishableWhenNoneOfThemCarriesATitle()
    {
        var answers = new[]
        {
            SourceAnswer.Answered(Array.Empty<DiscoverTitle>(), totalCount: 0),
            SourceAnswer.NotConfigured(),
            SourceAnswer.RateLimited(TimeSpan.FromSeconds(30), "Too many requests."),
            SourceAnswer.TemporarilyFailed("The service is unavailable.")
        };

        Assert.All(answers, answer => Assert.Empty(answer.Titles));

        var outcomes = answers.Select(answer => answer.Outcome).ToArray();

        Assert.Equal(outcomes.Length, outcomes.Distinct().Count());
        Assert.DoesNotContain(SourceOutcome.None, outcomes);
    }

    /// <summary>
    /// A source that was asked and had nothing is an answer rather than a failure.
    /// </summary>
    /// <remarks>
    /// Stated on its own because it is the one of the four most likely to be
    /// folded into a failure by whoever writes the refresh. An empty shelf that
    /// a source confirmed is empty is the state #63 needs to be able to tell a
    /// client about.
    /// </remarks>
    [Fact]
    public void AnsweredWithNothingIsAnAnswer()
    {
        var answer = SourceAnswer.Answered(Array.Empty<DiscoverTitle>(), totalCount: 0);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.Empty(answer.Titles);
        Assert.Equal(0, answer.TotalCount);
        Assert.Null(answer.RetryAfter);
        Assert.Null(answer.SourceMessage);
    }

    /// <summary>
    /// A source that answered carries what it gave, in the order it gave it.
    /// </summary>
    [Fact]
    public void AnsweredCarriesTheTitlesInTheOrderTheSourceListedThem()
    {
        var first = TitleCalled("Arrival", "329865");
        var second = TitleCalled("Sicario", "273481");

        var answer = SourceAnswer.Answered(new[] { first, second }, totalCount: 40);

        Assert.Equal(new[] { first, second }, answer.Titles);
        Assert.Equal(40, answer.TotalCount);
    }

    /// <summary>
    /// A total the source did not give is absent rather than zero.
    /// </summary>
    /// <remarks>
    /// A caller paging through a shelf stops when it has the total. Reading a
    /// missing total as zero would stop it after the first page, and reading it
    /// as the page length would stop it one page late for every source that
    /// reports one.
    /// </remarks>
    [Fact]
    public void ATotalTheSourceDidNotGiveIsNull()
    {
        var answer = SourceAnswer.Answered(new[] { TitleCalled("Arrival", "329865") }, totalCount: null);

        Assert.Null(answer.TotalCount);
        Assert.Single(answer.Titles);
    }

    /// <summary>
    /// A total smaller than the page it describes is refused.
    /// </summary>
    /// <remarks>
    /// The near-miss is an adapter that reports the count of the page it just
    /// read instead of the count the source gave, which is right until the
    /// source returns a short page.
    /// </remarks>
    [Fact]
    public void ATotalSmallerThanThePageIsRefused()
    {
        var titles = new[] { TitleCalled("Arrival", "329865"), TitleCalled("Sicario", "273481") };

        Assert.Throws<ArgumentOutOfRangeException>(() => SourceAnswer.Answered(titles, totalCount: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SourceAnswer.Answered(titles, totalCount: -1));
    }

    /// <summary>
    /// A set that is null, or that holds a null, is refused.
    /// </summary>
    /// <remarks>
    /// An adapter that dropped a title it could not map leaves a shorter set.
    /// A hole in the set would reach a shelf as a title with no fields at all,
    /// and the first thing to read it would be the item the surface hands a
    /// client.
    /// </remarks>
    [Fact]
    public void ASetThatIsNullOrHoldsANullIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => SourceAnswer.Answered(null!, totalCount: null));
        Assert.Throws<ArgumentNullException>(
            () => SourceAnswer.Answered(new DiscoverTitle?[] { TitleCalled("Arrival", "329865"), null }!, totalCount: null));
    }

    /// <summary>
    /// A rate limit carries how long to wait, and refuses a wait that runs backwards.
    /// </summary>
    /// <remarks>
    /// A negative wait is what arithmetic on a date produces when a source's
    /// clock and this server's disagree, and #78's backoff would read it as
    /// permission to ask again at once. That is the one thing the source
    /// refused, so it is refused here rather than clamped, because a clamp
    /// would hide a source whose dates cannot be trusted.
    /// </remarks>
    [Fact]
    public void ARateLimitCarriesItsWaitAndRefusesANegativeOne()
    {
        var limited = SourceAnswer.RateLimited(TimeSpan.FromSeconds(30), "Too many requests.");

        Assert.Equal(SourceOutcome.RateLimited, limited.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(30), limited.RetryAfter);
        Assert.Equal("Too many requests.", limited.SourceMessage);

        Assert.Null(SourceAnswer.RateLimited(retryAfter: null, sourceMessage: null).RetryAfter);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SourceAnswer.RateLimited(TimeSpan.FromSeconds(-1), sourceMessage: null));
    }

    /// <summary>
    /// A message with nothing in it is stored as no message.
    /// </summary>
    /// <remarks>
    /// An error body that is present and blank is a source that said nothing,
    /// and an operator shown an empty line under "what the source said" reads
    /// it as this plugin losing the message. Whitespace as well as empty,
    /// because a body of one newline is the common shape of the first.
    /// </remarks>
    [Fact]
    public void ABlankMessageIsNoMessage()
    {
        Assert.Null(SourceAnswer.TemporarilyFailed(string.Empty).SourceMessage);
        Assert.Null(SourceAnswer.TemporarilyFailed("   \n ").SourceMessage);
        Assert.Null(SourceAnswer.RateLimited(retryAfter: null, sourceMessage: " ").SourceMessage);
        Assert.Equal("Upstream timed out.", SourceAnswer.TemporarilyFailed("Upstream timed out.").SourceMessage);
    }

    /// <summary>
    /// Nothing but an answer can carry a title, a total or a wait.
    /// </summary>
    /// <remarks>
    /// By construction rather than by a check: the three constructors that are
    /// not <see cref="SourceAnswer.Answered"/> take no titles and no total, and
    /// only <see cref="SourceAnswer.RateLimited"/> takes a wait. This test is
    /// what says so out loud, because "there is no way to build one" is a claim
    /// a reader cannot check by looking at a call site.
    /// </remarks>
    [Fact]
    public void OnlyAnAnswerCarriesTitlesAndOnlyARateLimitCarriesAWait()
    {
        var notConfigured = SourceAnswer.NotConfigured();
        var failed = SourceAnswer.TemporarilyFailed("The service is unavailable.");

        Assert.Empty(notConfigured.Titles);
        Assert.Null(notConfigured.TotalCount);
        Assert.Null(notConfigured.RetryAfter);
        Assert.Null(notConfigured.SourceMessage);

        Assert.Empty(failed.Titles);
        Assert.Null(failed.TotalCount);
        Assert.Null(failed.RetryAfter);
        Assert.Equal("The service is unavailable.", failed.SourceMessage);
    }

    /// <summary>
    /// Builds a title the way an adapter would build one out of a response.
    /// </summary>
    /// <param name="name">What the source called it.</param>
    /// <param name="identifier">The identifier the source gave for it.</param>
    /// <returns>The title.</returns>
    private static DiscoverTitle TitleCalled(string name, string identifier) => new DiscoverTitle
    {
        Identity = new DiscoverTitleIdentity(
            new List<ProviderIdentifier> { new ProviderIdentifier(MetadataSource.Tmdb, identifier) }),
        Kind = DiscoverTitleKind.Movie,
        FetchedAt = _fetched,
        Name = name
    };
}
