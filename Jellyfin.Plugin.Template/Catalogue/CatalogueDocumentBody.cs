using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// What one shelf's catalogue document holds, and how it becomes the bytes
/// <see cref="CatalogueDocumentStore"/> writes and reads.
/// </summary>
/// <remarks>
/// <para>
/// <c>docs/decisions/0005</c> fixed which document a shelf's titles are in and
/// what it is called, and its closing section says the payload's shape is #67's
/// and the refresh's. #67 landed the envelope, which is the line a document
/// names its format on and the refusal of a version this build does not know.
/// This is the other half: the bytes between that header and the end of the
/// file.
/// </para>
/// <para>
/// Until this existed the store took a stream and nothing in the plugin made
/// one, so a refresh had a place to put a shelf's titles and no way to make what
/// goes in it. That is the second absence under #87's first condition, and it is
/// a different one from the missing scheduled task.
/// </para>
/// <para>
/// The document holds its titles and nothing else, which is the other question
/// 0005 left open. A shelf's identity is the document's name, by the layout; the
/// format version is the envelope's, by #67; and when the source answered is on
/// each record, because <see cref="CatalogueRetention"/> is asked per record
/// rather than per document and a document-level age would be a second answer to
/// a question a record already answers. Every header this document could carry
/// is a fact something else already holds.
/// </para>
/// <para>
/// JSON, written straight to the destination stream. The means is not carried
/// over from habit: the runtime this plugin already builds against reads the
/// source's answers with <see cref="JsonDocument"/>, so no package, language or
/// runtime is added; the store's envelope is ASCII and a UTF-8 payload after it
/// leaves that header's byte count fixed; and a text payload is one an operator
/// can read beside the checksum when they are working out why a shelf is empty.
/// A binary form would be smaller and would make that last case a tooling
/// question.
/// </para>
/// <para>
/// Compact rather than indented. The bytes are what #71 will measure and what
/// the store hashes on every read, and indentation buys a rendering any reader
/// can produce from the compact form.
/// </para>
/// <para>
/// Written and read field by field rather than through
/// <see cref="JsonSerializer"/>. Every refusal <see cref="DiscoverTitle"/> makes
/// is on an <c>init</c> accessor, and going through those accessors by hand
/// keeps the record's own refusals on the way in from a disk, which is the route
/// where the bytes were last touched by something other than this build.
/// </para>
/// <para>
/// A field the source did not supply is left out rather than written as null, so
/// a document does not carry two spellings of an absence for a reader to tell
/// apart.
/// </para>
/// <para>
/// A payload this reader cannot rebuild is refused whole. Half a shelf restored
/// silently is a row a user scrolls that is missing titles for a reason nobody
/// can see, and the store already treats a document it refuses as absent, which
/// is the state a refresh replaces.
/// </para>
/// </remarks>
public static class CatalogueDocumentBody
{
    private const string SchemaVersionField = "schemaVersion";
    private const string KindField = "kind";
    private const string NameField = "name";
    private const string OriginalNameField = "originalName";
    private const string ReleaseYearField = "releaseYear";
    private const string SummaryField = "summary";
    private const string ArtworkField = "artwork";
    private const string ScoreField = "score";
    private const string ScoreCountField = "scoreCount";
    private const string FetchedAtField = "fetchedAt";
    private const string IdentifiersField = "identifiers";

    /// <summary>
    /// Writes a shelf's titles as the payload of its catalogue document.
    /// </summary>
    /// <param name="destination">Where the bytes go, which is the stream the store copies and hashes.</param>
    /// <param name="titles">The shelf's titles, in the order they are to be read back.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either argument is null, or when the list holds a null title.
    /// </exception>
    /// <remarks>
    /// The order is preserved rather than sorted here. #91 puts the order on the
    /// shelf, so a document that sorted on the way out would be a second answer
    /// to that question and the two would disagree the day either moved.
    ///
    /// An empty shelf is an empty array rather than an absent document. A shelf
    /// that was asked and came back with nothing and a shelf that was never
    /// asked are different states, and #63's third condition is about telling
    /// them apart; writing nothing for the first would make them one.
    /// </remarks>
    public static void Write(Stream destination, IReadOnlyList<DiscoverTitle> titles)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(titles);

        using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = false });

        writer.WriteStartArray();

        for (var index = 0; index < titles.Count; index++)
        {
            var title = titles[index];
            ArgumentNullException.ThrowIfNull(title, nameof(titles));
            WriteTitle(writer, title);
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    /// <summary>
    /// Reads back the titles a document holds.
    /// </summary>
    /// <param name="payload">The bytes the store handed back, which are the ones after its header.</param>
    /// <returns>The shelf's titles, in the order they were written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the payload is null.</exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the bytes are not a payload this writer produced, including
    /// when a record in them is one <see cref="DiscoverTitle"/> refuses to be.
    /// </exception>
    /// <remarks>
    /// Every refusal names what it found rather than saying the document is
    /// invalid, because the caller writes it into a log an operator reads and
    /// "the catalogue is corrupt" tells them nothing they can act on.
    ///
    /// A refusal the record type itself makes is carried out as the same
    /// exception with the record's own message inside it, so a caller has one
    /// type to catch and the reason survives.
    /// </remarks>
    public static IReadOnlyList<DiscoverTitle> Read(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException reason)
        {
            throw new InvalidDataException(
                "The bytes after this document's header are not the JSON this plugin writes into one.",
                reason);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    FormattableString.Invariant(
                        $"A catalogue document holds an array of titles. This one holds {document.RootElement.ValueKind}."));
            }

            var titles = new List<DiscoverTitle>();
            var position = 0;

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                titles.Add(ReadTitle(entry, position));
                position++;
            }

            return titles;
        }
    }

    private static void WriteTitle(Utf8JsonWriter writer, DiscoverTitle title)
    {
        writer.WriteStartObject();

        // The record's own schema version, one level under the envelope's. It is
        // not the same number as the document format's and is not derived from
        // it: a record shape that moved without the document format moving would
        // be read field by field as though the two shapes agreed, which is the
        // reading #67's envelope refusal exists against, and the only thing that
        // makes that refusal available here is the number being on the record.
        writer.WriteNumber(SchemaVersionField, title.SchemaVersion);
        writer.WriteString(KindField, Spelling(title.Kind));
        writer.WriteString(NameField, title.Name);

        if (title.OriginalName is { } original)
        {
            writer.WriteString(OriginalNameField, original);
        }

        if (title.ReleaseYear is { } year)
        {
            writer.WriteNumber(ReleaseYearField, year);
        }

        if (title.Summary is { } summary)
        {
            writer.WriteString(SummaryField, summary);
        }

        if (title.ArtworkLocation is { } artwork)
        {
            writer.WriteString(ArtworkField, artwork.AbsoluteUri);
        }

        if (title.VoteAverage is { } score)
        {
            writer.WriteNumber(ScoreField, score);
        }

        if (title.VoteCount is { } scoreCount)
        {
            writer.WriteNumber(ScoreCountField, scoreCount);
        }

        // Round-trip format, so the instant that comes back is the instant that
        // went in rather than one rendered to a precision the record did not
        // choose.
        //
        // Written off UtcDateTime rather than off the DateTimeOffset, which
        // renders the same instant one character differently and for a reason
        // worth keeping. The record refuses an offset other than UTC, so the
        // offset in an offset-form rendering is always "+00:00", and the writer's
        // encoder escapes that plus as + in every document on disk. Ending
        // the instant in Z says the same thing in bytes an operator reading the
        // file can see, and the reader below parses both spellings under "O"
        // anyway, so nothing is narrowed by writing the legible one.
        writer.WriteString(FetchedAtField, title.FetchedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

        writer.WriteStartObject(IdentifiersField);

        foreach (var identifier in title.Identity.Identifiers)
        {
            writer.WriteString(Spelling(identifier.Source), identifier.Value);
        }

        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static DiscoverTitle ReadTitle(JsonElement entry, int position)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Entry {position} of this document is {entry.ValueKind} rather than a title."));
        }

        var schemaVersion = RequiredNumber(entry, SchemaVersionField, position);

        if (schemaVersion != DiscoverTitle.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Entry {position} declares record schema {schemaVersion} and this build reads schema {DiscoverTitle.CurrentSchemaVersion}. Nothing here migrates one shape onto the other, so it is refused rather than read as though the two agreed. The next refresh replaces the document."));
        }

        try
        {
            return new DiscoverTitle
            {
                SchemaVersion = schemaVersion,
                Kind = ReadKind(entry, position),
                Name = RequiredText(entry, NameField, position),
                OriginalName = OptionalText(entry, OriginalNameField, position),
                ReleaseYear = OptionalNumber(entry, ReleaseYearField, position),
                Summary = OptionalText(entry, SummaryField, position),
                ArtworkLocation = ReadArtwork(entry, position),
                VoteAverage = OptionalScore(entry, ScoreField, position),
                VoteCount = OptionalNumber(entry, ScoreCountField, position),
                FetchedAt = ReadFetchedAt(entry, position),
                Identity = ReadIdentity(entry, position)
            };
        }
        catch (ArgumentException reason)
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Entry {position} of this document is not a title this build will hold: {reason.Message}"),
                reason);
        }
    }

    private static DiscoverTitleKind ReadKind(JsonElement entry, int position)
    {
        var spelling = RequiredText(entry, KindField, position);

        return spelling switch
        {
            "movie" => DiscoverTitleKind.Movie,
            "series" => DiscoverTitleKind.Series,
            _ => throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Entry {position} calls its kind '{spelling}', which is not one this build writes."))
        };
    }

    private static Uri? ReadArtwork(JsonElement entry, int position)
    {
        var written = OptionalText(entry, ArtworkField, position);

        if (written is null)
        {
            return null;
        }

        if (!Uri.TryCreate(written, UriKind.Absolute, out var artwork))
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Entry {position} gives '{written}' as its artwork location, which is not an absolute address."));
        }

        return artwork;
    }

    private static DateTimeOffset ReadFetchedAt(JsonElement entry, int position)
    {
        var written = RequiredText(entry, FetchedAtField, position);

        if (!DateTimeOffset.TryParseExact(
                written,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var fetchedAt))
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Entry {position} gives '{written}' as when its source answered, which is not the round-trip instant this build writes."));
        }

        return fetchedAt;
    }

    private static DiscoverTitleIdentity ReadIdentity(JsonElement entry, int position)
    {
        if (!entry.TryGetProperty(IdentifiersField, out var written) || written.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Entry {position} carries no '{IdentifiersField}' object, so nothing in it says which title it is."));
        }

        var identifiers = new List<ProviderIdentifier>();

        foreach (var pair in written.EnumerateObject())
        {
            var source = pair.Name switch
            {
                "imdb" => MetadataSource.Imdb,
                "tmdb" => MetadataSource.Tmdb,
                "tvdb" => MetadataSource.Tvdb,
                _ => MetadataSource.None
            };

            if (source == MetadataSource.None)
            {
                throw new InvalidDataException(
                    FormattableString.Invariant(
                        $"Entry {position} names '{pair.Name}' as a source of an identifier, which is not one this build writes."));
            }

            if (pair.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    FormattableString.Invariant(
                        $"Entry {position} gives its {pair.Name} identifier as {pair.Value.ValueKind} rather than as text."));
            }

            identifiers.Add(new ProviderIdentifier(source, pair.Value.GetString()!));
        }

        return new DiscoverTitleIdentity(identifiers);
    }

    private static string RequiredText(JsonElement entry, string field, int position)
    {
        return OptionalText(entry, field, position)
            ?? throw new InvalidDataException(
                FormattableString.Invariant($"Entry {position} carries no '{field}', which every title this build writes has."));
    }

    private static string? OptionalText(JsonElement entry, string field, int position)
    {
        if (!entry.TryGetProperty(field, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Entry {position} gives its '{field}' as {value.ValueKind} rather than as text."));
        }

        return value.GetString();
    }

    private static int RequiredNumber(JsonElement entry, string field, int position)
    {
        return OptionalNumber(entry, field, position)
            ?? throw new InvalidDataException(
                FormattableString.Invariant($"Entry {position} carries no '{field}', which every title this build writes has."));
    }

    private static int? OptionalNumber(JsonElement entry, string field, int position)
    {
        if (!entry.TryGetProperty(field, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Entry {position} gives its '{field}' as {value.ValueKind} rather than as a whole number this build can hold."));
        }

        return number;
    }

    private static double? OptionalScore(JsonElement entry, string field, int position)
    {
        if (!entry.TryGetProperty(field, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var score))
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Entry {position} gives its '{field}' as {value.ValueKind} rather than as a number this build can hold."));
        }

        return score;
    }

    // The kind and the source are spelled here rather than taken from
    // CatalogueLayout, which spells the same two words for a file name. The two
    // are different subjects: a name in the layout is what a document is called
    // on a disk, and a value here is part of the format the envelope's version
    // covers, so moving one is a rename and moving the other is a format change.
    // A single spelling shared between them would make either move look like
    // both.
    private static string Spelling(DiscoverTitleKind kind) => kind switch
    {
        DiscoverTitleKind.Movie => "movie",
        DiscoverTitleKind.Series => "series",
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "A title in a catalogue document carries the kind it was mapped to. None is what an unset field reads as, and the record refuses it before this is reached.")
    };

    private static string Spelling(MetadataSource source) => source switch
    {
        MetadataSource.Imdb => "imdb",
        MetadataSource.Tmdb => "tmdb",
        MetadataSource.Tvdb => "tvdb",
        _ => throw new ArgumentOutOfRangeException(
            nameof(source),
            source,
            "An identifier in a catalogue document names the source that supplied it. None is what an unset field reads as, and the identity refuses it before this is reached.")
    };
}
