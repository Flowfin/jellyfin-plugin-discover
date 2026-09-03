using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Configuration;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Jellyfin.Plugin.Template.Tests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What the first source adapter makes of what a source sends it.
/// </summary>
/// <remarks>
/// Every test here supplies the reply itself, so what is judged is the reading
/// half of the adapter: the address it builds, the fields it maps and which of
/// the four outcomes it answers with. Nothing here reaches a network and
/// nothing here has been run against the source. What a real request and a real
/// answer do is unmeasured, and #38 is where a server is booted at all.
///
/// The bodies come from <see cref="TmdbFixtures"/> and were written by hand
/// rather than captured, which is #48's position and the reason no provenance
/// line is owed on any of them.
/// </remarks>
public class TmdbSourceAdapterTests
{
    /// <summary>
    /// The instant the clock these adapters are given reads.
    /// </summary>
    /// <remarks>
    /// Fixed rather than read from the machine, so a record the adapter stamps
    /// carries a value a test can name. It never advances: nothing here needs
    /// time to pass, and a clock that moved between two reads would make the
    /// stamp on one answer's titles a thing to compare rather than a thing to
    /// assert.
    /// </remarks>
    private static readonly DateTimeOffset _fetched = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The three entries of <c>PageWhoseAdultFlagIsFourThings</c> that survive.
    /// </summary>
    private static readonly string[] _theThreeTheSourceDidNotFlag =
    {
        "A Film The Source Did Not Flag",
        "A Film Whose Flag Arrived As Text",
        "A Film The Source Said Nothing About"
    };

    /// <summary>
    /// The adapter says which body it speaks for.
    /// </summary>
    /// <remarks>
    /// Named rather than inferred, because a response commonly carries
    /// identifiers from bodies other than the one that answered, and what a
    /// caller needs this for is the key, the terms and the rate budget, all of
    /// which belong to whoever was asked.
    /// </remarks>
    [Fact]
    public void TheAdapterSpeaksForTheSourceItIsNamedAfter()
    {
        Assert.Equal(MetadataSource.Tmdb, Asked(TmdbFixtures.EmptyPage).Source);
    }

    /// <summary>
    /// Every field a film's page carries reaches the record.
    /// </summary>
    /// <remarks>
    /// The mapping this issue exists for, read off a fixture rather than
    /// asserted field by field against a hand-built object. A wrong field name
    /// here does not fail loudly: the value comes back null and the title is
    /// drawn without it, so a test that only asked whether a title arrived
    /// would stay green through the whole class of defect.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AFilmCarriesEveryFieldTheSourceGaveIt()
    {
        var answer = await Asked(TmdbFixtures.MoviePage)
            .FetchAsync(new SourceQuery("trending", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.Equal(2, answer.Titles.Count);
        Assert.Equal(2, answer.TotalCount);

        var film = answer.Titles[0];

        Assert.Equal(DiscoverTitleKind.Movie, film.Kind);
        Assert.Equal("A Film That Does Not Exist", film.Name);
        Assert.Equal("A Film That Does Not Exist, As Its Own Language Spells It", film.OriginalName);
        Assert.Equal(2019, film.ReleaseYear);
        Assert.Equal("A synthetic description standing in for the one the source returned.", film.Summary);
        Assert.Equal(
            new Uri("https://image.tmdb.org/t/p/w500/synthetic-poster-one.jpg"),
            film.ArtworkLocation);
        Assert.Equal(
            new[] { new ProviderIdentifier(MetadataSource.Tmdb, "100001") },
            film.Identity.Identifiers);
        Assert.Equal(6.4, film.VoteAverage);
        Assert.Null(film.VoteCount);
    }

    /// <summary>
    /// A title the source flagged as adult never becomes a record.
    /// </summary>
    /// <remarks>
    /// #93's first condition asks for the exclusion by default, and it asks for
    /// it at the request. No reference page for the six addresses this adapter
    /// builds documents a parameter that would carry it, which
    /// <c>docs/limits.md</c> records, so the exclusion is made on the answer
    /// and this is what it costs and what it does not cost.
    ///
    /// Three of the four entries survive, and which three is the whole of the
    /// assertion. A reader that asked whether the field was present rather than
    /// what it held would drop the first as well; one that took any
    /// truthy-looking value would drop the third, which is the word rather than
    /// the value; and one that treated an absence as a yes would drop the
    /// fourth, which is what every entry on two of the six addresses looks
    /// like.
    ///
    /// The rest of the page surviving is the second half. An exclusion that
    /// ended the page would turn one flagged title into a shelf that stops at
    /// it.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ATitleTheSourceFlaggedAsAdultNeverBecomesARecord()
    {
        var answer = await Asked(TmdbFixtures.PageWhoseAdultFlagIsFourThings)
            .FetchAsync(new SourceQuery("trending", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.Equal(
            _theThreeTheSourceDidNotFlag,
            answer.Titles.Select(title => title.Name).ToArray());
    }

    /// <summary>
    /// A score the source did not send as a score is an absence rather than a failure.
    /// </summary>
    /// <remarks>
    /// The two numbers a shelf is ordered by, which is #91, so the wrong answer
    /// here is not a field drawn oddly: it is a title in a place on the row it
    /// has no claim to. Four entries and one page, because the case that
    /// matters is a bad entry costing its own values and costing nothing else.
    ///
    /// The record refuses each of these outright, and that is the right answer
    /// for a caller that composed one and the wrong answer here. A refresh that
    /// threw on one malformed entry would lose the page it was on, which is
    /// what #79 asks a source's bad answer not to cost.
    ///
    /// The overflowing count is the one to read. Read as its low bits it is a
    /// large positive number, so the title it is on would sort above everything
    /// the source's audience actually watched.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AScoreThatIsNotAScoreIsAnAbsenceRatherThanAFailure()
    {
        var answer = await Asked(TmdbFixtures.PageWhoseScoresAreNotScores)
            .FetchAsync(new SourceQuery("trending", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.Equal(4, answer.Titles.Count);

        Assert.Equal(8.25, answer.Titles[0].VoteAverage);
        Assert.Equal(1234, answer.Titles[0].VoteCount);

        Assert.Null(answer.Titles[1].VoteAverage);
        Assert.Null(answer.Titles[1].VoteCount);

        Assert.Null(answer.Titles[2].VoteAverage);
        Assert.Null(answer.Titles[2].VoteCount);

        Assert.Equal(7.0, answer.Titles[3].VoteAverage);
        Assert.Null(answer.Titles[3].VoteCount);
    }

    /// <summary>
    /// The four absences the source spells as presence arrive as absences.
    /// </summary>
    /// <remarks>
    /// An empty description, a null artwork path, an empty release date and an
    /// original title equal to the title. #64 asks that absence be a null
    /// rather than an empty string or a zero, and each of these carried
    /// through would put something in front of a user: an empty panel, a
    /// broken picture, a year of zero, and a second name that is the first one
    /// again.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AFieldTheSourceLeftEmptyIsAnAbsenceRatherThanAValue()
    {
        var answer = await Asked(TmdbFixtures.MoviePage)
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        var film = answer.Titles[1];

        Assert.Null(film.Summary);
        Assert.Null(film.ArtworkLocation);
        Assert.Null(film.ReleaseYear);
        Assert.Null(film.OriginalName);
        Assert.Equal("Another Film That Does Not Exist", film.Name);
    }

    /// <summary>
    /// A series is read with the names the source spells a series with.
    /// </summary>
    /// <remarks>
    /// Three field names differ from a film's, and the failure that follows
    /// from reading a film's on this page is silent: every title loses its
    /// name, every title is therefore dropped, and the answer is a source that
    /// said it has nothing. That is indistinguishable from an empty shelf,
    /// which is why the kind is a fixture of its own rather than one more case
    /// on the film's.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASeriesCarriesTheFieldsTheSourceSpellsDifferently()
    {
        var answer = await Asked(TmdbFixtures.SeriesPage)
            .FetchAsync(new SourceQuery("trending", DiscoverTitleKind.Series, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        var series = Assert.Single(answer.Titles);

        Assert.Equal(DiscoverTitleKind.Series, series.Kind);
        Assert.Equal("A Series That Does Not Exist", series.Name);
        Assert.Equal("A Series That Does Not Exist, As Its Own Language Spells It", series.OriginalName);
        Assert.Equal(2021, series.ReleaseYear);
        Assert.Equal(
            new[] { new ProviderIdentifier(MetadataSource.Tmdb, "200001") },
            series.Identity.Identifiers);
    }

    /// <summary>
    /// A page the source had nothing on is an answer rather than a failure.
    /// </summary>
    /// <remarks>
    /// The distinction #79 rests on. A shelf whose source answered with nothing
    /// is legitimately empty and #63 is what a client draws for it; a shelf
    /// whose source failed keeps what it had. Collapsing the two makes the
    /// second unimplementable.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASourceWithNothingToGiveHasAnsweredRatherThanFailed()
    {
        var answer = await Asked(TmdbFixtures.EmptyPage)
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.Empty(answer.Titles);
        Assert.Equal(0, answer.TotalCount);
    }

    /// <summary>
    /// A field this plugin does not know is ignored, and an entry it cannot map is dropped.
    /// </summary>
    /// <remarks>
    /// Four entries in, one title out. The unknown field is nested so that a
    /// reader walking the object would have met it. The entry with no
    /// identifier cannot become an identity, the entry with no title would be
    /// drawn as a blank row, and the entry that is a string where the source
    /// puts an object is a response that disagrees with itself. None of the
    /// three fails the page.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AnEntryThatCannotBeMappedIsDroppedAndTheRestOfThePageSurvives()
    {
        var answer = await Asked(TmdbFixtures.PageWithAnUnknownFieldAndEntriesThatCannotBeMapped)
            .FetchAsync(new SourceQuery("top-rated", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);

        var film = Assert.Single(answer.Titles);

        Assert.Equal("A Film Carrying A Field This Plugin Does Not Know", film.Name);
        Assert.Equal(4, answer.TotalCount);
    }

    /// <summary>
    /// A title the source has not translated falls back to its original name, and its
    /// summary is left unset rather than filled with anything.
    /// </summary>
    /// <remarks>
    /// #81's second and fourth conditions. The order is the one written at the
    /// adapter's mapping: the translated name, else the original one, else the
    /// entry is dropped; the translated summary, else nothing. Both shapes a
    /// missing translation can take on the wire are on the page, an empty string
    /// and an absent field, and both end in the same record. The translated
    /// entry between them is what catches a fallback that fires on every entry.
    ///
    /// The one-line mistake this is for is the mapping dropping an entry whose
    /// translated name is empty, which is what it did before this landed: a
    /// title with no translation vanished from the shelf rather than being drawn
    /// blank, and neither is what the condition asks for.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ATitleWithNoTranslationFallsBackToItsOriginalNameAndCarriesNoSummary()
    {
        var answer = await Asked(TmdbFixtures.PageOfFilmsWithNoTranslation)
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.Equal(3, answer.Titles.Count);

        var empty = answer.Titles[0];
        Assert.Equal("A Film With No Translation", empty.Name);
        Assert.Null(empty.OriginalName);
        Assert.Null(empty.Summary);

        var translated = answer.Titles[1];
        Assert.Equal("A Film Whose Translation Arrived", translated.Name);
        Assert.Equal("A Film Whose Translation Arrived In Its Own Language", translated.OriginalName);
        Assert.Equal("A synthetic description in the language asked for.", translated.Summary);

        var absent = answer.Titles[2];
        Assert.Equal("A Film Whose Translated Name Is Absent Rather Than Empty", absent.Name);
        Assert.Null(absent.OriginalName);
        Assert.Null(absent.Summary);
    }

    /// <summary>
    /// A body that stops in the middle is a failure the caller can retry, and never an exception.
    /// </summary>
    /// <remarks>
    /// The near-miss is a parser that reads the beginning of a body and stops.
    /// The first seventy bytes of this fixture are valid, and the whole of it
    /// is not.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ABodyThatStopsInTheMiddleIsATemporaryFailure()
    {
        var answer = await Asked(TmdbFixtures.TruncatedBody)
            .FetchAsync(new SourceQuery("trending", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.TemporarilyFailed, answer.Outcome);
        Assert.Empty(answer.Titles);
    }

    /// <summary>
    /// A credential the source rejected is a source that has not been set up.
    /// </summary>
    /// <remarks>
    /// The outcome that stops a retry. Asking again with a key the source has
    /// refused spends its budget for an answer that cannot arrive, which its
    /// own terms speak to, and no backoff in #78 would ever make it work. What
    /// it costs is the source's own words about the refusal, because this
    /// outcome carries no message.
    /// </remarks>
    /// <param name="status">The status the source refused with.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task ARejectedCredentialReadsAsASourceThatIsNotSetUp(int status)
    {
        var answer = await Asked(TmdbFixtures.RefusalBody, status)
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.NotConfigured, answer.Outcome);
        Assert.Null(answer.SourceMessage);
        Assert.Null(answer.RetryAfter);
    }

    /// <summary>
    /// A refusal for rate carries the wait the source named and the words it used.
    /// </summary>
    /// <remarks>
    /// The one outcome that carries how long to wait, because it is the one
    /// where asking again too soon makes things worse rather than better. What
    /// a caller does with the wait is #78 and is not decided here.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARefusalForRateCarriesTheWaitAndTheSourcesOwnWords()
    {
        var answer = await Asked(TmdbFixtures.RateLimitBody, 429, TimeSpan.FromSeconds(17))
            .FetchAsync(new SourceQuery("trending", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.RateLimited, answer.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(17), answer.RetryAfter);
        Assert.Equal(
            "A synthetic rate refusal standing in for the words the source uses.",
            answer.SourceMessage);
    }

    /// <summary>
    /// A refusal for rate with no wait beside it is still a refusal for rate.
    /// </summary>
    /// <remarks>
    /// The source may say nothing about how long, and it says it in a form this
    /// plugin does not read whenever it states a date rather than a number of
    /// seconds. Both arrive here as no wait, which the answer type has a
    /// meaning for, rather than as a wait of zero, which a backoff would read
    /// as permission to ask again at once.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARefusalForRateWithNoWaitIsNotAWaitOfNothing()
    {
        var answer = await Asked(TmdbFixtures.RateLimitBody, 429)
            .FetchAsync(new SourceQuery("trending", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.RateLimited, answer.Outcome);
        Assert.Null(answer.RetryAfter);
    }

    /// <summary>
    /// A failure at the source carries the source's words, and a failure from something else carries none.
    /// </summary>
    /// <remarks>
    /// An operator is shown what the source said or nothing at all. The second
    /// fixture is what a server behind a gateway or a captive portal actually
    /// receives, and the first line of somebody else's markup shown as the
    /// source's message is worse than an empty field.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task OnlyTheSourcesOwnWordsAreCarriedToAnOperator()
    {
        var fromTheSource = await Asked(TmdbFixtures.RefusalBody, 500)
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.TemporarilyFailed, fromTheSource.Outcome);
        Assert.Equal(
            "A synthetic refusal standing in for the words the source uses.",
            fromTheSource.SourceMessage);

        var fromSomethingElse = await Asked(TmdbFixtures.BodyFromSomethingThatIsNotTheSource, 502)
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.TemporarilyFailed, fromSomethingElse.Outcome);
        Assert.Null(fromSomethingElse.SourceMessage);
    }

    /// <summary>
    /// A source that contradicts itself about its total is read as having said nothing about it.
    /// </summary>
    /// <remarks>
    /// The record carrying an answer refuses a total smaller than the page it
    /// describes, so passing the contradiction through would throw out of the
    /// adapter and reach a refresh as an exception rather than as one of the
    /// four outcomes. Null is what the answer type already means by "did not
    /// say".
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ATotalSmallerThanThePageIsReadAsNoTotalRatherThanThrown()
    {
        var answer = await Asked(TmdbFixtures.PageWhoseTotalContradictsIt)
            .FetchAsync(new SourceQuery("top-rated", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.Equal(2, answer.Titles.Count);
        Assert.Null(answer.TotalCount);
    }

    /// <summary>
    /// With no credential the source is not asked at all.
    /// </summary>
    /// <remarks>
    /// A request that cannot be answered still costs the source a request and
    /// this server a connection. The assertion that matters is the second one:
    /// the outcome alone would be satisfied by an adapter that asked, was
    /// refused, and read the refusal.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task WithNoCredentialNothingIsAsked()
    {
        var asked = new List<Uri>();

        var adapter = new TmdbSourceAdapter(
            (address, cancellationToken) =>
            {
                asked.Add(address);
                return Task.FromResult(new SourceTransportReply(200, TmdbFixtures.Body(TmdbFixtures.EmptyPage), null));
            },
            configured: false,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);

        var answer = await adapter
            .FetchAsync(new SourceQuery("trending", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.NotConfigured, answer.Outcome);
        Assert.Empty(asked);
    }

    /// <summary>
    /// A question this source has none of its own for is not guessed at.
    /// </summary>
    /// <remarks>
    /// Which names a shelf may ask by is #86 and is not decided, so an adapter
    /// that mapped an unknown name onto its nearest endpoint would be choosing
    /// the shelf set rather than answering it. The source is not asked, which
    /// keeps a shelf built wrongly from spending the budget the terms bound.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AQuestionThisSourceHasNoAnswerForIsNotAsked()
    {
        var asked = new List<Uri>();

        var adapter = new TmdbSourceAdapter(
            (address, cancellationToken) =>
            {
                asked.Add(address);
                return Task.FromResult(new SourceTransportReply(200, TmdbFixtures.Body(TmdbFixtures.EmptyPage), null));
            },
            configured: true,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);

        var answer = await adapter
            .FetchAsync(
                new SourceQuery("a-shelf-nobody-has-agreed-on", DiscoverTitleKind.Movie, null, null),
                CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.NotConfigured, answer.Outcome);
        Assert.Empty(asked);
    }

    /// <summary>
    /// Each question this source answers is asked at its own address.
    /// </summary>
    /// <remarks>
    /// The addresses are read back off the transport rather than off the
    /// source, so what this establishes is the shape the adapter builds and not
    /// that the source answers there. The near-miss is the pair a copy leaves
    /// behind: a series question asked at the film endpoint answers with films
    /// and every field name is then wrong, which arrives as an empty shelf.
    /// </remarks>
    /// <param name="name">The question, in the shelf vocabulary.</param>
    /// <param name="kind">Which kind of title is wanted.</param>
    /// <param name="expected">Where it has to be asked.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData("trending", DiscoverTitleKind.Movie, "https://api.themoviedb.org/3/trending/movie/week?page=1")]
    [InlineData("trending", DiscoverTitleKind.Series, "https://api.themoviedb.org/3/trending/tv/week?page=1")]
    [InlineData("popular", DiscoverTitleKind.Movie, "https://api.themoviedb.org/3/movie/popular?page=1")]
    [InlineData("popular", DiscoverTitleKind.Series, "https://api.themoviedb.org/3/tv/popular?page=1")]
    [InlineData("top-rated", DiscoverTitleKind.Movie, "https://api.themoviedb.org/3/movie/top_rated?page=1")]
    [InlineData("top-rated", DiscoverTitleKind.Series, "https://api.themoviedb.org/3/tv/top_rated?page=1")]
    public async Task EveryQuestionIsAskedAtItsOwnAddress(string name, DiscoverTitleKind kind, string expected)
    {
        var asked = await AddressOf(new SourceQuery(name, kind, null, null)).ConfigureAwait(true);

        Assert.Equal(new Uri(expected), asked);
    }

    /// <summary>
    /// A stated language is asked for at every one of the six addresses.
    /// </summary>
    /// <remarks>
    /// The whole of what a language costs is this parameter, and the whole of
    /// what its absence costs is silence: the source documents a default of
    /// <c>en-US</c> on all six, which <c>docs/source-api/tmdb.md</c> records, so
    /// a server whose metadata language is not English gets English and nothing
    /// anywhere says a language was not asked for. That is the defect #81 is
    /// written against and it cannot be seen in an answer.
    ///
    /// The near-miss is the parameter added to the address a reader tries first
    /// and not to the other five. Both trending addresses are in the table for
    /// that reason: their reference documents `language` and documents no
    /// paging, so they are the pair somebody treats as the odd ones out.
    /// </remarks>
    /// <param name="name">The question, in the shelf vocabulary.</param>
    /// <param name="kind">Which kind of title is wanted.</param>
    /// <param name="expected">Where it has to be asked.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData("trending", DiscoverTitleKind.Movie, "https://api.themoviedb.org/3/trending/movie/week?page=1&language=de-DE")]
    [InlineData("trending", DiscoverTitleKind.Series, "https://api.themoviedb.org/3/trending/tv/week?page=1&language=de-DE")]
    [InlineData("popular", DiscoverTitleKind.Movie, "https://api.themoviedb.org/3/movie/popular?page=1&language=de-DE")]
    [InlineData("popular", DiscoverTitleKind.Series, "https://api.themoviedb.org/3/tv/popular?page=1&language=de-DE")]
    [InlineData("top-rated", DiscoverTitleKind.Movie, "https://api.themoviedb.org/3/movie/top_rated?page=1&language=de-DE")]
    [InlineData("top-rated", DiscoverTitleKind.Series, "https://api.themoviedb.org/3/tv/top_rated?page=1&language=de-DE")]
    public async Task ALanguageIsAskedForAtEveryAddress(string name, DiscoverTitleKind kind, string expected)
    {
        var asked = await AddressOf(
                new SourceQuery(name, kind, null, null),
                SourceLocale.Of("de-DE", null))
            .ConfigureAwait(true);

        Assert.Equal(new Uri(expected), asked);
    }

    /// <summary>
    /// A stated region reaches the two addresses whose reference documents one, and no others.
    /// </summary>
    /// <remarks>
    /// <c>region</c> is documented on <c>movie/popular</c> and on
    /// <c>movie/top_rated</c> and on neither trending address and neither
    /// television address, which <c>docs/source-api/tmdb.md</c> records from the
    /// six references. Sending it to the other four would be sending a
    /// parameter no reference lists, honoured or dropped for reasons no reading
    /// of those pages settles, and an operator would then have a setting that
    /// means one thing on two shelves and an unknown thing on four.
    ///
    /// The near-miss is the region appended beside the language, which is the
    /// obvious way to write it and passes on the two addresses somebody checks
    /// by hand. The four rows expecting no region are the ones that catch it.
    /// </remarks>
    /// <param name="name">The question, in the shelf vocabulary.</param>
    /// <param name="kind">Which kind of title is wanted.</param>
    /// <param name="expected">Where it has to be asked.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData("popular", DiscoverTitleKind.Movie, "https://api.themoviedb.org/3/movie/popular?page=1&language=de-DE&region=AT")]
    [InlineData("top-rated", DiscoverTitleKind.Movie, "https://api.themoviedb.org/3/movie/top_rated?page=1&language=de-DE&region=AT")]
    [InlineData("trending", DiscoverTitleKind.Movie, "https://api.themoviedb.org/3/trending/movie/week?page=1&language=de-DE")]
    [InlineData("trending", DiscoverTitleKind.Series, "https://api.themoviedb.org/3/trending/tv/week?page=1&language=de-DE")]
    [InlineData("popular", DiscoverTitleKind.Series, "https://api.themoviedb.org/3/tv/popular?page=1&language=de-DE")]
    [InlineData("top-rated", DiscoverTitleKind.Series, "https://api.themoviedb.org/3/tv/top_rated?page=1&language=de-DE")]
    public async Task ARegionReachesOnlyTheAddressesThatDocumentOne(string name, DiscoverTitleKind kind, string expected)
    {
        var asked = await AddressOf(
                new SourceQuery(name, kind, null, null),
                SourceLocale.Of("de-DE", "AT"))
            .ConfigureAwait(true);

        Assert.Equal(new Uri(expected), asked);
    }

    /// <summary>
    /// Every title an answer produces carries the language the request asked for.
    /// </summary>
    /// <remarks>
    /// Per entry rather than per document, because a language change is
    /// answered by refreshing what was fetched in the old one and a partial
    /// refresh therefore leaves two languages in one shelf on purpose. A
    /// catalogue that recorded one value for the whole of itself would be
    /// recording a value that is wrong for every entry the partial refresh did
    /// not reach, which is the silent mixture #81's third condition exists
    /// against.
    ///
    /// What is asserted is what was asked for. None of the six addresses states
    /// in its answer which language it answered in, so nothing here can assert
    /// that the source obeyed, and the field says so of itself.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task EveryTitleCarriesTheLanguageItWasAskedFor()
    {
        var answer = await AskedIn(SourceLocale.Of("de-DE", null))
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.NotEmpty(answer.Titles);
        Assert.All(answer.Titles, title => Assert.Equal("de-DE", title.Language));
    }

    /// <summary>
    /// With no language stated nothing is asked for and no title claims one.
    /// </summary>
    /// <remarks>
    /// The absence has to travel as an absence. A record stamped with whatever
    /// this plugin believes the source's default to be would be a record
    /// claiming a language was chosen, and a later refresh in a language an
    /// operator did choose could not be told from it.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task WithNoLanguageStatedNothingIsAskedForAndNoTitleClaimsOne()
    {
        var asked = await AddressOf(
                new SourceQuery("popular", DiscoverTitleKind.Movie, null, null),
                SourceLocale.Unstated)
            .ConfigureAwait(true);

        Assert.Equal(new Uri("https://api.themoviedb.org/3/movie/popular?page=1"), asked);

        var answer = await Asked(TmdbFixtures.MoviePage)
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.NotEmpty(answer.Titles);
        Assert.All(answer.Titles, title => Assert.Null(title.Language));
    }

    /// <summary>
    /// A start index is turned into the page that holds it.
    /// </summary>
    /// <remarks>
    /// The source pages in twenties and takes a page number rather than an
    /// offset. The case worth having is the one that does not divide: a start
    /// index of twenty-five is on the second page, and the five titles ahead of
    /// it on that page are dropped rather than returned as though the index had
    /// been twenty.
    /// </remarks>
    /// <param name="startIndex">How many titles to skip, or null for the beginning.</param>
    /// <param name="page">The page that holds it.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData(null, 1)]
    [InlineData(0, 1)]
    [InlineData(19, 1)]
    [InlineData(20, 2)]
    [InlineData(25, 2)]
    [InlineData(40, 3)]
    public async Task AStartIndexAsksForThePageThatHoldsIt(int? startIndex, int page)
    {
        var asked = await AddressOf(new SourceQuery("popular", DiscoverTitleKind.Movie, startIndex, null))
            .ConfigureAwait(true);

        Assert.Equal(
            new Uri(FormattableString.Invariant($"https://api.themoviedb.org/3/movie/popular?page={page}")),
            asked);
    }

    /// <summary>
    /// A start index inside a page skips what is ahead of it, and a limit stops at what was asked for.
    /// </summary>
    /// <remarks>
    /// Both are read off the page rather than asked of the source, because the
    /// source takes a page and not an offset. A start index of one on a page of
    /// two therefore leaves the second title and not the first, and a limit of
    /// one leaves the first.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task WhatIsAheadOfTheStartIndexIsDroppedAndTheLimitIsHonoured()
    {
        var skipped = await Asked(TmdbFixtures.MoviePage)
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, 1, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal("Another Film That Does Not Exist", Assert.Single(skipped.Titles).Name);

        var limited = await Asked(TmdbFixtures.MoviePage)
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, 1), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal("A Film That Does Not Exist", Assert.Single(limited.Titles).Name);
    }

    /// <summary>
    /// An artwork path shaped like anything but a path is dropped rather than put in a URL.
    /// </summary>
    /// <remarks>
    /// The one piece of a response that ends up inside a URL. It is admitted by
    /// shape rather than escaped, so a path carrying a traversal, a host, a
    /// query or a space costs a title its picture and cannot point a client
    /// anywhere but at the source's own artwork host. The near-miss is the
    /// first one: a path that reads as a file name to a person and climbs out
    /// of the size directory when a client resolves it.
    /// </remarks>
    /// <param name="path">The artwork path the source gave.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData("/../../etc/passwd")]
    [InlineData("//somewhere-else.example/poster.jpg")]
    [InlineData("/poster.jpg?followed=by-a-query")]
    [InlineData("/poster with a space.jpg")]
    [InlineData("/")]
    [InlineData("")]
    public async Task AnArtworkPathThatIsNotOneIsDropped(string path)
    {
        var body = FormattableString.Invariant(
            $"{{\"results\":[{{\"id\":1,\"title\":\"A Film With An Artwork Path Like That\",\"poster_path\":\"{path}\"}}]}}");

        var answer = await new TmdbSourceAdapter(
                (address, cancellationToken) => Task.FromResult(new SourceTransportReply(200, body, null)),
                configured: true,
                new ClockATestAdvances(_fetched),
                SourceLocale.Unstated)
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Null(Assert.Single(answer.Titles).ArtworkLocation);
    }

    /// <summary>
    /// Cancellation is a fault rather than one of the four answers.
    /// </summary>
    /// <remarks>
    /// The interface says so: a source that could not answer is an outcome, and
    /// a caller that stopped asking is not. A refresh that read cancellation as
    /// a temporary failure would report the shelf as broken every time the
    /// server shut down during one.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task CancellationIsThrownRatherThanReportedAsAFailure()
    {
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync().ConfigureAwait(true);

        var adapter = new TmdbSourceAdapter(
            (address, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new SourceTransportReply(200, null, null));
            },
            configured: true,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => adapter.FetchAsync(
                    new SourceQuery("popular", DiscoverTitleKind.Movie, null, null),
                    stopped.Token))
            .ConfigureAwait(true);
    }

    /// <summary>
    /// A connection whose certificate did not verify fails, and the source is not asked a second time.
    /// </summary>
    /// <remarks>
    /// #45's third condition. A transport that cannot verify the endpoint hands
    /// the adapter the shape the runtime produces for it, an
    /// <see cref="HttpRequestException"/> wrapping an
    /// <see cref="AuthenticationException"/>, and what is asserted is that the
    /// fetch ends there: one outcome saying the source could not answer, no
    /// titles, and exactly one question asked.
    ///
    /// The count is the assertion, not the outcome. Falling back to a
    /// connection that skips verification is a second request, so a run in
    /// which the transport was reached once is a run in which no such fallback
    /// was made. A retry added later that asks again on any failure reddens
    /// this before it reaches a server, which is the near-miss it was written
    /// against; #78 owns whatever retry does arrive and this is the bound it
    /// has to be built inside.
    ///
    /// Its reach stops at this adapter. The transport is supplied here, so what
    /// verification a real client performs is not observed by this test, and
    /// what a real endpoint presents is not a property any test in this tree
    /// holds. That nothing in the plugin turns verification off is a separate
    /// statement and is held by <c>no-machine-trust-store</c> over the tracked
    /// text rather than here.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AConnectionThatCouldNotBeVerifiedFailsAndIsNotAskedAgain()
    {
        var asked = 0;

        var adapter = new TmdbSourceAdapter(
            (address, cancellationToken) =>
            {
                asked++;

                throw new HttpRequestException(
                    "The SSL connection could not be established, see inner exception.",
                    new AuthenticationException("The remote certificate is invalid according to the validation procedure."));
            },
            configured: true,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);

        var answer = await adapter
            .FetchAsync(new SourceQuery("popular", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.TemporarilyFailed, answer.Outcome);
        Assert.Empty(answer.Titles);
        Assert.Equal(1, asked);
    }

    /// <summary>
    /// Nothing else in the plugin names this adapter.
    /// </summary>
    /// <remarks>
    /// #74's first condition, held as a property rather than as an inspection
    /// that goes stale. What it refuses is the shape that spreads: a field, a
    /// constructor parameter or a return type somewhere else in the plugin
    /// naming the concrete adapter, after which the source behind the interface
    /// is knowledge every caller has and a second source is a change to all of
    /// them.
    ///
    /// Its bound is what reflection can see. A local variable inside a method
    /// body naming the adapter is invisible here, so this catches the defect
    /// where it would last rather than everywhere it could be typed.
    /// </remarks>
    [Fact]
    public void NothingElseInThePluginNamesTheAdapter()
    {
        var adapter = typeof(TmdbSourceAdapter);
        var everywhere = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        var naming = new List<string>();

        foreach (var type in adapter.Assembly.GetTypes().Where(candidate => Elsewhere(candidate, adapter)))
        {
            foreach (var field in type.GetFields(everywhere).Where(field => field.FieldType == adapter))
            {
                naming.Add($"{type.FullName} holds {field.Name}");
            }

            foreach (var method in type.GetMethods(everywhere).Where(method => Names(method, adapter)))
            {
                naming.Add($"{type.FullName}.{method.Name} names it");
            }

            foreach (var constructor in type.GetConstructors(everywhere)
                         .Where(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == adapter)))
            {
                naming.Add($"{type.FullName} takes one in a constructor: {constructor}");
            }
        }

        Assert.Empty(naming);
    }

    /// <summary>
    /// Whether a type is somewhere else in the plugin rather than part of the adapter itself.
    /// </summary>
    /// <param name="candidate">The type found in the assembly.</param>
    /// <param name="adapter">The adapter.</param>
    /// <returns>True where a reference from it would be the adapter spreading.</returns>
    /// <remarks>
    /// What this excludes is the adapter and what the compiler wrote for it.
    /// The lambda the adapter builds its transport in becomes a class nested
    /// inside it holding a field of the adapter's type, which is the adapter
    /// referring to itself and reads as the defect this test is for. It was
    /// found by the test failing on the day it was written rather than by
    /// anybody predicting it.
    /// </remarks>
    private static bool Elsewhere(Type candidate, Type adapter)
    {
        for (var type = candidate; type is not null; type = type.DeclaringType)
        {
            if (type == adapter)
            {
                return false;
            }
        }

        return !candidate.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
    }

    /// <summary>
    /// Whether a method's signature mentions a type.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="type">The type to look for.</param>
    /// <returns>True where the return type or any parameter is that type.</returns>
    private static bool Names(MethodInfo method, Type type) =>
        method.ReturnType == type || method.GetParameters().Any(parameter => parameter.ParameterType == type);

    /// <summary>
    /// Every title in one answer carries the instant the source answered, not the instant it was asked.
    /// </summary>
    /// <remarks>
    /// The near-miss is a clock read at the top of the fetch rather than after
    /// the reply. Both compile, both stamp a plausible time, and the difference
    /// is however long the source took, which is the direction that understates
    /// a record's age against a retention ceiling. The clock here advances by an
    /// hour inside the transport, so a stamp taken before the call reads 12:00
    /// and one taken after reads 13:00, and only one of the two is asserted.
    ///
    /// The second assertion is that one answer is one instant. Reading the clock
    /// per title would give the records in a page different ages for no reason
    /// a reader could explain, and a retention pass would then expire a page in
    /// pieces.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task EveryTitleCarriesTheInstantTheSourceAnswered()
    {
        var clock = new ClockATestAdvances(_fetched);

        var adapter = new TmdbSourceAdapter(
            (address, cancellationToken) =>
            {
                clock.Advance(TimeSpan.FromHours(1));
                return Task.FromResult(new SourceTransportReply(200, TmdbFixtures.Body(TmdbFixtures.MoviePage), null));
            },
            configured: true,
            clock,
            SourceLocale.Unstated);

        var answer = await adapter
            .FetchAsync(new SourceQuery("trending", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.NotEmpty(answer.Titles);
        Assert.All(answer.Titles, title => Assert.Equal(_fetched + TimeSpan.FromHours(1), title.FetchedAt));
        Assert.Single(answer.Titles.Select(title => title.FetchedAt).Distinct());
    }

    /// <summary>
    /// This source says how long its own terms allow anything it answered with to be kept.
    /// </summary>
    /// <remarks>
    /// #68's fifth condition, asserted as the property rather than as the
    /// literal. Clause 1.C of the terms in <c>docs/sources/tmdb.md</c> caps a
    /// cache at six months, and six calendar months is not one duration: the
    /// shortest run of six consecutive months is a hundred and eighty-one days,
    /// February to July outside a leap year. So the assertion is that the
    /// ceiling is under the shortest reading of the clause and is a real
    /// duration, which a value edited upward to a comfortable number would
    /// fail. Asserting the literal back would assert that a value equals itself.
    /// </remarks>
    [Fact]
    public void ThisSourceCarriesTheCeilingItsTermsImpose()
    {
        var shortestSixMonths = TimeSpan.FromDays(181);

        var ceiling = Asked(TmdbFixtures.EmptyPage).RetentionCeiling;

        Assert.True(ceiling > TimeSpan.Zero, $"A ceiling of {ceiling} would forbid keeping anything at all.");
        Assert.True(ceiling < shortestSixMonths, $"A ceiling of {ceiling} can exceed the six months clause 1.C allows.");
    }

    /// <summary>
    /// A source answering with ten times the bound leaves the bound holding.
    /// </summary>
    /// <remarks>
    /// #58's second condition, driven along the path a shelf actually takes: the
    /// shelf composes the question, the question carries the cap, and the
    /// adapter stops reading at it. Ten times is the ratio that condition names,
    /// and the point of the ratio is that a source is under no obligation to
    /// answer with a page: nothing in the terms or the reference promises how
    /// many entries arrive, which <c>docs/source-api/tmdb.md</c> records, so an
    /// adapter that trusted the answer's length would be trusting a number the
    /// source chooses.
    /// <para>
    /// Two assertions rather than one. The count is bounded, and the entries
    /// kept are the first ones the source sent rather than an arbitrary slice,
    /// because a bound that dropped from the front would quietly hide whatever a
    /// source ranked highest.
    /// </para>
    /// <para>
    /// The body is composed here rather than added to <c>TmdbFixtures</c> as a
    /// base64 constant. Two hundred entries encoded is one line nobody can read
    /// or edit, and the reason that file encodes its bodies is that a literal on
    /// disk is normalised on the way into git. A body built in the test is never
    /// on disk, so its bytes are exact by construction and the reason for the
    /// encoding does not reach it.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task APageTenTimesTheBoundIsCutToTheBound()
    {
        var cap = CatalogueBounds.DefaultTitlesPerShelf;

        var shelf = new Shelf
        {
            DisplayName = "Trending films",
            Question = ShelfQuestion.Trending,
            Kind = DiscoverTitleKind.Movie,
            Source = MetadataSource.Tmdb,
            Cap = cap
        };

        var adapter = new TmdbSourceAdapter(
            (address, cancellationToken) =>
                Task.FromResult(new SourceTransportReply(200, PageOf(cap * 10), null)),
            configured: true,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);

        var answer = await adapter.FetchAsync(shelf.Ask(), CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.Equal(cap, answer.Titles.Count);
        Assert.Equal("A Film That Does Not Exist 1", answer.Titles[0].Name);
        Assert.Equal($"A Film That Does Not Exist {cap}", answer.Titles[cap - 1].Name);
    }

    /// <summary>
    /// A well-formed page of films with as many entries as asked for.
    /// </summary>
    /// <param name="entries">How many entries the page carries.</param>
    /// <returns>The body, as the source would have sent it.</returns>
    /// <remarks>
    /// Every entry is complete and unremarkable, so the only thing that can cut
    /// the answer short is the bound. An entry the mapping drops for a missing
    /// identifier, a missing name or an adult flag would make a short answer
    /// prove nothing.
    /// </remarks>
    private static string PageOf(int entries)
    {
        var body = new StringBuilder("{\"page\":1,\"results\":[", 256 * entries);

        for (var index = 1; index <= entries; index++)
        {
            if (index > 1)
            {
                body.Append(',');
            }

            body.Append(CultureInfo.InvariantCulture, $"{{\"adult\":false,\"id\":{900000 + index},");
            body.Append(CultureInfo.InvariantCulture, $"\"title\":\"A Film That Does Not Exist {index}\",");
            body.Append(CultureInfo.InvariantCulture, $"\"original_title\":\"A Film That Does Not Exist {index}\",");
            body.Append("\"overview\":\"A synthetic description standing in for the one the source returned.\",");
            body.Append(CultureInfo.InvariantCulture, $"\"poster_path\":\"/synthetic-poster-{index}.jpg\",");
            body.Append("\"release_date\":\"2019-07-04\",\"vote_average\":6.4}");
        }

        body.Append(CultureInfo.InvariantCulture, $"],\"total_pages\":1,\"total_results\":{entries}}}");

        return body.ToString();
    }

    /// <summary>
    /// An adapter that answers every question with one fixture.
    /// </summary>
    /// <param name="fixture">The body it answers with.</param>
    /// <param name="status">The status it answers with.</param>
    /// <param name="retryAfter">The wait the source named, if any.</param>
    /// <returns>The adapter.</returns>
    private static TmdbSourceAdapter Asked(string fixture, int status = 200, TimeSpan? retryAfter = null) =>
        new(
            (address, cancellationToken) =>
                Task.FromResult(new SourceTransportReply(status, TmdbFixtures.Body(fixture), retryAfter)),
            configured: true,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);

    /// <summary>
    /// An adapter that answers one page of films and was told which language to ask in.
    /// </summary>
    /// <param name="locale">Which language to ask in and which region to ask about.</param>
    /// <returns>The adapter.</returns>
    private static TmdbSourceAdapter AskedIn(SourceLocale locale) =>
        new(
            (address, cancellationToken) =>
                Task.FromResult(new SourceTransportReply(200, TmdbFixtures.Body(TmdbFixtures.MoviePage), null)),
            configured: true,
            new ClockATestAdvances(_fetched),
            locale);

    /// <summary>
    /// The address an adapter asks a question at.
    /// </summary>
    /// <param name="query">The question.</param>
    /// <returns>Where it was asked.</returns>
    private static Task<Uri> AddressOf(SourceQuery query) => AddressOf(query, SourceLocale.Unstated);

    /// <summary>
    /// The address an adapter asks a question at, having been told a language and a region.
    /// </summary>
    /// <param name="query">The question.</param>
    /// <param name="locale">Which language to ask in and which region to ask about.</param>
    /// <returns>Where it was asked.</returns>
    private static async Task<Uri> AddressOf(SourceQuery query, SourceLocale locale)
    {
        Uri? asked = null;

        var adapter = new TmdbSourceAdapter(
            (address, cancellationToken) =>
            {
                asked = address;
                return Task.FromResult(new SourceTransportReply(200, TmdbFixtures.Body(TmdbFixtures.EmptyPage), null));
            },
            configured: true,
            new ClockATestAdvances(_fetched),
            locale);

        await adapter.FetchAsync(query, CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(asked);

        return asked;
    }
}
