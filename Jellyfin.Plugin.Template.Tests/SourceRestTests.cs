using System;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// How long a source that refused is left alone, and when this plugin stops
/// taking it at its word.
/// </summary>
/// <remarks>
/// #78's third and fourth conditions, asserted on the type that decides them
/// rather than through a run, so that the boundary cases are one call each. What
/// a run then does with the decision is
/// <c>ARefusedSourceIsLeftAloneTests</c>.
///
/// Every instant here comes from a value this file declares and is compared
/// against a value this file declares. Nothing reads a machine clock and
/// nothing sleeps, which is the property #78's fifth condition asks for and the
/// reason the rest is an instant rather than a delay.
/// </remarks>
public class SourceRestTests
{
    private static readonly DateTimeOffset _noon =
        new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A source nobody has asked yet is not resting.
    /// </summary>
    /// <remarks>
    /// First, because every assertion below is that a source IS resting, and a
    /// type answering that of every source would pass all of them.
    /// </remarks>
    [Fact]
    public void ASourceThatHasNotRefusedIsNotResting()
    {
        Assert.Null(new SourceRest().RestingFor(MetadataSource.Tmdb, _noon));
    }

    /// <summary>
    /// The wait a source stated is the rest it gets, to the second.
    /// </summary>
    /// <remarks>
    /// #78's third condition. The wait is asserted as an equality rather than
    /// as a lower bound, because a rest longer than the source asked for is a
    /// plugin deciding it knows the source's budget better than the source
    /// does, and a rest shorter than it is the second request the source
    /// refused.
    /// </remarks>
    [Fact]
    public void TheWaitASourceStatedIsTheRestItGets()
    {
        var rest = new SourceRest();

        var taken = rest.Refused(MetadataSource.Tmdb, SourceAnswer.RateLimited(TimeSpan.FromSeconds(17), "slow down"), _noon);

        Assert.Equal(TimeSpan.FromSeconds(17), taken.Rest);
        Assert.Equal(_noon + TimeSpan.FromSeconds(17), taken.Until);
        Assert.False(taken.GaveUp);
    }

    /// <summary>
    /// A rest is over at the instant it runs out and not one tick later.
    /// </summary>
    /// <param name="secondsAfterTheRefusal">How far past the refusal the clock reads.</param>
    /// <param name="resting">Whether the source is still being left alone then.</param>
    /// <remarks>
    /// The boundary in both directions, one tick either side, which is what
    /// this repository asks of every comparison a wrong sign would hide. A
    /// greater-than where this has a greater-or-equal leaves the source resting
    /// for one extra tick, which no assertion about "still resting after a
    /// minute" would catch.
    /// </remarks>
    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    [InlineData(18, false)]
    public void ARestIsOverAtTheInstantItRunsOut(int secondsAfterTheRefusal, bool resting)
    {
        var rest = new SourceRest();

        rest.Refused(MetadataSource.Tmdb, SourceAnswer.RateLimited(TimeSpan.FromSeconds(17), null), _noon);

        var left = rest.RestingFor(MetadataSource.Tmdb, _noon + TimeSpan.FromSeconds(secondsAfterTheRefusal));

        Assert.Equal(resting, left is not null);
    }

    /// <summary>
    /// A refusal that named no wait doubles the rest each time, and stops at
    /// the longest one.
    /// </summary>
    /// <param name="refusalsInARow">How many refusals in a row have arrived.</param>
    /// <param name="minutes">How long the source is left alone after the last of them.</param>
    /// <remarks>
    /// #78's fourth condition, in the backed-off half. The source implemented
    /// here names no rate-limit response header at all, which
    /// <c>docs/source-api/tmdb.md</c> records, so this is the ordinary path
    /// rather than the exception.
    ///
    /// The fourth row is the giving up rather than a fourth doubling: twice
    /// twenty minutes is forty, and what the fourth refusal earns is the six
    /// hours the threshold imposes.
    /// </remarks>
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 360)]
    public void ARefusalThatNamedNoWaitDoublesTheRest(int refusalsInARow, int minutes)
    {
        var rest = new SourceRest();
        var taken = default(SourceRestTaken);

        for (var refusal = 0; refusal < refusalsInARow; refusal++)
        {
            taken = rest.Refused(MetadataSource.Tmdb, SourceAnswer.TemporarilyFailed(null), _noon);
        }

        Assert.Equal(TimeSpan.FromMinutes(minutes), taken.Rest);
        Assert.Equal(refusalsInARow, taken.Refusals);
    }

    /// <summary>
    /// A source that keeps naming a short wait is still given up on.
    /// </summary>
    /// <remarks>
    /// This is the bound #78's fourth condition asks for and it is the one a
    /// backoff alone does not give: a source answering "wait one second" for
    /// ever is asked for ever by anything that only honours the stated wait.
    /// The giving up therefore overrides the source's own number rather than
    /// deferring to it, and the assertion is that the fourth rest is the
    /// longest one rather than the second the source asked for.
    /// </remarks>
    [Fact]
    public void ASourceThatKeepsNamingAShortWaitIsStillGivenUpOn()
    {
        var rest = new SourceRest();
        var taken = default(SourceRestTaken);

        for (var refusal = 0; refusal < SourceRest.Tries; refusal++)
        {
            Assert.False(taken.GaveUp);

            taken = rest.Refused(MetadataSource.Tmdb, SourceAnswer.RateLimited(TimeSpan.FromSeconds(1), null), _noon);
        }

        Assert.True(taken.GaveUp);
        Assert.Equal(SourceRest.LongestRest, taken.Rest);
        Assert.Equal(SourceRest.Tries, taken.Refusals);
    }

    /// <summary>
    /// A source that answers has its refusals forgotten.
    /// </summary>
    /// <remarks>
    /// The count is of refusals IN A ROW. Without this a source that fails once
    /// a week reaches the threshold after a month of working and is then left
    /// alone for six hours for four failures spread across it, which is a
    /// plugin that stops asking a source that is fine.
    /// </remarks>
    [Fact]
    public void ASourceThatAnswersHasItsRefusalsForgotten()
    {
        var rest = new SourceRest();

        rest.Refused(MetadataSource.Tmdb, SourceAnswer.TemporarilyFailed(null), _noon);
        rest.Refused(MetadataSource.Tmdb, SourceAnswer.TemporarilyFailed(null), _noon);
        rest.Answered(MetadataSource.Tmdb);

        var taken = rest.Refused(MetadataSource.Tmdb, SourceAnswer.TemporarilyFailed(null), _noon);

        Assert.Equal(1, taken.Refusals);
        Assert.Equal(SourceRest.FirstRest, taken.Rest);
        Assert.Null(rest.RestingFor(MetadataSource.Tvdb, _noon));
    }

    /// <summary>
    /// One source refusing does not leave another one alone.
    /// </summary>
    /// <remarks>
    /// The rest is a source's, because the budget it protects is the source's.
    /// A rest kept for the plugin rather than per source would stop this server
    /// asking every other body it is set up for because one of them refused,
    /// and that failure is invisible: the other shelves report exactly what
    /// they report when their own source refused.
    /// </remarks>
    [Fact]
    public void OneSourceRefusingDoesNotLeaveAnotherAlone()
    {
        var rest = new SourceRest();

        rest.Refused(MetadataSource.Tmdb, SourceAnswer.RateLimited(TimeSpan.FromHours(1), null), _noon);

        Assert.NotNull(rest.RestingFor(MetadataSource.Tmdb, _noon));
        Assert.Null(rest.RestingFor(MetadataSource.Tvdb, _noon));
    }

    /// <summary>
    /// A shelf that was not asked reports the refusal the source really made.
    /// </summary>
    /// <remarks>
    /// The source's own answer, with its own message, rather than one composed
    /// where the shelf was skipped. A refusal invented for a shelf nobody asked
    /// about would put words in front of an operator that no source ever said,
    /// and this repository's rule against that is the same one that keeps a
    /// source's message on <see cref="SourceAnswer.SourceMessage"/> rather than
    /// letting the plugin describe what it thought had happened.
    /// </remarks>
    [Fact]
    public void AShelfThatWasNotAskedReportsTheRefusalTheSourceMade()
    {
        var rest = new SourceRest();
        var refusal = SourceAnswer.RateLimited(TimeSpan.FromMinutes(30), "too many requests");

        rest.Refused(MetadataSource.Tmdb, refusal, _noon);

        var standing = rest.Standing(MetadataSource.Tmdb);

        Assert.Same(refusal, standing);
        Assert.Equal("too many requests", standing.SourceMessage);
    }

    /// <summary>
    /// A source that is not resting has no standing refusal to report.
    /// </summary>
    /// <remarks>
    /// It throws rather than answering with an absence, because every caller
    /// reaches this only after being told the source is resting, and an absence
    /// here would be read by such a caller as a refusal with nothing in it.
    /// </remarks>
    [Fact]
    public void ASourceThatIsNotRestingHasNoStandingRefusal()
    {
        Assert.Throws<InvalidOperationException>(() => new SourceRest().Standing(MetadataSource.Tmdb));
    }

    /// <summary>
    /// An answer and a source that has not been set up are not refusals.
    /// </summary>
    /// <param name="outcome">Which of the two non-refusals this case is about.</param>
    /// <remarks>
    /// Refused by name rather than ignored, so that a caller reaching here with
    /// one of the two has a defect reported rather than a source quietly left
    /// alone for five minutes. A source that has not been set up is the case to
    /// be careful about: nothing is wrong with it, so a rest would stop this
    /// plugin asking a source whose only fault is that a shelf named it.
    /// </remarks>
    [Theory]
    [InlineData(SourceOutcome.NotConfigured)]
    [InlineData(SourceOutcome.Answered)]
    public void AnAnswerIsNotARefusal(SourceOutcome outcome)
    {
        var answer = outcome is SourceOutcome.NotConfigured
            ? SourceAnswer.NotConfigured()
            : SourceAnswer.Answered(Array.Empty<DiscoverTitle>(), totalCount: 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SourceRest().Refused(MetadataSource.Tmdb, answer, _noon));
    }

    /// <summary>
    /// A wait longer than the longest rest is held to the longest rest.
    /// </summary>
    /// <remarks>
    /// A source is taken at its word about waiting longer, up to the point
    /// where taking it at its word means never asking again. A header saying to
    /// come back in a week would otherwise put a shelf out of date for a week
    /// with nothing between the operator and it, and the ceiling is the same
    /// one the giving up uses so there is one longest rest rather than two.
    /// </remarks>
    [Fact]
    public void AWaitLongerThanTheLongestRestIsHeldToIt()
    {
        var taken = new SourceRest()
            .Refused(MetadataSource.Tmdb, SourceAnswer.RateLimited(TimeSpan.FromDays(7), null), _noon);

        Assert.Equal(SourceRest.LongestRest, taken.Rest);
        Assert.False(taken.GaveUp);
    }
}
