using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Time;

namespace Jellyfin.Plugin.Template.Sources;

/// <summary>
/// The adapter in front of TMDB, and the only thing in this plugin that knows how TMDB answers.
/// </summary>
/// <remarks>
/// The first source, chosen because it is the one the server already speaks to
/// for its own metadata, so the identifiers this plugin carries are the ones an
/// operator's library already holds. That is what #89 compares against and what
/// #94 hands over.
///
/// It speaks to the source directly rather than through a client library, and
/// that is the fourth condition on #74 answered rather than defaulted: the
/// mapping below is four field names per kind, a client library would add a
/// dependency and its transitive graph to the one process in this plugin that
/// holds a credential, and #16 is where that trade is argued. The cost is that
/// a field the source renames is found by a test here rather than by a package
/// update.
///
/// What it does not own, and must not learn to. The credential is #77 and
/// arrives as an argument. Whose key it is was question 4 on #2 and was
/// answered on 2026-08-24: the operator supplies theirs. That answer changes
/// nothing here, because where a key is stored and how it is entered is still
/// not this type's business, and #77 is where the rest of it lands. The limits
/// are #78: this reports a refusal for rate and never decides when to ask
/// again. The terms are `docs/sources/tmdb.md`, and the rows there that name
/// #74 are the two below.
///
/// Two of those rows land here. The request identifies the application, which
/// clause 1.C requires, through a user agent naming this assembly. And artwork
/// is a location at the source rather than a copy, which is #62, so nothing
/// here fetches an image.
///
/// The file's name is load-bearing nowhere, and it was twice until #387 wrote the
/// lint's exception as this file's path and #394 took the last reader of the name
/// out of `source-terms`. That check finds this file by the interface it
/// implements and reads which body it speaks for out of the `Source` property
/// below, so a rename fails it by leaving `no-network-outside-source-adapter`'s
/// exception naming nothing rather than by changing what page is owed. What
/// decides that a terms page is owed at all is the interface, which no rename
/// touches.
/// </remarks>
public sealed class TmdbSourceAdapter : IMetadataSource
{
    /// <summary>
    /// How many titles one page of this source's answers holds.
    /// </summary>
    /// <remarks>
    /// The source pages in twenties and takes a page number rather than an
    /// offset, so a query's start index has to be turned into one. It is stated
    /// here rather than read from a response because the number is needed to
    /// build the request that would carry it. A source that changed it would be
    /// found by the start index landing on the wrong title rather than by an
    /// error, which is why the remainder is dropped from the page rather than
    /// assumed to be zero.
    /// </remarks>
    private const int PageSize = 20;

    private static readonly Uri _baseAddress = new("https://api.themoviedb.org/3/", UriKind.Absolute);

    private static readonly Uri _artworkBase = new("https://image.tmdb.org/t/p/w500/", UriKind.Absolute);

    private readonly Func<Uri, CancellationToken, Task<SourceTransportReply>> _transport;

    private readonly IClock _clock;

    private readonly bool _configured;

    private readonly SourceLocale _locale;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbSourceAdapter"/> class, speaking to the source.
    /// </summary>
    /// <param name="httpClientFactory">Where the client comes from, so the server owns its lifetime and its handler.</param>
    /// <param name="accessToken">
    /// The credential to present, or null where none has been supplied. Whose
    /// key it is and where it was stored is #77 and is not this type's
    /// business; all it does with the absence is decline to ask.
    /// </param>
    /// <param name="clock">What the instant on each record is read from.</param>
    /// <param name="locale">
    /// Which language to ask in and which region to ask about. Where it comes
    /// from is #81 and is not this type's business either, in the same way and
    /// for the same reason as the credential above.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/>, <paramref name="clock"/> or <paramref name="locale"/> is null.</exception>
    public TmdbSourceAdapter(IHttpClientFactory httpClientFactory, string? accessToken, IClock clock, SourceLocale locale)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(locale);

        _clock = clock;
        _locale = locale;
        _configured = !string.IsNullOrWhiteSpace(accessToken);
        _transport = (address, cancellationToken) => SendAsync(httpClientFactory, accessToken, address, cancellationToken);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbSourceAdapter"/> class over a transport it was given.
    /// </summary>
    /// <param name="transport">What carries a request and brings a reply back.</param>
    /// <param name="configured">Whether a credential has been supplied.</param>
    /// <param name="clock">What the instant on each record is read from.</param>
    /// <param name="locale">Which language to ask in and which region to ask about.</param>
    /// <remarks>
    /// This is the constructor a test uses, and the reason it exists is on
    /// <see cref="SourceTransportReply"/>. It is public rather than hidden
    /// because a test cannot name the type it would need to fake the other
    /// constructor's transport without breaking the rule that keeps outbound
    /// calls in adapters.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transport"/>, <paramref name="clock"/> or <paramref name="locale"/> is null.</exception>
    public TmdbSourceAdapter(Func<Uri, CancellationToken, Task<SourceTransportReply>> transport, bool configured, IClock clock, SourceLocale locale)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(locale);

        _transport = transport;
        _clock = clock;
        _locale = locale;
        _configured = configured;
    }

    /// <inheritdoc/>
    public MetadataSource Source => MetadataSource.Tmdb;

    /// <inheritdoc/>
    /// <remarks>
    /// Clause 1.C of the terms this adapter was written against prohibits
    /// caching anything obtained from the API for longer than six months, and
    /// the row for it is in <c>docs/sources/tmdb.md</c>.
    ///
    /// A hundred and eighty days rather than six months, because six calendar
    /// months is not one duration. The shortest run of six consecutive months
    /// is a hundred and eighty-one days, February to July in a year that is not
    /// a leap year, so a hundred and eighty is under every reading of the
    /// clause and needs no argument about which months a retention happened to
    /// span. The four days that costs are freshness, which is the direction an
    /// error here should point.
    /// </remarks>
    public TimeSpan RetentionCeiling => TimeSpan.FromDays(180);

    /// <inheritdoc/>
    public async Task<SourceAnswer> FetchAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        var asked = query.Validated();

        if (!_configured)
        {
            return SourceAnswer.NotConfigured();
        }

        var page = Page(asked.StartIndex);
        var address = Address(asked, page, _locale);

        if (address is null)
        {
            return SourceAnswer.NotConfigured();
        }

        SourceTransportReply reply;

        try
        {
            reply = await _transport(address, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException failure)
        {
            return SourceAnswer.TemporarilyFailed(failure.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SourceAnswer.TemporarilyFailed(null);
        }

        return Read(reply, asked, page, _clock.UtcNow, _locale.Language);
    }

    /// <summary>
    /// Turns a reply into one of the four answers.
    /// </summary>
    /// <param name="reply">What came back.</param>
    /// <param name="query">What was asked, for the kind and how much of the page to keep.</param>
    /// <param name="page">Which page was asked for, so the start index inside it can be dropped.</param>
    /// <param name="fetchedAt">When the source answered, read once for the whole reply.</param>
    /// <param name="language">
    /// The language the request asked for, carried onto every record this reply
    /// produces, or null where it asked for none.
    /// </param>
    /// <returns>The answer, never an exception.</returns>
    /// <remarks>
    /// A credential the source rejected is <see cref="SourceOutcome.NotConfigured"/>
    /// rather than a failure, and that is a choice with a cost. Asking again
    /// with the same rejected key spends the source's budget for an answer that
    /// cannot arrive, which clause 1.C of its terms speaks to directly, so the
    /// outcome that stops a retry is the right one. What it loses is the
    /// source's own words about the refusal, because that outcome carries no
    /// message: an operator is told the source is not set up and not which of
    /// absent, wrong or revoked its key is. #92 is where an operator is shown
    /// the state of a shelf, and this is the case it will find thinnest.
    /// </remarks>
    private static SourceAnswer Read(SourceTransportReply reply, SourceQuery query, int page, DateTimeOffset fetchedAt, string? language)
    {
        var status = (HttpStatusCode)reply.StatusCode;

        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return SourceAnswer.NotConfigured();
        }

        if (status == HttpStatusCode.TooManyRequests)
        {
            return SourceAnswer.RateLimited(reply.RetryAfter, Said(reply.Body));
        }

        if (reply.StatusCode is < 200 or > 299)
        {
            return SourceAnswer.TemporarilyFailed(Said(reply.Body));
        }

        if (string.IsNullOrWhiteSpace(reply.Body))
        {
            return SourceAnswer.TemporarilyFailed(null);
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(reply.Body);
        }
        catch (JsonException malformed)
        {
            return SourceAnswer.TemporarilyFailed(malformed.Message);
        }

        using (document)
        {
            return Titles(document.RootElement, query, page, fetchedAt, language);
        }
    }

    /// <summary>
    /// Reads the titles out of a page the source answered with.
    /// </summary>
    /// <param name="root">The parsed body.</param>
    /// <param name="query">What was asked, for the kind and the limit.</param>
    /// <param name="page">Which page this is, so the start index inside it can be dropped.</param>
    /// <param name="fetchedAt">When the source answered, carried onto every record this page produces.</param>
    /// <param name="language">The language the request asked for, carried onto every record this page produces.</param>
    /// <returns>What the page held, with everything unmappable left out.</returns>
    /// <remarks>
    /// An entry this plugin cannot map is dropped rather than carried, and the
    /// set comes back shorter. What cannot be mapped is an entry with no
    /// identifier, since #64 refuses an identity with nothing in it, and an
    /// entry with no name, since a row a client draws as blank is one a user
    /// cannot tell from a broken one. A field this plugin does not know is
    /// ignored by not being asked for.
    ///
    /// The total the source reports is carried unless it contradicts the page
    /// it describes, in which case it is dropped to "did not say". Passing a
    /// contradiction on would throw out of the adapter, and #73 asks that this
    /// method not throw to report anything about a source.
    /// </remarks>
    private static SourceAnswer Titles(JsonElement root, SourceQuery query, int page, DateTimeOffset fetchedAt, string? language)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            return SourceAnswer.TemporarilyFailed(null);
        }

        var skip = query.StartIndex is { } start ? start - ((page - 1) * PageSize) : 0;
        var titles = new List<DiscoverTitle>();
        var seen = 0;

        foreach (var entry in results.EnumerateArray())
        {
            seen++;

            if (seen <= skip)
            {
                continue;
            }

            if (TheSourceFlagsThisAsAdult(entry))
            {
                continue;
            }

            if (query.Limit is { } limit && titles.Count >= limit)
            {
                break;
            }

            var title = Title(entry, query.Kind, fetchedAt, language);

            if (title is not null)
            {
                titles.Add(title);
            }
        }

        int? total = null;

        if (root.TryGetProperty("total_results", out var reported)
            && reported.ValueKind == JsonValueKind.Number
            && reported.TryGetInt32(out var count)
            && count >= titles.Count)
        {
            total = count;
        }

        return SourceAnswer.Answered(titles, total);
    }

    /// <summary>
    /// Says whether the source has flagged this entry as adult.
    /// </summary>
    /// <param name="entry">One title as the source spells it.</param>
    /// <returns><see langword="true"/> where the source said so, and false otherwise.</returns>
    /// <remarks>
    /// #93's first condition asks that adult content be excluded by default. It
    /// asks for that at the request, and no reference page for the six
    /// addresses this adapter builds documents a parameter that would carry it,
    /// which `docs/limits.md` records. So the exclusion is made on the answer,
    /// and this is where it is made: before the entry becomes a record, so
    /// nothing a caller could store or draw was ever built from it.
    ///
    /// It is a separate step from the mapping rather than a null out of it. An
    /// entry this plugin could not read and an entry it refused are two
    /// different events, and folding the second into the first would leave a
    /// deliberate exclusion looking like a parse that went wrong.
    ///
    /// Only <see langword="true"/> excludes. A missing flag, a null, a string
    /// and a number are all the source not having said, and this treats them as
    /// what they are rather than as a yes. That reading is not cautious, and it
    /// is the honest one for what is being read: two of the six addresses this
    /// adapter builds document no such flag at all, so treating an absence as a
    /// yes would empty both of them entirely rather than exclude anything. What
    /// those two shelves do instead is #93's own open half and belongs with the
    /// shelves that ship, which is #86.
    ///
    /// There is no way to turn this off. #93 asks for one and it has nowhere to
    /// live: the configuration page carries no controls, which is #103.
    /// </remarks>
    private static bool TheSourceFlagsThisAsAdult(JsonElement entry) =>
        entry.ValueKind == JsonValueKind.Object
        && entry.TryGetProperty("adult", out var flag)
        && flag.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Maps one entry of a page onto this plugin's record.
    /// </summary>
    /// <param name="entry">One title as the source spells it.</param>
    /// <param name="kind">Which kind was asked for, which is what decides the field names.</param>
    /// <param name="fetchedAt">When the source answered.</param>
    /// <param name="language">The language the request asked for, or null where it asked for none.</param>
    /// <returns>The record, or null where the entry carries too little to be one.</returns>
    /// <remarks>
    /// The kind comes from the question rather than from the entry. The source
    /// answers a question about films with films and a question about series
    /// with series, and its entries carry no type of their own on these routes,
    /// so reading one back out would be reading a field that is not there.
    ///
    /// The language comes from the question as well, and for a harder reason:
    /// none of the six addresses this adapter builds states in its answer which
    /// language it answered in. So what a record can carry is what was asked
    /// for, which is what <see cref="DiscoverTitle.Language"/> says of itself.
    /// </remarks>
    private static DiscoverTitle? Title(JsonElement entry, DiscoverTitleKind kind, DateTimeOffset fetchedAt, string? language)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var isSeries = kind == DiscoverTitleKind.Series;

        var name = Text(entry, isSeries ? "name" : "title");

        if (name is null || !entry.TryGetProperty("id", out var identifier)
            || identifier.ValueKind != JsonValueKind.Number
            || !identifier.TryGetInt32(out var number))
        {
            return null;
        }

        var original = Text(entry, isSeries ? "original_name" : "original_title");

        return new DiscoverTitle
        {
            Identity = new DiscoverTitleIdentity(
                new[]
                {
                    new ProviderIdentifier(MetadataSource.Tmdb, number.ToString(CultureInfo.InvariantCulture))
                }),
            Kind = kind,
            Name = name,
            FetchedAt = fetchedAt,
            Language = language,
            OriginalName = string.Equals(original, name, StringComparison.Ordinal) ? null : original,
            ReleaseYear = Year(Text(entry, isSeries ? "first_air_date" : "release_date")),
            Summary = Text(entry, "overview"),
            ArtworkLocation = Artwork(Text(entry, "poster_path")),
            VoteAverage = Score(entry, "vote_average"),
            VoteCount = Count(entry, "vote_count")
        };
    }

    /// <summary>
    /// Reads a score the source may have left out or sent as something that is
    /// not a score.
    /// </summary>
    /// <param name="entry">The entry to read from.</param>
    /// <param name="field">The field name.</param>
    /// <returns>The score, or null where there was no usable one.</returns>
    /// <remarks>
    /// The record refuses a negative, a NaN and an infinity, and refusing is
    /// the right answer for a caller that composed one. It is the wrong answer
    /// here: what arrives is a third party's bytes, and a refresh that throws
    /// on one malformed number in one entry loses the whole page for it. So the
    /// unusable value becomes an absence, which is what #79 asks a source's bad
    /// answer to cost, and the record's guard stands behind this for callers
    /// that are not reading a wire.
    /// </remarks>
    private static double? Score(JsonElement entry, string field)
    {
        if (!entry.TryGetProperty(field, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var score)
            || double.IsNaN(score)
            || double.IsInfinity(score)
            || score < 0)
        {
            return null;
        }

        return score;
    }

    /// <summary>
    /// Reads a count of scores the source may have left out or sent as
    /// something that is not a count.
    /// </summary>
    /// <param name="entry">The entry to read from.</param>
    /// <param name="field">The field name.</param>
    /// <returns>The count, or null where there was no usable one.</returns>
    /// <remarks>
    /// Absence rather than a throw, for the reason on <see cref="Score"/>. A
    /// count larger than an <see cref="int"/> holds is an absence as well: no
    /// number that large is a count of scores, and reading the low bits of one
    /// would put a title at the top of a shelf.
    /// </remarks>
    private static int? Count(JsonElement entry, string field)
    {
        if (!entry.TryGetProperty(field, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var count)
            || count < 0)
        {
            return null;
        }

        return count;
    }

    /// <summary>
    /// Reads a field the source may have left out, left null or left blank.
    /// </summary>
    /// <param name="entry">The entry to read from.</param>
    /// <param name="field">The field name.</param>
    /// <returns>What it held, or null for all three of those.</returns>
    /// <remarks>
    /// The three collapse to one absence on purpose. #64 asks that absence be a
    /// null rather than an empty string, and a source that sends an empty
    /// description has told a client nothing to draw, whichever of the three
    /// spellings it used to say so.
    /// </remarks>
    private static string? Text(JsonElement entry, string field) =>
        entry.TryGetProperty(field, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    /// <summary>
    /// Takes the year out of the date the source gives a title.
    /// </summary>
    /// <param name="date">The date as the source spells it, or null.</param>
    /// <returns>The year, or null where there was no readable one.</returns>
    /// <remarks>
    /// A year rather than a date, which is #64's field. The source spells a
    /// release as a full date and spells a title that has been announced and
    /// not dated as an empty string, and the second is exactly the sort of
    /// title a discover page exists to show, so an unreadable date is an
    /// absence rather than a fault.
    /// </remarks>
    private static int? Year(string? date)
    {
        if (date is null || date.Length < 4)
        {
            return null;
        }

        return int.TryParse(
            date.AsSpan(0, 4),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var year)
            ? year
            : null;
    }

    /// <summary>
    /// Turns the source's artwork path into the location a client fetches from.
    /// </summary>
    /// <param name="path">The path the source gave, or null.</param>
    /// <returns>Where the artwork is, or null where the source gave nothing usable.</returns>
    /// <remarks>
    /// A location and never a copy, which is #62 and clause 1.C's bar on using
    /// the source for image hosting. Nothing here fetches it; what does is
    /// whatever draws the item, from the source's own host, and #116 is where
    /// that is disclosed to an operator.
    ///
    /// The path is the one piece of a response that ends up inside a URL, so it
    /// is admitted by shape rather than escaped. Anything but a leading slash
    /// followed by unreserved characters is dropped as absent, which costs a
    /// title its picture and cannot cost it a request somewhere else. A size is
    /// chosen here rather than read from the source's configuration endpoint,
    /// which would be a second request per refresh; a client wanting another
    /// size is #55's problem and not a reason to make that call.
    /// </remarks>
    private static Uri? Artwork(string? path)
    {
        if (path is null || path.Length < 2 || path[0] != '/')
        {
            return null;
        }

        for (var index = 1; index < path.Length; index++)
        {
            var character = path[index];

            var admitted = char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_';

            if (!admitted)
            {
                return null;
            }
        }

        return new Uri(_artworkBase, path.AsSpan(1).ToString());
    }

    /// <summary>
    /// Turns a start index into the page that holds it.
    /// </summary>
    /// <param name="startIndex">How many titles to skip, or null for the beginning.</param>
    /// <returns>The page number the source is asked for, counting from one.</returns>
    private static int Page(int? startIndex) =>
        startIndex is { } start ? (start / PageSize) + 1 : 1;

    /// <summary>
    /// Builds the address one question is asked at.
    /// </summary>
    /// <param name="query">What is being asked for.</param>
    /// <param name="page">Which page of it.</param>
    /// <param name="locale">Which language to ask in and which region to ask about.</param>
    /// <returns>The address, or null where this source has no question for that name.</returns>
    /// <remarks>
    /// Nothing a caller supplied reaches the address as text unvouched for. The
    /// path is one of six literals chosen by a switch, the page number is
    /// already an integer, and the language and the region are two values
    /// <see cref="SourceLocale"/> refuses unless every character of them is a
    /// letter or the one hyphen its shape allows, so the fifth condition on #74
    /// is a property of the shape here rather than of an escaping call somebody
    /// has to remember. That is also the spelling `source-terms` can see: it
    /// reads hostnames out of the text of a tracked adapter, and a base address
    /// assembled at run time from parts would be invisible to it.
    ///
    /// A name this source has no question for is not guessed at. Which names
    /// are legitimate is the shelf set in #86, which is not decided, so the
    /// three here are the questions this source answers directly rather than a
    /// shelf list this adapter has taken upon itself.
    ///
    /// THE REGION REACHES TWO OF THE SIX AND THE LANGUAGE REACHES ALL SIX, and
    /// that asymmetry is the source's rather than a simplification. The
    /// references read on `docs/source-api/tmdb.md` document `language` on
    /// every one of the six and `region` on `movie/popular` and
    /// `movie/top_rated` alone, so a region sent to the other four would be a
    /// parameter no reference lists, honoured or dropped for reasons no reading
    /// of those pages settles. Sending it anyway would make the setting an
    /// operator sets mean one thing on two shelves and an unknown thing on
    /// four, which is what #81 records as the reason the answer is per address.
    /// The addresses that take one are named here rather than carried as a flag
    /// beside the switch, so the two lists cannot drift apart.
    /// </remarks>
    private static Uri? Address(SourceQuery query, int page, SourceLocale locale)
    {
        var series = query.Kind == DiscoverTitleKind.Series;

        var path = query.Name switch
        {
            "trending" => series ? "trending/tv/week" : "trending/movie/week",
            "popular" => series ? "tv/popular" : "movie/popular",
            "top-rated" => series ? "tv/top_rated" : "movie/top_rated",
            _ => null
        };

        if (path is null)
        {
            return null;
        }

        var parameters = FormattableString.Invariant($"page={page}");

        if (locale.Language is { } language)
        {
            parameters += FormattableString.Invariant($"&language={language}");
        }

        if (locale.Region is { } region && path is "movie/popular" or "movie/top_rated")
        {
            parameters += FormattableString.Invariant($"&region={region}");
        }

        return new UriBuilder(new Uri(_baseAddress, path))
        {
            Query = parameters
        }.Uri;
    }

    /// <summary>
    /// Turns a body the source did not really say anything in into the absence it is.
    /// </summary>
    /// <param name="body">What arrived beside the status.</param>
    /// <returns>The source's own message, or null where there was none to read.</returns>
    /// <remarks>
    /// The source states a refusal as a message inside a JSON object, and
    /// anything else that answers on its behalf - a proxy, a gateway, a captive
    /// portal - answers with a page. Reading only the field means an operator
    /// is shown the source's words or nothing, rather than the first line of
    /// somebody else's HTML. #79 is what asks for the words where there are
    /// any.
    /// </remarks>
    private static string? Said(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Text(document.RootElement, "status_message")
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Carries one request to the source and brings back what the reading half needs.
    /// </summary>
    /// <param name="httpClientFactory">Where the client comes from.</param>
    /// <param name="accessToken">The credential to present.</param>
    /// <param name="address">Where to ask.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The status, the body and any wait the source named.</returns>
    /// <remarks>
    /// The credential is a header and never a query parameter, so it is absent
    /// from anything that logs a URL, which is #80's door. Nothing here writes
    /// a log line at all.
    ///
    /// A wait is read only where the source stated it as a number of seconds. A
    /// date-form wait would have to be subtracted from the current time, and
    /// the time this plugin uses is injected rather than read from the machine,
    /// which is #47; taking it here would put a second clock in the tree for
    /// the rarer of the two spellings. What that costs is a rate limit reported
    /// without a wait, which <see cref="SourceAnswer.RateLimited"/> already has
    /// a meaning for.
    ///
    /// Nothing in this method has been run against the source. What is measured
    /// is the reading half, from fixtures, and the two are separated here for
    /// exactly that reason.
    /// </remarks>
    private static async Task<SourceTransportReply> SendAsync(
        IHttpClientFactory httpClientFactory,
        string? accessToken,
        Uri address,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);

        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", Identity());

        var client = httpClientFactory.CreateClient();

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return new SourceTransportReply((int)response.StatusCode, body, response.Headers.RetryAfter?.Delta);
    }

    /// <summary>
    /// What this plugin calls itself to the source.
    /// </summary>
    /// <returns>The assembly's own name and version.</returns>
    /// <remarks>
    /// Clause 1.C of the source's terms refuses concealing which application is
    /// calling. It is derived from the assembly rather than written out, so it
    /// follows the rename #14 is waiting to make and cannot go stale against
    /// it. It names the plugin and nothing about the server or the operator.
    /// </remarks>
    private static string Identity()
    {
        var assembly = typeof(TmdbSourceAdapter).Assembly.GetName();

        return FormattableString.Invariant($"{assembly.Name}/{assembly.Version}");
    }
}
