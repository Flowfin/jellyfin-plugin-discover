namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// What two identities say about each other when they are put side by side.
/// </summary>
/// <remarks>
/// Three outcomes and not two, because "these are not the same title" and "I
/// cannot tell" are different answers and collapsing them is how a catalogue
/// grows two records for one film and reports neither as odd.
/// </remarks>
public enum IdentityAgreement
{
    /// <summary>
    /// No comparison was made. What an unset field reads as, and never returned
    /// by <see cref="DiscoverTitleIdentity.Agrees(DiscoverTitleIdentity)"/>.
    /// </summary>
    None = 0,

    /// <summary>
    /// The two identities name at least one source in common and every source
    /// they have in common gives the same value, so they are one title.
    /// </summary>
    SameTitle = 1,

    /// <summary>
    /// The two identities name at least one source in common and that source
    /// gives them different values.
    /// </summary>
    /// <remarks>
    /// One of the two is wrong and nothing here can say which, so they are kept
    /// apart rather than merged. Merging on the sources that do agree would
    /// take the contradiction into a single record where no later reader can
    /// see it.
    /// </remarks>
    Contradiction = 2,

    /// <summary>
    /// The two identities name no source in common, so nothing about them can
    /// be compared.
    /// </summary>
    /// <remarks>
    /// Not the same as being different titles. Two responses about one film,
    /// one carrying only an IMDb identifier and the other only a TMDB one, land
    /// here, and the answer is that neither this record nor this plugin knows.
    /// </remarks>
    NotComparable = 3
}
