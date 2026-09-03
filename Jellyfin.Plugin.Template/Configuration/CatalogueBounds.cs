using System;
using System.Globalization;

namespace Jellyfin.Plugin.Template.Configuration;

/// <summary>
/// The most this plugin may write into the operator's library database, as one
/// pair of numbers with the refusals that hold them together.
/// </summary>
/// <remarks>
/// #58 is what this exists for. Every title the surface returns becomes a row in
/// the library database, so a plugin with no bound turns that database into
/// somebody else's catalogue, and that is the concrete way this plugin damages a
/// server. The bound is therefore a designed number rather than whatever paging
/// default a source happens to answer with.
///
/// Two numbers rather than one, because they fail differently. A per-shelf bound
/// is what stops one shelf from being a source's whole catalogue. A bound across
/// all shelves is what stops six modest shelves from adding up to something an
/// operator did not agree to, and it is the one an operator reads when they want
/// to know what installing this costs them.
///
/// The type exists so that the numbers and the check on them are one thing,
/// which is the shape <c>CatalogueRetention</c> already takes for a source's
/// terms. A pair of bare integers on a configuration class, with nothing
/// comparing them against each other or against the set of shelves they have to
/// cover, is the failure this is written against: the combination that cannot be
/// satisfied compiles, reads plausibly, and is found at the moment a refresh
/// silently drops shelves rather than at the moment it was typed.
///
/// What this does not decide is what a row costs. How much disk the catalogue
/// takes and how much the library database grows per title is #71 and has not
/// been measured, so every number here is a count of rows and never a size.
/// </remarks>
public sealed class CatalogueBounds
{
    private CatalogueBounds(int titlesPerShelf, int titlesAcrossAllShelves)
    {
        TitlesPerShelf = titlesPerShelf;
        TitlesAcrossAllShelves = titlesAcrossAllShelves;
    }

    /// <summary>
    /// Gets the most titles one shelf may hold before an operator says otherwise.
    /// </summary>
    /// <remarks>
    /// Twenty, which is the one implemented source's own page size rather than a
    /// round number. The constant it follows is <c>PageSize</c>, which lives with
    /// that source's adapter, and <c>docs/configuration.md</c> pastes the command
    /// that reads it. The adapter is not named here: <c>docs/sources/tmdb.md</c>
    /// carries a search asserting that nothing in this plugin outside that file
    /// names its type, and #73's boundary is what that search is about.
    ///
    /// A shelf of at most one page is one request per refresh, and every twenty
    /// titles above that is another request against a budget this plugin does
    /// not own, which is #78. So the default is the largest shelf that costs the
    /// source nothing beyond the request a shelf costs anyway, and the next
    /// number up doubles the calls for a row a television remote has to scroll
    /// to reach.
    ///
    /// It is a default rather than a ceiling. An operator who wants forty pays
    /// two requests per shelf per refresh and says so themselves.
    /// </remarks>
    public static int DefaultTitlesPerShelf => 20;

    /// <summary>
    /// Gets the most titles the whole surface may hold before an operator says otherwise.
    /// </summary>
    /// <remarks>
    /// A hundred and twenty, which is the shipped set at the per-shelf default
    /// rather than a second number chosen on its own. Six shelves ship and each
    /// may hold twenty, so a first install that turns nothing off and changes
    /// nothing writes a hundred and twenty title rows and no more.
    ///
    /// The arithmetic is held rather than stated: <c>CatalogueBoundsTests</c>
    /// derives this value from the shipped set's own size at the per-shelf
    /// default, so a seventh shelf reddens here instead of quietly making the
    /// default configuration one this type refuses.
    ///
    /// A hundred and twenty is a row count and not a size. What a row costs on
    /// disk and in the library database is #71 and is unmeasured, so this
    /// default is defended against the request budget above and against nothing
    /// else.
    /// </remarks>
    public static int DefaultTitlesAcrossAllShelves => 120;

    /// <summary>
    /// Gets the most titles one shelf may hold.
    /// </summary>
    public int TitlesPerShelf { get; }

    /// <summary>
    /// Gets the most titles every shelf may hold between them.
    /// </summary>
    public int TitlesAcrossAllShelves { get; }

    /// <summary>
    /// Reads a configured pair, refusing one no set of shelves could satisfy.
    /// </summary>
    /// <param name="maximumTitlesPerShelf">The most titles one shelf may hold, as <see cref="PluginConfiguration.MaximumTitlesPerShelf"/> spells it.</param>
    /// <param name="maximumTitlesAcrossAllShelves">The most titles every shelf may hold between them, as <see cref="PluginConfiguration.MaximumTitlesAcrossAllShelves"/> spells it.</param>
    /// <returns>The bounds, checked against each other.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either number is zero or negative, or when the total is
    /// smaller than one shelf's own bound. The message names the setting as the
    /// configuration document spells it, the number that was offered and the
    /// range that is accepted, which is the rule <see cref="PluginConfiguration"/>
    /// states for every refusal of a setting. "Out of range" alone leaves an
    /// operator who typed one number guessing which of the pair objected, and a
    /// message naming this method's parameter leaves them searching the document
    /// for a word that is not in it.
    /// </exception>
    /// <remarks>
    /// Zero is refused rather than read as "hold nothing". A plugin that may
    /// hold nothing is one that is turned off, and turning it off is #109 rather
    /// than a bound of zero that leaves every shelf drawn and empty.
    ///
    /// A total below one shelf's bound is refused because it is the pair that
    /// cannot be satisfied by any set at all, including a set of one. It is also
    /// the ordinary typing mistake: lowering the total and forgetting the
    /// per-shelf number beside it.
    ///
    /// There is no ceiling of this type's own on either number. An operator who
    /// types a large total gets a large catalogue, which is their server and
    /// their decision; what this refuses is a pair that contradicts itself, and
    /// inventing a maximum here would be a number chosen by feel in the one
    /// place this issue asks for numbers that are not.
    ///
    /// The parameters are spelled as the settings are, so the parameter name the
    /// runtime appends to the message agrees with the setting the message opens
    /// with rather than offering a second word for the same thing.
    /// </remarks>
    public static CatalogueBounds Of(int maximumTitlesPerShelf, int maximumTitlesAcrossAllShelves)
    {
        if (maximumTitlesPerShelf <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumTitlesPerShelf),
                maximumTitlesPerShelf,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} is {1}, and it is how many titles one shelf may hold, so it is a count of one or more. Holding nothing is not spelled as a bound of zero: it is turning the shelf off.",
                    nameof(PluginConfiguration.MaximumTitlesPerShelf),
                    maximumTitlesPerShelf));
        }

        if (maximumTitlesAcrossAllShelves <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumTitlesAcrossAllShelves),
                maximumTitlesAcrossAllShelves,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} is {1}, and it is how many titles this plugin may write into the library database, so it is a count of one or more. Writing nothing is not spelled as a bound of zero: it is not configuring the plugin.",
                    nameof(PluginConfiguration.MaximumTitlesAcrossAllShelves),
                    maximumTitlesAcrossAllShelves));
        }

        if (maximumTitlesAcrossAllShelves < maximumTitlesPerShelf)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumTitlesAcrossAllShelves),
                maximumTitlesAcrossAllShelves,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} is {1}, which is smaller than {2} at {3}, so no set of shelves satisfies both. {0} is at least {2}: raise it, or lower {2}.",
                    nameof(PluginConfiguration.MaximumTitlesAcrossAllShelves),
                    maximumTitlesAcrossAllShelves,
                    nameof(PluginConfiguration.MaximumTitlesPerShelf),
                    maximumTitlesPerShelf));
        }

        return new CatalogueBounds(maximumTitlesPerShelf, maximumTitlesAcrossAllShelves);
    }

    /// <summary>
    /// Refuses a number of shelves this pair cannot cover.
    /// </summary>
    /// <param name="shelfCount">How many shelves are on.</param>
    /// <returns>The same bounds, so a caller can check and use them in one step.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the count is negative, which is a caller handing in a count
    /// no set of shelves has rather than a number an operator typed.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when that many shelves at the per-shelf bound would exceed the
    /// total. No argument of this call is wrong then: the count is the shipped
    /// set's own, and what does not fit is the pair an operator saved. So the
    /// refusal carries no parameter name, and it names the two settings as the
    /// document spells them with all four numbers, because the operator's next
    /// action is to change one of the two and they cannot choose which without
    /// seeing the arithmetic.
    /// </exception>
    /// <remarks>
    /// This is #58's third condition and the reason it is a refusal rather than
    /// a cap applied later. Truncating at refresh time gives an operator a
    /// surface whose last shelves are short or empty for a reason nothing on
    /// their screen explains, and it is indistinguishable from a source that
    /// answered with nothing, which is the state #92 has to read thinnest. A
    /// refusal at the moment the pair is read happens while the operator is
    /// still looking at the numbers they typed.
    ///
    /// The refusal stays in the <see cref="ArgumentException"/> family on
    /// purpose. The server answers a save with a status code chosen by the
    /// exception's type, and that family is the one it reads as the request
    /// being wrong rather than the server; the reading is in the change that
    /// made it an <see cref="ArgumentException"/> rather than restated here.
    ///
    /// The product is computed in <see cref="long"/> so that a large per-shelf
    /// bound and a large count are refused rather than wrapping into a small
    /// positive number that passes.
    /// </remarks>
    public CatalogueBounds ThrowIfShelvesDoNotFit(int shelfCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shelfCount, nameof(shelfCount));

        var wanted = (long)shelfCount * TitlesPerShelf;

        if (wanted > TitlesAcrossAllShelves)
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} shelves holding {1} titles each, which is {2} at {3}, is {4} titles, more than the {5} that {6} allows. Lower {2} or raise {6}.",
                    shelfCount,
                    TitlesPerShelf,
                    nameof(PluginConfiguration.MaximumTitlesPerShelf),
                    TitlesPerShelf,
                    wanted,
                    TitlesAcrossAllShelves,
                    nameof(PluginConfiguration.MaximumTitlesAcrossAllShelves)));
        }

        return this;
    }
}
