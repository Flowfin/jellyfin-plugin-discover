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
    /// Twenty, which is the one source's page size rather than a round number.
    /// The constant it follows is <c>PageSize</c> in <c>TmdbSourceAdapter</c>.
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
    /// <param name="titlesPerShelf">The most titles one shelf may hold.</param>
    /// <param name="titlesAcrossAllShelves">The most titles every shelf may hold between them.</param>
    /// <returns>The bounds, checked against each other.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either number is zero or negative, or when the total is
    /// smaller than one shelf's own bound. The message says which of the two is
    /// being refused and against what, because "out of range" leaves an operator
    /// who typed one number guessing which of the pair objected.
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
    /// </remarks>
    public static CatalogueBounds Of(int titlesPerShelf, int titlesAcrossAllShelves)
    {
        if (titlesPerShelf <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(titlesPerShelf),
                titlesPerShelf,
                "A shelf's bound is how many titles it may hold, so it is a positive count. Holding nothing is not spelled as a bound of zero: it is turning the shelf off.");
        }

        if (titlesAcrossAllShelves <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(titlesAcrossAllShelves),
                titlesAcrossAllShelves,
                "The bound across all shelves is how many titles this plugin may write into the library database, so it is a positive count. Writing nothing is not spelled as a bound of zero: it is not configuring the plugin.");
        }

        if (titlesAcrossAllShelves < titlesPerShelf)
        {
            throw new ArgumentOutOfRangeException(
                nameof(titlesAcrossAllShelves),
                titlesAcrossAllShelves,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "A total of {0} titles is smaller than the {1} one shelf may hold, so no set of shelves satisfies both. Raise the total or lower the per-shelf bound.",
                    titlesAcrossAllShelves,
                    titlesPerShelf));
        }

        return new CatalogueBounds(titlesPerShelf, titlesAcrossAllShelves);
    }

    /// <summary>
    /// Refuses a number of shelves this pair cannot cover.
    /// </summary>
    /// <param name="shelfCount">How many shelves are on.</param>
    /// <returns>The same bounds, so a caller can check and use them in one step.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the count is negative, or when that many shelves at the
    /// per-shelf bound would exceed the total. The message carries all four
    /// numbers, because the operator's next action is to change one of them and
    /// they cannot choose which without seeing the arithmetic.
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
            throw new ArgumentOutOfRangeException(
                nameof(shelfCount),
                shelfCount,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} shelves holding {1} titles each is {2} titles, which is more than the {3} allowed across all shelves. Turn a shelf off, lower the per-shelf bound, or raise the total.",
                    shelfCount,
                    TitlesPerShelf,
                    wanted,
                    TitlesAcrossAllShelves));
        }

        return this;
    }
}
