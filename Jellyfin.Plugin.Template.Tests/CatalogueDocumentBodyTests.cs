using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Jellyfin.Plugin.Template.Catalogue;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What a shelf's catalogue document holds, asserted from both ends: the bytes
/// a shelf becomes, and the shelf a set of bytes becomes again.
/// </summary>
/// <remarks>
/// The refusals below are written as bytes rather than as objects, because the
/// route this reader exists for is a disk. A document that has been edited, half
/// written by a build that died, or restored from a backup taken under another
/// version arrives as bytes nothing in this process made, and a test that built
/// its input through the writer could not produce one.
///
/// The instants are literals. <c>no-wall-clock</c> refuses a read of the
/// machine's clock anywhere in this tree, and a fixture whose expected bytes
/// move with the day it runs on is one nobody can assert against.
/// </remarks>
public class CatalogueDocumentBodyTests
{
    private static readonly DateTimeOffset _fetchedAt = new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] _writtenInThisOrder = { "Zulu", "Alpha", "Mike" };

    /// <summary>
    /// Every field a title carries comes back as it went in.
    /// </summary>
    /// <remarks>
    /// First, because every assertion below is that something was refused, and a
    /// reader that refused every input would pass all of them.
    /// </remarks>
    [Fact]
    public void EveryFieldATitleCarriesSurvivesTheRoundTrip()
    {
        var written = Everything();

        var read = Assert.Single(CatalogueDocumentBody.Read(Bytes(written)));

        Assert.Equal(written.SchemaVersion, read.SchemaVersion);
        Assert.Equal(written.Kind, read.Kind);
        Assert.Equal(written.Name, read.Name);
        Assert.Equal(written.OriginalName, read.OriginalName);
        Assert.Equal(written.ReleaseYear, read.ReleaseYear);
        Assert.Equal(written.Summary, read.Summary);
        Assert.Equal(written.ArtworkLocation, read.ArtworkLocation);
        Assert.Equal(written.VoteAverage, read.VoteAverage);
        Assert.Equal(written.VoteCount, read.VoteCount);
        Assert.Equal(written.FetchedAt, read.FetchedAt);
        Assert.Equal(written.Identity, read.Identity);
    }

    /// <summary>
    /// The instant a source answered survives to the tick, not to the second.
    /// </summary>
    /// <remarks>
    /// Separated from the round trip above because it is the field a rendering
    /// choice loses silently: a document written with a format that drops the
    /// fraction reads back as a title fetched slightly earlier than it was, and
    /// every assertion about equality above would still pass with a whole
    /// second thrown away.
    ///
    /// It is also the field a retention is asked against, per #68, so the
    /// direction of the loss is the one that keeps a record past its expiry.
    /// </remarks>
    [Fact]
    public void TheInstantASourceAnsweredSurvivesToTheTick()
    {
        var precise = _fetchedAt.AddTicks(1234567);
        var written = Everything() with { FetchedAt = precise };

        var read = Assert.Single(CatalogueDocumentBody.Read(Bytes(written)));

        Assert.Equal(precise, read.FetchedAt);
        Assert.Equal(precise.UtcTicks, read.FetchedAt.UtcTicks);
    }

    /// <summary>
    /// A field the source did not supply is absent from the document rather than
    /// written as null.
    /// </summary>
    /// <remarks>
    /// Asserted on the bytes as well as on the record, because the two halves
    /// fail differently: a writer that emitted nulls would still round-trip, and
    /// what it would have cost is a document carrying two spellings of an
    /// absence for every later reader to tell apart.
    /// </remarks>
    [Fact]
    public void AFieldTheSourceDidNotSupplyIsAbsentRatherThanNull()
    {
        var bare = Bare();

        var document = Text(bare);

        Assert.DoesNotContain("null", document, StringComparison.Ordinal);
        Assert.DoesNotContain("originalName", document, StringComparison.Ordinal);
        Assert.DoesNotContain("releaseYear", document, StringComparison.Ordinal);
        Assert.DoesNotContain("summary", document, StringComparison.Ordinal);
        Assert.DoesNotContain("artwork", document, StringComparison.Ordinal);
        Assert.DoesNotContain("score", document, StringComparison.Ordinal);

        var read = Assert.Single(CatalogueDocumentBody.Read(Bytes(bare)));

        Assert.Null(read.OriginalName);
        Assert.Null(read.ReleaseYear);
        Assert.Null(read.Summary);
        Assert.Null(read.ArtworkLocation);
        Assert.Null(read.VoteAverage);
        Assert.Null(read.VoteCount);
    }

    /// <summary>
    /// A null written where a field would go is refused rather than read as the
    /// absence beside it.
    /// </summary>
    /// <remarks>
    /// The other side of the assertion above, and the one that has to be made on
    /// bytes: this build never writes such a document, so the case only arrives
    /// from something else. Reading it as an absence would make a document with
    /// a field somebody blanked by hand indistinguishable from one where the
    /// source said nothing.
    /// </remarks>
    [Fact]
    public void ANullWhereAFieldWouldGoIsRefusedRatherThanReadAsAnAbsence()
    {
        var document = Text(Everything()).Replace(
            "\"summary\":\"Two crews on one job\"",
            "\"summary\":null",
            StringComparison.Ordinal);

        var refused = Assert.Throws<InvalidDataException>(() => CatalogueDocumentBody.Read(Encoding.UTF8.GetBytes(document)));

        Assert.Contains("summary", refused.Message, StringComparison.Ordinal);
        Assert.Contains("Null", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The order titles were written in is the order they come back in.
    /// </summary>
    /// <remarks>
    /// #91 puts a shelf's order on the shelf, so this reader restoring a
    /// different sequence would be a second answer to that question. Three
    /// titles whose names sort the other way round, so a document that came back
    /// sorted by anything on the record fails rather than passing by accident.
    /// </remarks>
    [Fact]
    public void TheOrderTitlesWereWrittenInIsTheOrderTheyComeBack()
    {
        var titles = new[]
        {
            Named("Zulu", "3"),
            Named("Alpha", "1"),
            Named("Mike", "2")
        };

        var read = CatalogueDocumentBody.Read(Bytes(titles));

        Assert.Equal(_writtenInThisOrder, Names(read));
    }

    /// <summary>
    /// A shelf that came back with nothing is an empty document, not an absent
    /// one.
    /// </summary>
    /// <remarks>
    /// #63's third condition is about telling a shelf that was never refreshed
    /// from one that was refreshed and came back empty. The first is the absence
    /// of a document, which the store already answers with null; this is the
    /// second, and writing nothing for it would make the two one state.
    /// </remarks>
    [Fact]
    public void AShelfThatCameBackWithNothingIsAnEmptyDocumentRatherThanNoDocument()
    {
        var bytes = Bytes(Array.Empty<DiscoverTitle>());

        Assert.NotEmpty(bytes);
        Assert.Empty(CatalogueDocumentBody.Read(bytes));
    }

    /// <summary>
    /// The document carries its titles and nothing else.
    /// </summary>
    /// <remarks>
    /// The other question <c>docs/decisions/0005</c> left open, asserted rather
    /// than left to the eye. A header added later would be a second register of
    /// a fact the document's name, the envelope's version or the record's own
    /// fetch instant already holds, and it would arrive as a wrapper object
    /// around this array.
    /// </remarks>
    [Fact]
    public void TheDocumentCarriesItsTitlesAndNothingElse()
    {
        var document = Text(Everything());

        Assert.StartsWith("[", document, StringComparison.Ordinal);
        Assert.EndsWith("]", document, StringComparison.Ordinal);
    }

    /// <summary>
    /// Bytes this writer did not produce are refused whole.
    /// </summary>
    /// <param name="payload">What arrived where a document was expected.</param>
    [Theory]
    [InlineData("")]
    [InlineData("not a document at all")]
    [InlineData("{\"titles\":[]}")]
    [InlineData("[{\"schemaVersion\":1,")]
    public void BytesThisWriterDidNotProduceAreRefusedWhole(string payload)
    {
        Assert.Throws<InvalidDataException>(() => CatalogueDocumentBody.Read(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>
    /// A record schema this build does not read is refused, and both numbers are
    /// in what the operator is told.
    /// </summary>
    /// <remarks>
    /// The envelope makes the same refusal for the document format, under #67.
    /// This is the record's own schema one level down, and it is the reason the
    /// number is written into every entry rather than left to be assumed from
    /// the envelope: without it, a document written under a later record shape
    /// would be read field by field as though the two shapes agreed.
    /// </remarks>
    [Fact]
    public void ARecordSchemaThisBuildDoesNotReadIsRefusedAndNamesBothNumbers()
    {
        var document = Text(Everything()).Replace(
            "\"schemaVersion\":1",
            "\"schemaVersion\":2",
            StringComparison.Ordinal);

        var refused = Assert.Throws<InvalidDataException>(() => CatalogueDocumentBody.Read(Encoding.UTF8.GetBytes(document)));

        Assert.Contains("2", refused.Message, StringComparison.Ordinal);
        Assert.Contains("1", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A title the record type refuses is refused on the way in from a disk, and
    /// the record's own reason survives.
    /// </summary>
    /// <param name="replaced">What the writer put there.</param>
    /// <param name="edit">The value a hand or a half-written file put in its place.</param>
    /// <remarks>
    /// This is what going through the record's initialisers by hand buys. A
    /// route that built the record around them would put a title on a shelf that
    /// the type refuses to be constructed as anywhere else in this plugin, and
    /// the disk is the one place the bytes were last touched by something other
    /// than this build.
    /// </remarks>
    [Theory]
    [InlineData("\"name\":\"Heat\"", "\"name\":\"   \"")]
    [InlineData("\"score\":8.2", "\"score\":-1")]
    [InlineData("\"scoreCount\":15234", "\"scoreCount\":-1")]
    [InlineData("\"fetchedAt\":\"2026-08-30T09:00:00.0000000Z\"", "\"fetchedAt\":\"2026-08-30T09:00:00.0000000+02:00\"")]
    public void ATitleTheRecordTypeRefusesIsRefusedOnTheWayInFromADisk(string replaced, string edit)
    {
        var document = Text(Everything());

        Assert.Contains(replaced, document, StringComparison.Ordinal);

        var edited = document.Replace(replaced, edit, StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => CatalogueDocumentBody.Read(Encoding.UTF8.GetBytes(edited)));
    }

    /// <summary>
    /// An entry naming a source this build does not write is refused rather than
    /// dropped.
    /// </summary>
    /// <remarks>
    /// Dropping it would be the worse failure of the two. An identity is what a
    /// title is compared and handed over by, per #94 and #99, so a document that
    /// silently lost one identifier would produce a title that is a different
    /// title to everything downstream while still drawing correctly.
    /// </remarks>
    [Fact]
    public void AnIdentifierNamingASourceThisBuildDoesNotWriteIsRefused()
    {
        var document = Text(Everything()).Replace(
            "\"imdb\":",
            "\"omdb\":",
            StringComparison.Ordinal);

        var refused = Assert.Throws<InvalidDataException>(() => CatalogueDocumentBody.Read(Encoding.UTF8.GetBytes(document)));

        Assert.Contains("omdb", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A document whose entries say nothing about which title they are is
    /// refused.
    /// </summary>
    /// <param name="identifiers">What stood where the identifiers belong.</param>
    [Theory]
    [InlineData("\"identifiers\":{}")]
    [InlineData("\"identifiers\":[]")]
    [InlineData("\"identifiers\":\"tt0113277\"")]
    public void AnEntryThatSaysNothingAboutWhichTitleItIsIsRefused(string identifiers)
    {
        var document = Text(Everything());
        var start = document.IndexOf("\"identifiers\":", StringComparison.Ordinal);

        Assert.True(start >= 0);

        var edited = string.Concat(document.AsSpan(0, start), identifiers, "}]");

        Assert.Throws<InvalidDataException>(() => CatalogueDocumentBody.Read(Encoding.UTF8.GetBytes(edited)));
    }

    /// <summary>
    /// A kind this build does not write is refused rather than mapped onto the
    /// nearest one.
    /// </summary>
    [Fact]
    public void AKindThisBuildDoesNotWriteIsRefused()
    {
        var document = Text(Everything()).Replace(
            "\"kind\":\"movie\"",
            "\"kind\":\"film\"",
            StringComparison.Ordinal);

        var refused = Assert.Throws<InvalidDataException>(() => CatalogueDocumentBody.Read(Encoding.UTF8.GetBytes(document)));

        Assert.Contains("film", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An artwork location that is not an absolute address is refused.
    /// </summary>
    /// <remarks>
    /// The record refuses a relative one for the same reason it is refused here:
    /// artwork is referenced where the source keeps it, per #62, so a location
    /// that does not say which host that is is one a client cannot draw.
    /// </remarks>
    [Fact]
    public void AnArtworkLocationThatIsNotAnAbsoluteAddressIsRefused()
    {
        var document = Text(Everything()).Replace(
            "https://image.themoviedb.example/w500/heat.jpg",
            "/w500/heat.jpg",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => CatalogueDocumentBody.Read(Encoding.UTF8.GetBytes(document)));
    }

    /// <summary>
    /// An instant that is not the round-trip form this build writes is refused.
    /// </summary>
    /// <remarks>
    /// A shorter rendering of the same moment is the case to be careful about.
    /// It parses under most formats, so accepting it would mean a document
    /// written by something else deciding the precision a retention is measured
    /// at.
    /// </remarks>
    [Fact]
    public void AnInstantThatIsNotTheRoundTripFormIsRefused()
    {
        var document = Text(Everything()).Replace(
            "\"fetchedAt\":\"2026-08-30T09:00:00.0000000Z\"",
            "\"fetchedAt\":\"2026-08-30 09:00:00Z\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => CatalogueDocumentBody.Read(Encoding.UTF8.GetBytes(document)));
    }

    /// <summary>
    /// A document that came back through the store is the one that went in.
    /// </summary>
    /// <remarks>
    /// The two halves are written and tested apart, so this is the seam between
    /// them: the writer hands the store a stream, the store puts its own header
    /// and checksum in front of the bytes, and what <c>Read</c> gives back is
    /// the payload this reader is handed. A header the store grew that this
    /// reader had to skip would fail here and nowhere else.
    /// </remarks>
    [Fact]
    public void ADocumentThatCameBackThroughTheStoreIsTheOneThatWentIn()
    {
        var folder = Path.Combine(Path.GetTempPath(), "jellyfin-plugin-discover-tests", "document-body-through-the-store");
        Remove(folder);
        try
        {
            var store = new CatalogueDocumentStore(
                new CatalogueDirectory(folder),
                new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());

            var name = CatalogueLayout.DocumentName(Jellyfin.Plugin.Template.Shelves.ShelfQuestion.Trending, DiscoverTitleKind.Movie);

            using (var content = new MemoryStream(Bytes(Everything())))
            {
                store.Write(name, content);
            }

            var payload = store.Read(name);

            Assert.NotNull(payload);

            var read = Assert.Single(CatalogueDocumentBody.Read(payload!));

            Assert.Equal("Heat", read.Name);
            Assert.Equal(_fetchedAt, read.FetchedAt);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Neither half is handed a null.
    /// </summary>
    [Fact]
    public void NeitherHalfIsHandedANull()
    {
        Assert.Throws<ArgumentNullException>(() => CatalogueDocumentBody.Write(new MemoryStream(), null!));
        Assert.Throws<ArgumentNullException>(() => CatalogueDocumentBody.Write(null!, Array.Empty<DiscoverTitle>()));
        Assert.Throws<ArgumentNullException>(() => CatalogueDocumentBody.Read(null!));
        Assert.Throws<ArgumentNullException>(() => CatalogueDocumentBody.Write(new MemoryStream(), new DiscoverTitle[] { null! }));
    }

    private static DiscoverTitle Everything() => new DiscoverTitle
    {
        Kind = DiscoverTitleKind.Movie,
        Name = "Heat",
        OriginalName = "Heat 1995",
        ReleaseYear = 1995,
        Summary = "Two crews on one job",
        ArtworkLocation = new Uri("https://image.themoviedb.example/w500/heat.jpg"),
        VoteAverage = 8.2,
        VoteCount = 15234,
        FetchedAt = _fetchedAt,
        Identity = new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Imdb, "tt0113277"),
            new ProviderIdentifier(MetadataSource.Tmdb, "949")
        })
    };

    private static DiscoverTitle Bare() => new DiscoverTitle
    {
        Kind = DiscoverTitleKind.Series,
        Name = "The Wire",
        FetchedAt = _fetchedAt,
        Identity = new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Tmdb, "1438")
        })
    };

    private static DiscoverTitle Named(string name, string identifier) => new DiscoverTitle
    {
        Kind = DiscoverTitleKind.Movie,
        Name = name,
        FetchedAt = _fetchedAt,
        Identity = new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Tmdb, identifier)
        })
    };

    private static byte[] Bytes(DiscoverTitle title) => Bytes(new[] { title });

    private static byte[] Bytes(IReadOnlyList<DiscoverTitle> titles)
    {
        using var destination = new MemoryStream();
        CatalogueDocumentBody.Write(destination, titles);
        return destination.ToArray();
    }

    private static string Text(DiscoverTitle title) => Encoding.UTF8.GetString(Bytes(title));

    private static List<string> Names(IReadOnlyList<DiscoverTitle> titles)
    {
        var names = new List<string>();

        foreach (var title in titles)
        {
            names.Add(title.Name);
        }

        return names;
    }

    private static void Remove(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
