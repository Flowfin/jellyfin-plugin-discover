using System;
using System.Globalization;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Seam;

/// <summary>
/// This plugin's own identifier for one want, derived rather than drawn.
/// </summary>
/// <remarks>
/// #99 is what this exists for. Two users want the same film; one user undoes
/// the gesture and makes it again; a refresh recreates the item and the gesture
/// is seen a second time. None of those may become two acquisitions on the far
/// side of the seam, and this plugin is the only one that can tell them apart,
/// because a receiver sees only what it was handed.
///
/// Derived from the title identity and the user, and from nothing else. Not
/// drawn, because a drawn identifier is a new one every time the thing that
/// drew it restarts, and not counted, because a counter is state this plugin
/// would have to keep correct across a restart in order to be correct at all.
/// A derivation keeps no state, so the same title wanted by the same user is
/// one identifier on every run of every build that derives it this way.
///
/// The value's shape is part of the contract rather than an implementation
/// detail, which
/// `docs/decisions/0004-what-crosses-the-seam-to-a-requests-plugin.md` states in
/// as many words: a receiver that stores this has made it part of the contract
/// whether or not anybody said so, and a release that recomputes it is
/// breaking. So the shape is written down here and there rather than left to be
/// read off this method.
///
/// <c>&lt;source&gt;:&lt;user&gt;:&lt;identifier&gt;</c>, in that order. The
/// first two cannot contain the separator - one is a
/// <see cref="MetadataSource"/> member's name and the other is a
/// <see cref="Guid"/> written as thirty-two hexadecimal digits - and the third
/// is everything after the second separator. So the three parts are recoverable
/// from the whole whatever a source spells its identifier with, which matters
/// because <see cref="ProviderIdentifier"/> keeps a value exactly as the source
/// gave it and normalises nothing.
///
/// The free-form part is last so that it is the remainder rather than something
/// to be parsed out of the middle. That is a property of this shape rather than
/// the only shape with it: with one free-form field and two that cannot carry
/// the separator, the other orders are recoverable too, by reading from the
/// right. What is load bearing is that the value is neither split nor shortened
/// on the way in, and that is what the tests refuse.
/// </remarks>
public static class WantIdentifiers
{
    private const string Separator = ":";

    /// <summary>
    /// Derives the identifier for one user wanting one title.
    /// </summary>
    /// <param name="identity">The title's identity, from the catalogue record.</param>
    /// <param name="askingUser">The server's identifier for the user who made the gesture.</param>
    /// <returns>The identifier to put on the want that crosses the seam.</returns>
    /// <remarks>
    /// <see cref="DiscoverTitleIdentity.Primary"/> rather than the whole set,
    /// and that is the choice this method turns on.
    ///
    /// A derivation over the whole set moves whenever the set moves at all, so
    /// a refresh whose response carried one identifier more than the last one
    /// produces a second want for a title nobody asked for twice. The primary
    /// moves only when a higher-precedence identifier arrives, which is strictly
    /// less often.
    ///
    /// What makes that the right side of the trade rather than merely the
    /// cheaper one is that the server's own item identity is derived from the
    /// same primary, which is #60. So the moment this identifier moves is the
    /// moment the item a user was looking at is a different item, and a second
    /// want for it is what a second item means. The residual is disclosed rather
    /// than removed: an identity that gains an identifier the precedence puts
    /// first does change this value, and
    /// <see cref="DiscoverTitleIdentity.Primary"/> names that trap as #60's.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="askingUser"/> is <see cref="Guid.Empty"/>,
    /// which is what an unset field reads as. Every want derived from it would
    /// be one anonymous user's, which is the collapse this method exists
    /// against, and <see cref="Want.AskingUser"/> refuses the same value for the
    /// same reason.
    /// </exception>
    public static string For(DiscoverTitleIdentity identity, Guid askingUser)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (askingUser == Guid.Empty)
        {
            throw new ArgumentException(
                "A want belongs to the user who asked for it. Guid.Empty is what an unset field reads as, and every want derived from it would carry one identifier, which is two users collapsing into one want.",
                nameof(askingUser));
        }

        var primary = identity.Primary;

        return string.Concat(
            primary.Source.ToString(),
            Separator,
            askingUser.ToString("N", CultureInfo.InvariantCulture),
            Separator,
            primary.Value);
    }
}
