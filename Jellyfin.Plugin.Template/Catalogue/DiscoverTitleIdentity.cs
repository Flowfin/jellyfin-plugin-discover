using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// What makes a discover title that title and not another one.
/// </summary>
/// <remarks>
/// The set of provider identifiers a source supplied for it, and nothing else.
/// Not the name: names are not unique, they are translated, and the same film
/// arrives under two of them from one source asked in two languages.
///
/// Two identities are equal when they hold the same set, which is an ordinary
/// equivalence and can therefore sit in a dictionary or a set. The other
/// question, whether two identities that hold different sets are nonetheless
/// one title, is deliberately not equality:
/// <see cref="Agrees(DiscoverTitleIdentity)"/> answers it and returns three
/// outcomes. It is not transitive, so putting it behind
/// <see cref="object.Equals(object)"/> would break every collection that ever
/// held one of these.
/// </remarks>
public sealed class DiscoverTitleIdentity : IEquatable<DiscoverTitleIdentity>
{
    private static readonly MetadataSource[] _precedence =
    {
        MetadataSource.Imdb,
        MetadataSource.Tmdb,
        MetadataSource.Tvdb
    };

    private readonly ProviderIdentifier[] _identifiers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoverTitleIdentity"/> class.
    /// </summary>
    /// <param name="identifiers">
    /// The identifiers a source supplied. Order does not matter and a source
    /// repeating one identifier with the same value is not an error.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifiers"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the set is empty, when an identifier names no source or has
    /// a blank value, or when one source appears twice with two values. An
    /// identity with nothing in it would compare equal to every other empty
    /// one, and a source contradicting itself inside one response is a fault in
    /// the response rather than a title with two identifiers.
    /// </exception>
    public DiscoverTitleIdentity(IEnumerable<ProviderIdentifier> identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);

        var bySource = new SortedDictionary<MetadataSource, string>();

        foreach (var candidate in identifiers)
        {
            var identifier = candidate.Validated();

            if (bySource.TryGetValue(identifier.Source, out var already))
            {
                if (!string.Equals(already, identifier.Value, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"{identifier.Source} was given twice, as '{already}' and as '{identifier.Value}'. One source cannot hold two identifiers for one title, so this is a response that disagrees with itself.",
                        nameof(identifiers));
                }

                continue;
            }

            bySource.Add(identifier.Source, identifier.Value);
        }

        if (bySource.Count == 0)
        {
            throw new ArgumentException(
                "A discover title with no provider identifier cannot be told apart from any other, and it cannot be handed to anything that would resolve it. A title a source supplied no identifier for is dropped rather than stored.",
                nameof(identifiers));
        }

        _identifiers = new ProviderIdentifier[bySource.Count];

        var next = 0;
        foreach (var pair in bySource)
        {
            _identifiers[next++] = new ProviderIdentifier(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// Gets the order in which a source's identifier is preferred over another's.
    /// </summary>
    /// <remarks>
    /// IMDb first, because its identifiers are issued by none of the sources
    /// this plugin fetches from and are carried by all of them, so a primary
    /// chosen this way survives the day a source is replaced. A primary that
    /// was the fetching source's own identifier would move every title's
    /// identity when the source behind a shelf changed, which is the orphaning
    /// #60 is about. TMDB next, as the source #74 puts behind the first
    /// adapter, and TheTVDB last, as the one #83 refused as a source. Its
    /// identifiers still arrive, from another source's answer and from the
    /// server's own library, so it keeps a place in the order.
    /// </remarks>
    public static IReadOnlyList<MetadataSource> Precedence => _precedence;

    /// <summary>
    /// Gets the identifiers this identity holds, ordered by source.
    /// </summary>
    /// <remarks>
    /// The order is this type's own and is not the order a response listed
    /// them in, so two responses listing one title's identifiers differently
    /// produce one identity rather than two.
    /// </remarks>
    public IReadOnlyList<ProviderIdentifier> Identifiers => _identifiers;

    /// <summary>
    /// Gets the identifier that stands for this title where exactly one is needed.
    /// </summary>
    /// <remarks>
    /// The highest-precedence identifier this identity actually holds. What
    /// needs one rather than the set is the external identifier the server
    /// hashes an item's own identity out of, which is #60.
    ///
    /// There is a trap in it that belongs to #60 rather than to this type: an
    /// identity that later gains an identifier of higher precedence has a
    /// different primary, so anything that derived a stored value from the
    /// primary has to pin what it derived at first sight rather than
    /// recomputing it.
    /// </remarks>
    public ProviderIdentifier Primary
    {
        get
        {
            foreach (var source in _precedence)
            {
                foreach (var identifier in _identifiers)
                {
                    if (identifier.Source == source)
                    {
                        return identifier;
                    }
                }
            }

            // Unreachable while the constructor refuses an empty set and every
            // MetadataSource other than None is in _precedence. Stated as a
            // throw rather than a comment, because the day a source is added to
            // the enum and not to the precedence list, this is the line that
            // says so instead of a title silently having no primary.
            throw new InvalidOperationException(
                $"No identifier in this identity names a source in {nameof(Precedence)}. A source was added to {nameof(MetadataSource)} without being given a position there.");
        }
    }

    /// <summary>
    /// Says whether two identities are one title, contradict each other, or cannot be compared.
    /// </summary>
    /// <param name="other">The identity to compare with.</param>
    /// <returns>
    /// <see cref="IdentityAgreement.SameTitle"/> when they name at least one
    /// source in common and every common source agrees,
    /// <see cref="IdentityAgreement.Contradiction"/> when a common source gives
    /// two values, and <see cref="IdentityAgreement.NotComparable"/> when they
    /// have no source in common.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    public IdentityAgreement Agrees(DiscoverTitleIdentity other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var shared = 0;

        foreach (var mine in _identifiers)
        {
            foreach (var theirs in other._identifiers)
            {
                if (mine.Source != theirs.Source)
                {
                    continue;
                }

                shared++;

                if (!string.Equals(mine.Value, theirs.Value, StringComparison.Ordinal))
                {
                    return IdentityAgreement.Contradiction;
                }
            }
        }

        return shared == 0 ? IdentityAgreement.NotComparable : IdentityAgreement.SameTitle;
    }

    /// <inheritdoc />
    public bool Equals(DiscoverTitleIdentity? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (_identifiers.Length != other._identifiers.Length)
        {
            return false;
        }

        for (var index = 0; index < _identifiers.Length; index++)
        {
            if (!_identifiers[index].Equals(other._identifiers[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DiscoverTitleIdentity);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = default(HashCode);

        foreach (var identifier in _identifiers)
        {
            hash.Add(identifier);
        }

        return hash.ToHashCode();
    }
}
