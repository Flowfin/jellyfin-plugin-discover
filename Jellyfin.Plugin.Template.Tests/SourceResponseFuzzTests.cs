using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;
using Xunit;
using Xunit.Abstractions;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What the source response reader does with bytes no test wrote.
/// </summary>
/// <remarks>
/// Everything a discover page shows arrives as a response from a third party,
/// is parsed, normalised and ends up as a record that will be written into an
/// operator's library. That is the door untrusted bytes come through, and it is
/// the input class no unit test enumerates: a fixture proves the reader handles
/// the shapes somebody thought of, and this proves it survives the ones nobody
/// did. #37 is where that is asked for.
///
/// The properties below are what the reader promises rather than what any one
/// body produces. #73 says the fetch does not throw to report anything about a
/// source, so an exception escaping is a defect whatever caused it, and the
/// records it builds are refused by their own constructors when a field is
/// wrong, which turns "the adapter mapped something impossible" into "the
/// adapter threw" and lands it in the same assertion.
///
/// What none of this covers. No request has been made to the source, so a
/// mutant is a body a connection could have produced and not one that was
/// observed. The corpus is the recorded fixtures, every one of which was
/// written by hand rather than captured, so a field a real response carries and
/// no fixture does is not reached by any mutation of them. And a campaign is a
/// search rather than a proof: passing says these bodies did not break the
/// reader, never that no body can.
/// </remarks>
public class SourceResponseFuzzTests
{
    /// <summary>
    /// Where the source keeps the artwork this plugin references and never copies.
    /// </summary>
    /// <remarks>
    /// Written out rather than read from the adapter, for the reason the
    /// surface's name is written out in <c>DiscoverSurfaceTests</c>: a value
    /// derived from the code under test agrees with it however wrong it is.
    /// A mutated path that moved a location to another host would be a request
    /// this plugin caused an operator's client to make somewhere nobody
    /// declared, which is the one thing a byte inside a URL can do that the
    /// other fields cannot.
    /// </remarks>
    private const string ArtworkHost = "image.tmdb.org";

    /// <summary>
    /// How far apart the second change of a pair is taken.
    /// </summary>
    /// <remarks>
    /// A stride and not a draw. Walking the pair space in reading order spends
    /// the whole of a small budget on one value of the second change, because
    /// the first change's space is tens of thousands wide and the budget is
    /// smaller than it; a step of this size crosses the second axis on every
    /// body instead. It is coprime with every count it is taken against for the
    /// same reason a stride ever is, so the walk does not settle into a short
    /// cycle. Nothing here is unpredictable: the same budget walks the same
    /// bodies on every machine, which is what makes a failure reproducible from
    /// the numbers printed beside it.
    /// </remarks>
    private const long Stride = 7_919;

    /// <summary>
    /// The instant the clock every adapter here is given reads.
    /// </summary>
    /// <remarks>
    /// Fixed, so that the stamp on every record a campaign produces is a value
    /// the assertion below can name rather than compare. A clock that moved
    /// under a run of eighty thousand bodies would make the one property that
    /// says where a record's time came from unassertable.
    /// </remarks>
    private static readonly DateTimeOffset _fetched = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The statuses a mutant is read beside, other than an answer.
    /// </summary>
    /// <remarks>
    /// Three, because the reader takes a different route through the body for
    /// each: a credential the source rejected, a refusal for rate, and a
    /// failure it may recover from. Two of the three read the body for the
    /// source's own words, which is a second parse of the same untrusted bytes
    /// and is where a body that is not the source's answers instead.
    /// </remarks>
    private static readonly int[] _refusals = { 401, 429, 500 };

    /// <summary>
    /// The questions a mutant is read against.
    /// </summary>
    /// <remarks>
    /// Rotated across the campaign rather than run in full against every
    /// mutant, which would cost four times the run for a fourth of the reach.
    /// The four differ in what the reader does after the parse: the kind
    /// decides which field names a title is read out of, and a start index
    /// inside a page and a limit are the two that decide how much of the parsed
    /// page survives.
    /// </remarks>
    private static readonly SourceQuery[] _questions =
    {
        new SourceQuery("trending", DiscoverTitleKind.Movie, null, null),
        new SourceQuery("popular", DiscoverTitleKind.Series, null, null),
        new SourceQuery("top-rated", DiscoverTitleKind.Movie, 1, 1),
        new SourceQuery("trending", DiscoverTitleKind.Series, 21, 5)
    };

    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceResponseFuzzTests"/> class.
    /// </summary>
    /// <param name="output">Where the size of each campaign is written.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> is null.</exception>
    public SourceResponseFuzzTests(ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;
    }

    /// <summary>
    /// Names every recorded fixture, so a campaign runs per seed and a failure names one.
    /// </summary>
    /// <returns>One row per body in the corpus.</returns>
    public static TheoryData<string> Corpus()
    {
        var names = new TheoryData<string>();

        foreach (var seed in SourceResponseMutations.Corpus)
        {
            names.Add(seed.Name);
        }

        return names;
    }

    /// <summary>
    /// Every single-byte change to a recorded response leaves the reader inside what it promises.
    /// </summary>
    /// <param name="name">Which recorded body is being mutated.</param>
    /// <returns>A <see cref="Task"/> that completes when the campaign has run.</returns>
    /// <remarks>
    /// The whole depth-one space, enumerated rather than sampled, so this leg
    /// says the same thing on every run and a failure carries the index that
    /// reproduces it. Each mutant is read twice: once beside an answer, which
    /// is the route to the mapping and to a record, and once beside a refusal,
    /// which is the route to the source's own words.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Corpus))]
    public async Task NoSingleByteChangeToARecordedResponseBreaksTheReader(string name)
    {
        var seed = Seed(name);
        var mutants = seed.Count;

        for (long index = 0; index < mutants; index++)
        {
            var body = seed.Mutant(index);
            var question = _questions[(int)(index % _questions.Length)];

            await Holds(seed.Describe(index, 200), 200, body, question).ConfigureAwait(true);

            var refusal = _refusals[(int)(index % _refusals.Length)];

            await Holds(seed.Describe(index, refusal), refusal, body, question).ConfigureAwait(true);
        }

        _output.WriteLine(FormattableString.Invariant(
            $"{name}: {mutants} mutants, each read beside an answer and beside a refusal."));
    }

    /// <summary>
    /// Two changes at once leave the reader inside what it promises, for as far as the budget reaches.
    /// </summary>
    /// <param name="name">Which recorded body is being mutated.</param>
    /// <returns>A <see cref="Task"/> that completes when the campaign has run.</returns>
    /// <remarks>
    /// A walk rather than an enumeration, and the difference is stated because
    /// it is the whole bound on this leg. The depth-two space of one fixture is
    /// the square of its depth-one space, which is tens of millions of bodies
    /// for the smallest seed here, so no run covers it and a run that passed
    /// says only that the window it walked held nothing.
    ///
    /// The window is a budget rather than a draw, so the same budget walks the
    /// same bodies on every machine and a failure is reproducible from the
    /// fixture and the number beside it. What varies is how far it goes:
    /// `DISCOVER_FUZZ_DEPTH2_BUDGET` raises it, and the scheduled campaign in
    /// `.github/workflows/fuzz-the-source-reader.yml` is what sets it high
    /// enough to be worth the wall clock. The default is small on purpose,
    /// because this leg runs on every pull request and a suite nobody will wait
    /// for is a suite that gets a filter put on it.
    ///
    /// It is never zero and there is no way to make it zero. A campaign that
    /// can be turned off from the environment is a green leg that asked
    /// nothing, and the size of the one that ran is printed rather than assumed.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Corpus))]
    public async Task NoPairOfChangesInsideTheBudgetBreaksTheReader(string name)
    {
        var seed = Seed(name);
        var budget = Budget();

        for (long step = 0; step < budget; step++)
        {
            var first = step % seed.Count;
            var once = SourceResponseMutations.Mutant(seed.Body, first);

            if (once.Length == 0)
            {
                continue;
            }

            var second = (step * Stride) % SourceResponseMutations.Count(once);
            var body = SourceResponseMutations.Text(SourceResponseMutations.Mutant(once, second));
            var question = _questions[(int)(step % _questions.Length)];
            var refusal = _refusals[(int)(step % _refusals.Length)];

            await Holds(Pair(seed, first, second, 200), 200, body, question).ConfigureAwait(true);
            await Holds(Pair(seed, first, second, refusal), refusal, body, question).ConfigureAwait(true);
        }

        _output.WriteLine(FormattableString.Invariant(
            $"{name}: {budget} of the depth-two space walked, which is a window and not the space."));
    }

    /// <summary>
    /// Reads how far the depth-two walk goes.
    /// </summary>
    /// <returns>How many pairs to try per seed.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the environment names a budget that is not a positive
    /// number. A campaign asked for nothing, or asked in words it cannot read,
    /// fails rather than falling back to the default, because a scheduled run
    /// that quietly ran the pull-request budget would be reported as the deep
    /// one.
    /// </exception>
    private static long Budget()
    {
        var asked = Environment.GetEnvironmentVariable("DISCOVER_FUZZ_DEPTH2_BUDGET");

        if (string.IsNullOrWhiteSpace(asked))
        {
            return 2_000;
        }

        if (!long.TryParse(asked, NumberStyles.None, CultureInfo.InvariantCulture, out var budget) || budget <= 0)
        {
            throw new InvalidOperationException(
                FormattableString.Invariant(
                    $"DISCOVER_FUZZ_DEPTH2_BUDGET is '{asked}', which is not a positive number of bodies to try."));
        }

        return budget;
    }

    /// <summary>
    /// Says which pair of changes failed, in the numbers that reproduce them.
    /// </summary>
    /// <param name="seed">Which body was mutated.</param>
    /// <param name="first">The first change.</param>
    /// <param name="second">The second, applied to the result of the first.</param>
    /// <param name="status">The status the pair was read beside.</param>
    /// <returns>A description a reader can run again.</returns>
    private static string Pair(SourceResponseSeed seed, long first, long second, int status) =>
        FormattableString.Invariant($"{seed.Describe(first, status)}, then mutation {second} of that");

    /// <summary>
    /// Finds the seed a theory row names.
    /// </summary>
    /// <param name="name">The constant the body is declared as.</param>
    /// <returns>The seed.</returns>
    private static SourceResponseSeed Seed(string name) =>
        SourceResponseMutations.Corpus.Single(seed => string.Equals(seed.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Asks the reader one mutated body and holds it to everything it promises.
    /// </summary>
    /// <param name="where">Which body and which mutation, for the failure message.</param>
    /// <param name="status">The status the body arrives beside.</param>
    /// <param name="body">The mutated body.</param>
    /// <param name="question">What is being asked.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    private static async Task Holds(
        string where,
        int status,
        string body,
        SourceQuery question)
    {
        var adapter = new TmdbSourceAdapter(
            (_, _) => Task.FromResult(new SourceTransportReply(status, body, null)),
            configured: true,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);

        SourceAnswer? given = null;

        var thrown = await Record
            .ExceptionAsync(async () => given = await adapter.FetchAsync(question, CancellationToken.None)
                .ConfigureAwait(false))
            .ConfigureAwait(true);

        Assert.True(
            thrown is null,
            FormattableString.Invariant($"{where}: the reader threw {thrown?.GetType().FullName}: {thrown?.Message}"));

        var answer = given!;

        Assert.True(
            answer.Outcome is SourceOutcome.Answered
                or SourceOutcome.NotConfigured
                or SourceOutcome.RateLimited
                or SourceOutcome.TemporarilyFailed,
            where);

        if (answer.Outcome != SourceOutcome.Answered)
        {
            Assert.True(answer.Titles.Count == 0, where);
            Assert.True(answer.TotalCount is null, where);
        }

        if (answer.Outcome != SourceOutcome.RateLimited)
        {
            Assert.True(answer.RetryAfter is null, where);
        }

        Assert.True(answer.RetryAfter is null or { Ticks: >= 0 }, where);
        Assert.True(answer.SourceMessage is null || answer.SourceMessage.Trim().Length > 0, where);
        Assert.True(answer.TotalCount is null || answer.TotalCount >= answer.Titles.Count, where);
        Assert.True(question.Limit is not { } limit || answer.Titles.Count <= limit, where);

        foreach (var title in answer.Titles)
        {
            Holds(title, question, where);
        }
    }

    /// <summary>
    /// Holds one record a mutated body produced to what the record says about itself.
    /// </summary>
    /// <param name="title">What the reader built.</param>
    /// <param name="question">What was asked, which is where the kind comes from.</param>
    /// <param name="where">Which body and which mutation, for the failure message.</param>
    /// <remarks>
    /// Two of these the record already refuses in its own constructor, and they
    /// are asserted anyway rather than left to it. What the constructor refuses
    /// arrives here as an exception out of the fetch, which #73 forbids, so the
    /// two assertions say the same thing from the other side and one of them
    /// survives a later record that stops refusing.
    ///
    /// The artwork location is the one the record does not hold. It refuses a
    /// relative location and says nothing about which host an absolute one
    /// names, and a path is the only piece of a response that ends up inside a
    /// URL.
    /// </remarks>
    private static void Holds(DiscoverTitle title, SourceQuery question, string where)
    {
        Assert.True(title.Kind == question.Kind, where);
        Assert.True(!string.IsNullOrWhiteSpace(title.Name), where);
        Assert.True(title.FetchedAt == _fetched, where);
        Assert.True(title.SchemaVersion == DiscoverTitle.CurrentSchemaVersion, where);
        Assert.True(title.Identity.Identifiers.Count > 0, where);
        Assert.True(
            !string.Equals(title.OriginalName, title.Name, StringComparison.Ordinal),
            where);
        Assert.True(title.ReleaseYear is null or > 0, where);
        Assert.True(title.Summary is null || title.Summary.Trim().Length > 0, where);
        Assert.True(
            title.ArtworkLocation is null
                || (title.ArtworkLocation.IsAbsoluteUri
                    && title.ArtworkLocation.Scheme == Uri.UriSchemeHttps
                    && string.Equals(title.ArtworkLocation.Host, ArtworkHost, StringComparison.Ordinal)),
            where);
    }
}
