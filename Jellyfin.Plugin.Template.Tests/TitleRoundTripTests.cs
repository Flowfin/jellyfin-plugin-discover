using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;
using Jellyfin.Plugin.Template.Surface;
using Jellyfin.Plugin.Template.Tests.Fixtures;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The whole route a title takes, from a source's bytes to an entry, read back at the far end.
/// </summary>
/// <remarks>
/// Two properties #55 asks for that no single component can hold, because each
/// of them is about what survives a hand-over rather than about what one type
/// does. The parse is <see cref="TmdbSourceAdapterTests"/>, the address is
/// <see cref="TitleAddressTests"/>, and both of those start from a value
/// written in the test. These start from the bytes and end at the identifier a
/// client would send back.
///
/// What the expectation is compared against is the response rather than the
/// mapping. Every identifier and every piece of text below is read out of the
/// fixture with a second parser, so a mapping that renamed a field, prefixed an
/// identifier or composed a name is compared with what the source actually
/// sent instead of with itself. A test that asked the adapter for the expected
/// value would agree with any mapping at all.
///
/// Nothing here reaches a network. The bodies come from
/// <see cref="TmdbFixtures"/> and were written by hand rather than captured,
/// which is #48's position, so what a real response would carry in these fields
/// is unmeasured and the round trip is proven over the shapes this repository
/// wrote down.
/// </remarks>
public class TitleRoundTripTests
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
    /// A title reaches an entry and its address reads back as the identifier the response carried.
    /// </summary>
    /// <param name="kind">Which kind of page the source answered with.</param>
    /// <remarks>
    /// The third condition on #55. The identifiers are what #94 hands to the
    /// sibling and what #89 compares against the library, so a title whose
    /// identifier does not survive the trip out to a client and back is a title
    /// neither of those can act on. The failure is quiet in both directions: an
    /// identifier the mapping decorated still addresses a title, still draws,
    /// and only stops meaning anything at the far end.
    ///
    /// Both kinds are driven because the source spells a film's identifier and
    /// a series' under the same field name while spelling everything else
    /// differently, which is the shape a mapping is most likely to get half
    /// right.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Theory]
    [InlineData(DiscoverTitleKind.Movie)]
    [InlineData(DiscoverTitleKind.Series)]
    public async Task AnIdentifierSurvivesTheTripToAnEntryAndBack(DiscoverTitleKind kind)
    {
        var response = PageFor(kind);

        var answer = await Asked(response)
            .FetchAsync(new SourceQuery("trending", kind, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(SourceOutcome.Answered, answer.Outcome);
        Assert.NotEmpty(answer.Titles);

        var readBack = new List<ProviderIdentifier>();

        foreach (var title in answer.Titles)
        {
            var entry = SurfaceEntry.Of(TitleAddress.For(title.Identity), title);

            Assert.Equal(SurfaceEntryKind.Title, entry.Kind);
            Assert.Same(title, entry.Title);

            var identifier = TitleAddress.IdentifierIn(entry.Address);

            Assert.NotNull(identifier);

            readBack.Add(identifier.Value);
        }

        Assert.Equal(IdentifiersIn(response), readBack);
    }

    /// <summary>
    /// No text a title carries was composed out of a source's values and this plugin's own.
    /// </summary>
    /// <param name="kind">Which kind of page the source answered with.</param>
    /// <remarks>
    /// The fifth condition on #55. A field like "Some Film (2019)" draws
    /// perfectly and breaks the first comparison anybody makes against it,
    /// because taking it apart again needs a rule for every source and every
    /// language that ever produced one. The record's own remarks state the
    /// property; until now nothing asserted it.
    ///
    /// Every string the record carries is looked for verbatim among the strings
    /// the response holds, and an identifier among the numbers the response
    /// holds as the invariant culture spells them. That is a wider net than
    /// naming the fields: a text field added to the record later is caught by
    /// this test on the day its mapping composes something, without anybody
    /// remembering to add it here.
    ///
    /// What it cannot see is a composition out of two of the source's own
    /// values, since both halves are the source's and the result is not
    /// asserted to be one of them. It is also blind to a field this plugin
    /// fills with a constant of its own carrying no source value at all, which
    /// is the neighbouring defect and is not what this condition is about.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Theory]
    [InlineData(DiscoverTitleKind.Movie)]
    [InlineData(DiscoverTitleKind.Series)]
    public async Task NoTextOnATitleWasComposedByThisPlugin(DiscoverTitleKind kind)
    {
        var response = PageFor(kind);
        var sent = ValuesIn(response);

        var answer = await Asked(response)
            .FetchAsync(new SourceQuery("trending", kind, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.NotEmpty(answer.Titles);

        var composed = new List<string>();

        foreach (var title in answer.Titles)
        {
            foreach (var carried in TextOn(title))
            {
                if (!sent.Contains(carried))
                {
                    composed.Add($"{title.Identity.Primary.Value} carries '{carried}', which the source did not send.");
                }
            }
        }

        Assert.Empty(composed);
    }

    /// <summary>
    /// The one field that is composed is composed out of the source's own two halves.
    /// </summary>
    /// <remarks>
    /// Stated rather than left as a hole in the test above. The artwork
    /// location is a URL, and a URL is by construction a host joined to a path,
    /// so it is the one place on the record where a value the source sent is
    /// put together with something this plugin holds. What makes that legal
    /// under #55's fifth condition is that the other half is the source's own
    /// image host rather than anything of this plugin's, and that nothing of
    /// the title's own reaches it: not the name, not the year, not the
    /// identifier.
    ///
    /// Written as the location the response's path resolves against, so a size
    /// or a host this plugin changed later is a change to this line rather than
    /// something that passes quietly. What the source's reference says about
    /// that host is <c>docs/source-api/tmdb.md</c>, and that the picture is
    /// linked rather than copied is #62.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertions have been made.</returns>
    [Fact]
    public async Task TheArtworkLocationJoinsTheSourcesPathToTheSourcesHost()
    {
        var answer = await Asked(TmdbFixtures.MoviePage)
            .FetchAsync(new SourceQuery("trending", DiscoverTitleKind.Movie, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        var located = answer.Titles[0];

        Assert.NotNull(located.ArtworkLocation);
        Assert.Equal("image.tmdb.org", located.ArtworkLocation.Host);

        var path = Assert.Single(ValuesIn(TmdbFixtures.MoviePage, "poster_path"));

        Assert.Equal(new Uri("https://image.tmdb.org/t/p/w500" + path), located.ArtworkLocation);

        Assert.DoesNotContain(located.Name, located.ArtworkLocation.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain(located.Identity.Primary.Value, located.ArtworkLocation.AbsoluteUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page the source answers a question of a given kind with.
    /// </summary>
    /// <param name="kind">The kind that was asked for.</param>
    /// <returns>The fixture body.</returns>
    private static string PageFor(DiscoverTitleKind kind) => kind switch
    {
        DiscoverTitleKind.Movie => TmdbFixtures.MoviePage,
        DiscoverTitleKind.Series => TmdbFixtures.SeriesPage,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "There is no fixture for this kind.")
    };

    /// <summary>
    /// The identifiers a response carried, in the order it listed them.
    /// </summary>
    /// <param name="fixture">The response body.</param>
    /// <returns>One identifier per entry, spelled as the source spelled it.</returns>
    /// <remarks>
    /// Read with a parser of this test's own rather than through the adapter,
    /// which is the whole point of the comparison: an adapter asked what it
    /// found agrees with itself whatever it did.
    /// </remarks>
    private static List<ProviderIdentifier> IdentifiersIn(string fixture)
    {
        using var document = JsonDocument.Parse(TmdbFixtures.Body(fixture));

        var found = new List<ProviderIdentifier>();

        foreach (var entry in document.RootElement.GetProperty("results").EnumerateArray())
        {
            found.Add(new ProviderIdentifier(
                MetadataSource.Tmdb,
                entry.GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture)));
        }

        return found;
    }

    /// <summary>
    /// Every value a response holds, as text.
    /// </summary>
    /// <param name="fixture">The response body.</param>
    /// <returns>The set a value on a record has to be a member of to be one the source sent.</returns>
    /// <remarks>
    /// Numbers are included as the invariant culture spells them, because an
    /// identifier arrives as a number and is carried as text. Walking the
    /// document rather than the field names this plugin reads keeps the set
    /// from shrinking on the day a mapping starts reading a field nobody
    /// listed here.
    /// </remarks>
    private static HashSet<string> ValuesIn(string fixture)
    {
        using var document = JsonDocument.Parse(TmdbFixtures.Body(fixture));

        var sent = new HashSet<string>(StringComparer.Ordinal);

        Collect(document.RootElement, sent);

        return sent;
    }

    /// <summary>
    /// What a response held under one field name, wherever it appears.
    /// </summary>
    /// <param name="fixture">The response body.</param>
    /// <param name="field">The field name.</param>
    /// <returns>The values, in the order the document lists them.</returns>
    private static List<string> ValuesIn(string fixture, string field)
    {
        using var document = JsonDocument.Parse(TmdbFixtures.Body(fixture));

        var found = new List<string>();

        foreach (var entry in document.RootElement.GetProperty("results").EnumerateArray())
        {
            if (entry.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String)
            {
                found.Add(value.GetString()!);
            }
        }

        return found;
    }

    /// <summary>
    /// Adds every scalar under an element to a set, as text.
    /// </summary>
    /// <param name="element">Where to start.</param>
    /// <param name="sent">The set being filled.</param>
    private static void Collect(JsonElement element, HashSet<string> sent)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Collect(property.Value, sent);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, sent);
                }

                break;

            case JsonValueKind.String:
                sent.Add(element.GetString()!);
                break;

            case JsonValueKind.Number:
                sent.Add(element.GetRawText());
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Every piece of text a title carries, whatever field it sits in.
    /// </summary>
    /// <param name="title">The mapped title.</param>
    /// <returns>The strings that have to have come from the source unchanged.</returns>
    /// <remarks>
    /// The artwork location is not here, and the test above says why. Nothing
    /// else on the record is text: the kind is this plugin's own vocabulary,
    /// the schema version comes from the build, and the year is a number the
    /// mapping derives rather than a string it carries.
    /// </remarks>
    private static IEnumerable<string> TextOn(DiscoverTitle title)
    {
        yield return title.Name;

        if (title.OriginalName is { } original)
        {
            yield return original;
        }

        if (title.Summary is { } summary)
        {
            yield return summary;
        }

        foreach (var identifier in title.Identity.Identifiers)
        {
            yield return identifier.Value;
        }
    }

    /// <summary>
    /// An adapter that answers every question with one fixture.
    /// </summary>
    /// <param name="fixture">The body it answers with.</param>
    /// <returns>The adapter.</returns>
    private static TmdbSourceAdapter Asked(string fixture) =>
        new(
            (address, cancellationToken) =>
                Task.FromResult(new SourceTransportReply(200, TmdbFixtures.Body(fixture), null)),
            configured: true,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);
}
