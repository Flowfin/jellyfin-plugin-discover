using System;

namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// One title the server does not have, as one record.
/// </summary>
/// <remarks>
/// The same shape for all three of the places that would otherwise grow their
/// own: what a source adapter returns (#73), what the surface draws from (#55),
/// and what crosses the seam to a requests plugin (#94). Three types with
/// converters between them means every field added later is added three times,
/// and the converters are where the drift lives.
///
/// Nothing here is a string this plugin composed. There is no display name
/// built out of a title and a year, no sort key and no slug, because a
/// composed string is one a later comparison has to take apart again, and
/// taking it apart is where a title called "Arrival (2016)" becomes a title
/// called "Arrival" in one place and not in another. A client draws what it
/// wants out of the fields below.
///
/// Absence is a null and never an empty string or a zero. A source that
/// returned no year is a different thing from a source that returned the year
/// zero, and only one of them is worth asking again about.
/// </remarks>
public sealed record DiscoverTitle
{
    /// <summary>
    /// The schema version this build writes, and the only one it is able to read.
    /// </summary>
    /// <remarks>
    /// Carried from this record's first commit rather than added when the first
    /// migration is needed, for the reason
    /// <see cref="Configuration.PluginConfiguration.CurrentSchemaVersion"/>
    /// carries one: a record written before there is a version rule is a record
    /// nobody can migrate afterwards. What a stored catalogue does with it is
    /// #67, and this record is the thing that gives #67 something to switch on.
    /// </remarks>
    public const int CurrentSchemaVersion = 1;

    private readonly DiscoverTitleIdentity _identity = null!;
    private readonly DiscoverTitleKind _kind;
    private readonly string _name = null!;
    private readonly Uri? _artworkLocation;

    /// <summary>
    /// Gets the version of this record's shape.
    /// </summary>
    /// <remarks>
    /// Comes from this build unless a reader supplied it, which is what a
    /// catalogue written by another build does.
    /// </remarks>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// Gets what makes this that title. Never absent.
    /// </summary>
    /// <remarks>
    /// Comes from the identifiers in the source's response. A title a source
    /// gave no identifier for cannot be stored, cannot be compared with the
    /// library and cannot be handed over the seam, so it is dropped by the
    /// adapter rather than carried as a record with nothing to identify it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public required DiscoverTitleIdentity Identity
    {
        get => _identity;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _identity = value;
        }
    }

    /// <summary>
    /// Gets what sort of thing this is. Never absent.
    /// </summary>
    /// <remarks>
    /// Comes from the source's own type, mapped by the adapter. A response
    /// whose type this plugin does not recognise is not a title of an unknown
    /// kind; it is a title this plugin does not carry, and the adapter drops it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is <see cref="DiscoverTitleKind.None"/>, which is
    /// what an unset field reads as, or is not a declared kind.
    /// </exception>
    public required DiscoverTitleKind Kind
    {
        get => _kind;
        init
        {
            if (value is not (DiscoverTitleKind.Movie or DiscoverTitleKind.Series))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A discover title carries the kind the adapter mapped it to. None is what an unset field reads as, and a record holding it would be drawn by every client as whatever its default is.");
            }

            _kind = value;
        }
    }

    /// <summary>
    /// Gets the title as the source spells it. Never absent.
    /// </summary>
    /// <remarks>
    /// Comes from the source, in the language and region the server asked for,
    /// which is #81. It is what a client shows and it is not part of identity:
    /// asking one source twice in two languages returns two names for one
    /// title, and both records are the same title.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is null, empty or whitespace. There is no such
    /// thing as a title with no name in a source's response, so a blank one is
    /// a parse fault rather than an absence.
    /// </exception>
    public required string Name
    {
        get => _name;
        init
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A discover title with a blank name would be drawn as an empty row that a user cannot tell from a broken one.",
                    nameof(value));
            }

            _name = value;
        }
    }

    /// <summary>
    /// Gets the title in the source's own original language, or null where the source gave none.
    /// </summary>
    /// <remarks>
    /// Comes from the source. May be absent: a source has one only where it
    /// knows the original language differs from the one asked for. It is here
    /// so that a client or an operator can tell two translations of one film
    /// apart without this plugin composing a string that holds both.
    /// </remarks>
    public string? OriginalName { get; init; }

    /// <summary>
    /// Gets the year the source gives for this title, or null where it gives none.
    /// </summary>
    /// <remarks>
    /// Comes from the source. May be absent, and often is for a title that has
    /// been announced and not released, which is exactly the sort of title a
    /// discover page exists to show. A year rather than a date, because the
    /// sources disagree about which date a release is and the day is not
    /// something a shelf shows.
    /// </remarks>
    public int? ReleaseYear { get; init; }

    /// <summary>
    /// Gets the source's description of this title, or null where it gives none.
    /// </summary>
    /// <remarks>
    /// Comes from the source and is the source's content, so it is subject to
    /// what may be passed on, which is #82. May be absent, and an empty one
    /// from the source is stored as absent rather than as an empty string, so
    /// a client is not asked to draw an empty panel.
    /// </remarks>
    public string? Summary { get; init; }

    /// <summary>
    /// Gets where the source's own artwork for this title is, or null where there is none.
    /// </summary>
    /// <remarks>
    /// A location at the source rather than a copy. #62 is where that is
    /// argued: this plugin never holds artwork, so how long it may keep it and
    /// whether it may pass it on do not arise. May be absent, and a title with
    /// no artwork produces an item with none rather than a broken location.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is a relative location. A relative one would be
    /// resolved against whatever host happened to ask for it, which is not the
    /// source's.
    /// </exception>
    public Uri? ArtworkLocation
    {
        get => _artworkLocation;
        init
        {
            if (value is not null && !value.IsAbsoluteUri)
            {
                throw new ArgumentException(
                    $"Artwork is referenced where the source keeps it, so the location has to say which host that is. '{value}' does not.",
                    nameof(value));
            }

            _artworkLocation = value;
        }
    }
}
