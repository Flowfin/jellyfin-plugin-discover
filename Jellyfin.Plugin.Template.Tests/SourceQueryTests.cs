using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What a source may be asked, and what it may not.
/// </summary>
/// <remarks>
/// Every refusal here costs a request against somebody else's rate budget if it
/// is not made, which is the reason the query validates rather than the adapter
/// deciding for itself. A malformed question that reaches a source is a request
/// spent on an answer nobody wanted, and it is spent again on every refresh
/// until somebody notices.
/// </remarks>
public class SourceQueryTests
{
    /// <summary>
    /// A question with everything on it passes and comes back unchanged.
    /// </summary>
    [Fact]
    public void AWellFormedQuestionIsReturnedAsItWas()
    {
        var query = new SourceQuery("popular-this-week", DiscoverTitleKind.Movie, StartIndex: 20, Limit: 20);

        Assert.Equal(query, query.Validated());
    }

    /// <summary>
    /// A question with no name is refused.
    /// </summary>
    /// <remarks>
    /// The near-miss is a shelf record whose name field was never filled,
    /// which arrives here as an empty string rather than as a null, so both
    /// spellings and whitespace are refused together.
    /// </remarks>
    [Fact]
    public void AQuestionWithNoNameIsRefused()
    {
        Assert.Throws<ArgumentException>(
            () => new SourceQuery(null!, DiscoverTitleKind.Movie, null, null).Validated());
        Assert.Throws<ArgumentException>(
            () => new SourceQuery(string.Empty, DiscoverTitleKind.Movie, null, null).Validated());
        Assert.Throws<ArgumentException>(
            () => new SourceQuery("  ", DiscoverTitleKind.Movie, null, null).Validated());
    }

    /// <summary>
    /// A question that names no kind of title is refused.
    /// </summary>
    /// <remarks>
    /// <see cref="DiscoverTitleKind.None"/> is what an unset field reads as,
    /// and an adapter handed it has to choose a kind, which is a choice that
    /// belongs to the shelf. The same refusal covers a value cast in from
    /// outside the enum.
    /// </remarks>
    [Fact]
    public void AQuestionThatNamesNoKindIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SourceQuery("popular-this-week", DiscoverTitleKind.None, null, null).Validated());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SourceQuery("popular-this-week", (DiscoverTitleKind)99, null, null).Validated());
    }

    /// <summary>
    /// A page that runs backwards, or asks for nothing, is refused.
    /// </summary>
    /// <remarks>
    /// A limit of zero is the one worth being deliberate about. It is what an
    /// unset field reads as, and treating it as "no limit" would turn a shelf
    /// that forgot to say how many titles it wanted into a request for the
    /// whole catalogue. Null is how a caller says it has no limit of its own.
    /// </remarks>
    [Fact]
    public void APageThatRunsBackwardsOrAsksForNothingIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SourceQuery("popular-this-week", DiscoverTitleKind.Movie, StartIndex: -1, Limit: null).Validated());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SourceQuery("popular-this-week", DiscoverTitleKind.Movie, StartIndex: null, Limit: 0).Validated());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SourceQuery("popular-this-week", DiscoverTitleKind.Movie, StartIndex: null, Limit: -20).Validated());

        var noPage = new SourceQuery("popular-this-week", DiscoverTitleKind.Movie, StartIndex: null, Limit: null);

        Assert.Equal(noPage, noPage.Validated());
    }

    /// <summary>
    /// A source is asked what it was asked, and says so afterwards.
    /// </summary>
    /// <remarks>
    /// The fake records rather than only answers, which is what the counting in
    /// #78 and #79 needs. This is the assertion that says the recording works,
    /// so a later test asserting "asked once and not twice" is asserting
    /// something.
    /// </remarks>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task ASourceRecordsTheQuestionsItWasAsked()
    {
        var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
        var films = new SourceQuery("popular-this-week", DiscoverTitleKind.Movie, null, null);
        var series = new SourceQuery("popular-this-week", DiscoverTitleKind.Series, null, null);

        source.Answer(films, SourceAnswer.Answered(Array.Empty<DiscoverTitle>(), totalCount: 0));

        var answeredFilms = await source.FetchAsync(films, CancellationToken.None).ConfigureAwait(true);
        var answeredSeries = await source.FetchAsync(series, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(new[] { films, series }, source.Asked);
        Assert.Equal(SourceOutcome.Answered, answeredFilms.Outcome);
        Assert.Equal(SourceOutcome.NotConfigured, answeredSeries.Outcome);
    }
}
