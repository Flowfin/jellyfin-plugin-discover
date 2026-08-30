using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Wants;

/// <summary>
/// What the operator's want list is when it is bytes on a disk.
/// </summary>
/// <remarks>
/// <para>
/// #97's first condition is that every want is recorded whether or not a sink
/// accepted it, so the local list is complete rather than a fallback. A list
/// held only in memory is complete until the server is restarted, which is a
/// different sentence, and that is what its fifth condition asks for.
/// </para>
/// <para>
/// Written field by field for the reason
/// <see cref="CatalogueDocumentBody"/> gives: every refusal
/// <see cref="LocalWant"/> makes sits on an <c>init</c> accessor, and a disk is
/// the route where the bytes were last touched by something other than this
/// build.
/// </para>
/// <para>
/// A row's state is written as a word rather than as the number the enum
/// carries. A number is the position of a member in a list somebody can reorder,
/// and a file that outlives the build that wrote it should not depend on that.
/// </para>
/// <para>
/// The list is one document rather than one file per row. It is bounded by the
/// register, per #97's fourth condition, so it is small; the operator reads it
/// whole; and a per-row layout would make clearing one row a deletion whose
/// failure leaves a list that disagrees with itself.
/// </para>
/// </remarks>
public static class WantListDocument
{
    private const string WantIdentifierField = "wantIdentifier";
    private const string KindField = "kind";
    private const string NameField = "name";
    private const string ReleaseYearField = "releaseYear";
    private const string AskingUserField = "askingUser";
    private const string AskedAtField = "askedAt";
    private const string StateField = "state";
    private const string WithdrawnAtField = "withdrawnAt";
    private const string IdentifiersField = "identifiers";

    /// <summary>
    /// Writes the rows an operator's list holds.
    /// </summary>
    /// <param name="destination">Where the bytes go.</param>
    /// <param name="wants">The rows, in the order they are to be read back.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either argument is null, or when the list holds a null row.
    /// </exception>
    public static void Write(Stream destination, IReadOnlyList<LocalWant> wants)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(wants);

        using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = false });

        writer.WriteStartArray();

        for (var index = 0; index < wants.Count; index++)
        {
            var want = wants[index];
            ArgumentNullException.ThrowIfNull(want, nameof(wants));
            WriteWant(writer, want);
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    /// <summary>
    /// Reads back the rows a list holds.
    /// </summary>
    /// <param name="payload">The bytes after the file's format line.</param>
    /// <returns>The rows, in the order they were written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the payload is null.</exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the bytes are not a list this writer produced, including when
    /// a row in them is one <see cref="LocalWant"/> refuses to be.
    /// </exception>
    public static IReadOnlyList<LocalWant> Read(byte[] payload)
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
                "The bytes after this list's format line are not the JSON this plugin writes into one.",
                reason);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    FormattableString.Invariant(
                        $"A want list holds an array of rows. This one holds {document.RootElement.ValueKind}."));
            }

            var wants = new List<LocalWant>();
            var position = 0;

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                wants.Add(ReadWant(entry, position));
                position++;
            }

            return wants;
        }
    }

    private static void WriteWant(Utf8JsonWriter writer, LocalWant want)
    {
        writer.WriteStartObject();

        writer.WriteString(WantIdentifierField, want.WantIdentifier);
        writer.WriteString(KindField, Spelling(want.Kind));
        writer.WriteString(NameField, want.Name);

        if (want.ReleaseYear is { } year)
        {
            writer.WriteNumber(ReleaseYearField, year);
        }

        writer.WriteString(AskingUserField, want.AskingUser);
        writer.WriteString(AskedAtField, want.AskedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString(StateField, Spelling(want.State));

        if (want.WithdrawnAt is { } withdrawn)
        {
            writer.WriteString(WithdrawnAtField, withdrawn.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        }

        writer.WriteStartObject(IdentifiersField);

        foreach (var identifier in want.Identity.Identifiers)
        {
            writer.WriteString(Spelling(identifier.Source), identifier.Value);
        }

        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static LocalWant ReadWant(JsonElement entry, int position)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Row {position} of this list is {entry.ValueKind} rather than a want."));
        }

        try
        {
            return new LocalWant
            {
                WantIdentifier = RequiredText(entry, WantIdentifierField, position),
                Identity = ReadIdentity(entry, position),
                Kind = ReadKind(entry, position),
                Name = RequiredText(entry, NameField, position),
                ReleaseYear = OptionalNumber(entry, ReleaseYearField, position),
                AskingUser = ReadUser(entry, position),
                AskedAt = ReadInstant(entry, AskedAtField, position, required: true)!.Value,
                State = ReadState(entry, position),
                WithdrawnAt = ReadInstant(entry, WithdrawnAtField, position, required: false)
            };
        }
        catch (ArgumentException reason)
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Row {position} of this list is not a want this build will hold: {reason.Message}"),
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
                    $"Row {position} calls its kind '{spelling}', which is not one this build writes."))
        };
    }

    private static LocalWantState ReadState(JsonElement entry, int position)
    {
        var spelling = RequiredText(entry, StateField, position);

        return spelling switch
        {
            "asked" => LocalWantState.Asked,
            "withdrawn" => LocalWantState.Withdrawn,
            _ => throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Row {position} calls its state '{spelling}', which is not one this build writes."))
        };
    }

    private static Guid ReadUser(JsonElement entry, int position)
    {
        var written = RequiredText(entry, AskingUserField, position);

        if (!Guid.TryParseExact(written, "D", out var user))
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Row {position} gives '{written}' as who asked, which is not the identifier form the server uses."));
        }

        return user;
    }

    private static DateTimeOffset? ReadInstant(JsonElement entry, string field, int position, bool required)
    {
        var written = OptionalText(entry, field, position);

        if (written is null)
        {
            return required
                ? throw new InvalidDataException(
                    FormattableString.Invariant($"Row {position} carries no '{field}', which every want this build writes has."))
                : null;
        }

        if (!DateTimeOffset.TryParseExact(
                written,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var instant))
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Row {position} gives '{written}' as its '{field}', which is not the round-trip instant this build writes."));
        }

        return instant;
    }

    private static DiscoverTitleIdentity ReadIdentity(JsonElement entry, int position)
    {
        if (!entry.TryGetProperty(IdentifiersField, out var written) || written.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                FormattableString.Invariant(
                    $"Row {position} carries no '{IdentifiersField}' object, so nothing in it says which title was wanted."));
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
                        $"Row {position} names '{pair.Name}' as a source of an identifier, which is not one this build writes."));
            }

            if (pair.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    FormattableString.Invariant(
                        $"Row {position} gives its {pair.Name} identifier as {pair.Value.ValueKind} rather than as text."));
            }

            identifiers.Add(new ProviderIdentifier(source, pair.Value.GetString()!));
        }

        return new DiscoverTitleIdentity(identifiers);
    }

    private static string RequiredText(JsonElement entry, string field, int position)
    {
        return OptionalText(entry, field, position)
            ?? throw new InvalidDataException(
                FormattableString.Invariant($"Row {position} carries no '{field}', which every want this build writes has."));
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
                    $"Row {position} gives its '{field}' as {value.ValueKind} rather than as text."));
        }

        return value.GetString();
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
                    $"Row {position} gives its '{field}' as {value.ValueKind} rather than as a whole number this build can hold."));
        }

        return number;
    }

    private static string Spelling(DiscoverTitleKind kind) => kind switch
    {
        DiscoverTitleKind.Movie => "movie",
        DiscoverTitleKind.Series => "series",
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "A want carries the kind of the title it is about. None is what an unset field reads as, and the row refuses it before this is reached.")
    };

    private static string Spelling(LocalWantState state) => state switch
    {
        LocalWantState.Asked => "asked",
        LocalWantState.Withdrawn => "withdrawn",
        _ => throw new ArgumentOutOfRangeException(
            nameof(state),
            state,
            "A want is either standing or taken back. None is what an unset field reads as, and the row refuses it before this is reached.")
    };

    private static string Spelling(MetadataSource source) => source switch
    {
        MetadataSource.Imdb => "imdb",
        MetadataSource.Tmdb => "tmdb",
        MetadataSource.Tvdb => "tvdb",
        _ => throw new ArgumentOutOfRangeException(
            nameof(source),
            source,
            "An identifier names the source that supplied it. None is what an unset field reads as, and the identity refuses it before this is reached.")
    };
}
