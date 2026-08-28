using System;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Seam;

namespace Jellyfin.Plugin.Template.Wants;

/// <summary>
/// One row of what this server holds about a want, whoever else was told about it.
/// </summary>
/// <remarks>
/// #97 is what this exists for, and the first thing to say about it is what it
/// is not. <see cref="Want"/> is the message that crosses the seam: one moment,
/// stamped with the contract version it was written at, by #94 and #95. This is
/// a standing state that outlives a handover, has to survive a sink that
/// refused, and has to represent a withdrawal the message has no field for. The
/// cheap move is to store the message, and it produces a list that is wrong in
/// all three of those ways.
///
/// What it holds about a person is one value, the server's own identifier for
/// the user who asked. Not a user name: a name is a second copy of something
/// the server owns, it goes stale on a rename, and it is more than the feature
/// needs, because a page can resolve a name at the moment it draws one. It also
/// makes removal with the user a deletion keyed on something stable, which is
/// #97's third condition and #70's register. Nothing else here refers to a
/// person.
///
/// The title's own fields are carried rather than pointed at. A want outlives
/// the catalogue that produced it - the catalogue has a retention, #68, and the
/// operator's list does not - so a row holding only an identity would show an
/// operator a row it could no longer name.
/// </remarks>
public sealed record LocalWant
{
    private readonly string _wantIdentifier = null!;
    private readonly DiscoverTitleIdentity _identity = null!;
    private readonly DiscoverTitleKind _kind;
    private readonly string _name = null!;
    private readonly Guid _askingUser;
    private readonly LocalWantState _state;

    /// <summary>
    /// Gets this plugin's own identifier for the want this row is about. Never absent.
    /// </summary>
    /// <remarks>
    /// The key of the register, and it is the same value the seam carries, from
    /// <see cref="WantIdentifiers.For"/> by #99. Two properties come from that
    /// rather than from anything decided here: the same title wanted by the same
    /// user is one row across refreshes and restarts, and two users wanting one
    /// title are two rows, because the user is inside the value rather than in a
    /// field beside it.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is null, empty or whitespace. Two blank identifiers
    /// would be one row to anything that keys on them.
    /// </exception>
    public required string WantIdentifier
    {
        get => _wantIdentifier;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _wantIdentifier = value;
        }
    }

    /// <summary>
    /// Gets the identifiers of the title that was wanted. Never absent.
    /// </summary>
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
    /// Gets which sort of title was wanted.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is the unset member.
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
                    "A recorded want is about a film or a series. None is what an unset field reads as.");
            }

            _kind = value;
        }
    }

    /// <summary>
    /// Gets what the title is called, as the source gave it. Never absent.
    /// </summary>
    /// <remarks>
    /// Carried so the operator's list can still be read after the catalogue that
    /// produced the row has been thrown away, on demand or on retention.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is null, empty or whitespace.
    /// </exception>
    public required string Name
    {
        get => _name;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _name = value;
        }
    }

    /// <summary>
    /// Gets the year the title was released, or null where the source gave none.
    /// </summary>
    /// <remarks>
    /// Null rather than a zero or a guess, for the reason
    /// <see cref="DiscoverTitle"/> gives: a missing year is missing, and a year
    /// this plugin invented is one a client draws as a fact.
    /// </remarks>
    public int? ReleaseYear { get; init; }

    /// <summary>
    /// Gets the server's identifier for the user who asked. Never absent.
    /// </summary>
    /// <remarks>
    /// The only thing on this record that refers to a person, which is the whole
    /// of what #97's third condition allows and what #70's register takes as its
    /// entry. Removing a user from the server removes their rows by this value.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is <see cref="Guid.Empty"/>, which is what an unset
    /// field reads as. Every row carrying it would be one anonymous user's.
    /// </exception>
    public required Guid AskingUser
    {
        get => _askingUser;
        init
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A recorded want belongs to the user who asked for it. Guid.Empty is what an unset field reads as, and every row carrying it would read as one person's.",
                    nameof(value));
            }

            _askingUser = value;
        }
    }

    /// <summary>
    /// Gets when the want was first recorded.
    /// </summary>
    /// <remarks>
    /// Read from the injected clock by whatever records the row, never from the
    /// machine, which <c>no-wall-clock</c> refuses outside the one clock. It is
    /// the first asking rather than the most recent: a row that moved its date
    /// every time somebody re-asked would tell an operator that an old request
    /// is new.
    /// </remarks>
    public required DateTimeOffset AskedAt { get; init; }

    /// <summary>
    /// Gets where this want stands.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is <see cref="LocalWantState.None"/>.
    /// </exception>
    public required LocalWantState State
    {
        get => _state;
        init
        {
            if (value is LocalWantState.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A recorded want stands somewhere. None is what an unset field reads as, and a row in it would be a want nobody asked for.");
            }

            _state = value;
        }
    }

    /// <summary>
    /// Gets when the want was withdrawn, or null while it stands.
    /// </summary>
    /// <remarks>
    /// Null and a date are the two states, and they are kept in step with
    /// <see cref="State"/> by the makers below rather than by a check here:
    /// nothing outside this type sets one without the other.
    /// </remarks>
    public DateTimeOffset? WithdrawnAt { get; init; }

    /// <summary>
    /// The row a gesture produces.
    /// </summary>
    /// <param name="want">The want as it crosses the seam.</param>
    /// <param name="at">The moment, from the clock this plugin was given.</param>
    /// <returns>A standing row.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="want"/> is null.</exception>
    /// <remarks>
    /// Built from the message rather than beside it, so the six fields the two
    /// share cannot disagree about one want. What is deliberately not taken from
    /// the message is its contract version: this row is not a copy of what was
    /// sent, and a version on it would be read as one.
    /// </remarks>
    public static LocalWant From(Want want, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(want);

        return new LocalWant
        {
            WantIdentifier = want.WantIdentifier,
            Identity = want.Identity,
            Kind = want.Kind,
            Name = want.Name,
            ReleaseYear = want.ReleaseYear,
            AskingUser = want.AskingUser,
            AskedAt = at,
            State = LocalWantState.Asked
        };
    }

    /// <summary>
    /// The same row, taken back.
    /// </summary>
    /// <param name="at">The moment, from the clock this plugin was given.</param>
    /// <returns>The row in <see cref="LocalWantState.Withdrawn"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the moment is before <see cref="AskedAt"/>. A withdrawal that
    /// preceded the asking is a clock that was wound back, and the row it would
    /// produce cannot be read in order.
    /// </exception>
    /// <remarks>
    /// A new row rather than a mutation, because this is a record and the
    /// register replaces what it holds. <see cref="AskedAt"/> is carried
    /// unchanged: when somebody asked is not altered by their taking it back.
    /// </remarks>
    public LocalWant Withdrawn(DateTimeOffset at)
    {
        if (at < AskedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(at),
                at,
                "A want is withdrawn after it was asked for. An earlier moment is a clock that moved backwards, and the row it would produce cannot be read in order.");
        }

        return this with { State = LocalWantState.Withdrawn, WithdrawnAt = at };
    }

    /// <summary>
    /// The same row, asked for again after a withdrawal.
    /// </summary>
    /// <returns>The row in <see cref="LocalWantState.Asked"/>.</returns>
    /// <remarks>
    /// The withdrawal date goes rather than accumulating, and
    /// <see cref="AskedAt"/> stays at the first asking. What this list is for is
    /// telling an operator who asked for what and when they first did; a history
    /// of every time somebody toggled a flag is more than the feature needs and
    /// is more about the person than #70 admits.
    /// </remarks>
    public LocalWant Reasked() =>
        State is LocalWantState.Asked ? this : this with { State = LocalWantState.Asked, WithdrawnAt = null };
}
