using System;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Seam;

/// <summary>
/// One user's want, as the one message that crosses the seam to a requests plugin.
/// </summary>
/// <remarks>
/// The field set is fixed in
/// `docs/decisions/0004-what-crosses-the-seam-to-a-requests-plugin.md` and this
/// type is that list expressed once. Eight fields cross and the rest of the
/// catalogue record does not: the summary and the artwork location are the
/// source's content and stay here, the original-language name is re-resolved by
/// a receiver from the identifiers, and the catalogue record's own schema
/// version is about this plugin's storage rather than about the seam.
///
/// Absence is absence. A field the source gave nothing for is left null rather
/// than sent as an empty string or a zero, which is the rule
/// <see cref="DiscoverTitle"/> holds and holds for the same reason.
///
/// Nothing here derives the want identifier. Where it comes from and why it has
/// to be stable across a refresh that recreated an item is #99; this record
/// carries what it was given and refuses a blank one, so a caller that has not
/// solved that yet fails here rather than handing a receiver a value that means
/// something different next week.
///
/// What produces a want at all is the gesture in #96, and who may produce one is
/// #98. Neither exists, so today this record is built by the suite and by
/// nothing else.
/// </remarks>
public sealed record Want
{
    private readonly DiscoverTitleIdentity _identity = null!;
    private readonly DiscoverTitleKind _kind;
    private readonly string _name = null!;
    private readonly int? _releaseYear;
    private readonly Guid _askingUser;
    private readonly string _wantIdentifier = null!;
    private readonly bool? _replay;
    private readonly int _contractVersion = WantContract.CurrentVersion;

    /// <summary>
    /// Gets which version of the handover contract this message is written to.
    /// </summary>
    /// <remarks>
    /// Comes from this build unless a caller supplied it. A receiver reads it
    /// first and refuses a number it does not know rather than reading the
    /// fields it recognises, because the reason the number moved is that one of
    /// those fields no longer means what it did.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is below one. The contract version starts at one
    /// and only grows, so zero is what an unset field reads as and a receiver
    /// meeting it would have no way to tell it from a version it predates.
    /// </exception>
    public int ContractVersion
    {
        get => _contractVersion;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A want carries the contract version it was written to. Zero is what an unset field reads as, and the numbering starts at one.");
            }

            _contractVersion = value;
        }
    }

    /// <summary>
    /// Gets the provider identifiers a receiver resolves the title from. Never absent.
    /// </summary>
    /// <remarks>
    /// The whole input a receiver needs to find the title for itself, which is
    /// why the identity crosses rather than a display name. It is the strongest
    /// form of the version rule: a release that changes what these are scoped to
    /// or how they are computed is a breaking change to the contract.
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
    /// Gets whether the want is for a film or for a series. Never absent.
    /// </summary>
    /// <remarks>
    /// A receiver acts differently on the two, and inferring it from whether a
    /// year is present is a guess. It is the second of the three fields a
    /// receiver keys on.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is <see cref="DiscoverTitleKind.None"/>, which is
    /// what an unset field reads as. A receiver meeting it would act on
    /// whichever of the two its own default is.
    /// </exception>
    public required DiscoverTitleKind Kind
    {
        get => _kind;
        init
        {
            if (value == DiscoverTitleKind.None)
            {
                throw new ArgumentException(
                    "A want names the kind of title it is for. None is what an unset field reads as, and a receiver holding it would act on whichever kind its own default is.",
                    nameof(value));
            }

            _kind = value;
        }
    }

    /// <summary>
    /// Gets the title as the source gave it. Never absent.
    /// </summary>
    /// <remarks>
    /// So a person reading a request list recognises what they asked for without
    /// a lookup. It is not what a receiver resolves the title by, and a receiver
    /// matching on it rather than on the identifiers is doing the thing
    /// <see cref="DiscoverTitleIdentity"/> exists to make unnecessary.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is null, empty or whitespace. A want with a blank
    /// name reads on a request list as a row nobody can act on.
    /// </exception>
    public required string Name
    {
        get => _name;
        init
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A want carries the title as the source spelled it. A blank one leaves a request list holding a row a person cannot recognise.",
                    nameof(value));
            }

            _name = value;
        }
    }

    /// <summary>
    /// Gets the release year, or null where the source gave none.
    /// </summary>
    /// <remarks>
    /// The same reason as the name: two films share a name often enough that the
    /// name alone is not recognition. Null rather than zero, because a source
    /// that returned no year is a different thing from one that returned the
    /// year zero, and only one of them is worth asking again about.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is present and below one. Absence is expressed by
    /// leaving it null, so a zero here is an unset field wearing the shape of an
    /// answer.
    /// </exception>
    public int? ReleaseYear
    {
        get => _releaseYear;
        init
        {
            if (value is not null && value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A want with no release year leaves it absent. A zero is what an unset field reads as and a receiver cannot tell it from a year.");
            }

            _releaseYear = value;
        }
    }

    /// <summary>
    /// Gets the server's identifier for the user who made the gesture. Never absent.
    /// </summary>
    /// <remarks>
    /// A request belongs to somebody, and a receiver cannot ask this plugin
    /// later because there is no route back. The server's own user identifier
    /// rather than anything this plugin mints, so a receiver and this plugin
    /// mean one person by it without agreeing on a second vocabulary.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is <see cref="Guid.Empty"/>, which is what an unset
    /// field reads as. Every want carrying it would be one anonymous user's.
    /// </exception>
    public required Guid AskingUser
    {
        get => _askingUser;
        init
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A want belongs to the user who asked for it. Guid.Empty is what an unset field reads as, and every want carrying it would read as one person's.",
                    nameof(value));
            }

            _askingUser = value;
        }
    }

    /// <summary>
    /// Gets this plugin's own identifier for this want. Never absent.
    /// </summary>
    /// <remarks>
    /// So a receiver can tell a repeat of one want from two wants, and so the
    /// same want handed over twice is one thing. It is the third of the fields a
    /// receiver keys on and it carries the strongest form of the version rule: a
    /// receiver that stores it has made it part of the contract whether or not
    /// anybody said so, so a release that recomputes it is breaking.
    ///
    /// A string rather than a number or a drawn identifier, because #99 asks for
    /// it to be derived from the title identity and the user and to be stable
    /// across refreshes and restarts. <see cref="WantIdentifiers.For"/> is that
    /// derivation and the decision note carries its shape; this type refuses a
    /// blank one and stores what it was handed, so a caller deriving one another
    /// way is not stopped here.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is null, empty or whitespace. Two blank identifiers
    /// would be one want to any receiver that keys on them.
    /// </exception>
    public required string WantIdentifier
    {
        get => _wantIdentifier;
        init
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A want carries this plugin's own identifier for it. Two blank ones are one want to any receiver that keys on them, which is the collapse #99 exists against.",
                    nameof(value));
            }

            _wantIdentifier = value;
        }
    }

    /// <summary>
    /// Gets <see langword="true"/> where this want is being replayed from a
    /// record made earlier, and null where it is live.
    /// </summary>
    /// <remarks>
    /// The eighth field, decided on #335 against requests#93: a sibling that was
    /// installed after this plugin had already been collecting wants replays the
    /// list it holds through the same handover a live gesture takes, and a
    /// receiver that cannot tell the two apart shows an operator a sudden queue
    /// with no account of where it came from.
    ///
    /// A field a receiver may ignore, so the contract version does not move for
    /// it. Absence is what an older build writes and absence means live, which
    /// is why the marker says the unusual thing rather than the ordinary one:
    /// the reverse spelling would make every want a build without this field
    /// hands over read as a replay.
    ///
    /// Nothing in this tree replays anything yet. The local list is #97 and the
    /// gesture that produces a live want at all is #96, so today this field is
    /// set by the suite and by nothing else, exactly as the rest of this record
    /// is.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is <see langword="false"/>. Absence already means
    /// live, so a false is a second spelling of it and it is the value an unset
    /// field reads as: a receiver meeting one would have to decide whether the
    /// sender meant live or meant nothing, and those are the same want.
    /// </exception>
    public bool? Replay
    {
        get => _replay;
        init
        {
            if (value == false)
            {
                throw new ArgumentException(
                    "A want that is not a replay leaves this absent. False is what an unset field reads as and it is a second spelling of live, which is what absence already says.",
                    nameof(value));
            }

            _replay = value;
        }
    }
}
