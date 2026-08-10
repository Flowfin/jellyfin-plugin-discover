using System;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Sources;

/// <summary>
/// One question put to one source, in this plugin's own words.
/// </summary>
/// <remarks>
/// A named question rather than a description of how to ask it. Which titles
/// "popular this week" means is the source's business, and every source spells
/// the request for it differently: a path, a parameter set, a sort field, a
/// discovery endpoint with filters. If any of that were here, adding a second
/// source would mean changing the first one's callers, which is what #73 exists
/// against.
///
/// The name is opaque to this type on purpose. What names are legitimate is the
/// shelf set in #86, and an adapter that is handed a name it has no question for
/// answers <see cref="SourceOutcome.NotConfigured"/> rather than guessing.
///
/// Two things a reader may expect here and will not find. There is no language
/// or region: where those come from is #81's decision and it is the server's
/// answer rather than the shelf's, so putting a field here first would fix the
/// wrong origin. And there is no sort: the order a shelf is drawn in is this
/// plugin's, per #91, and asking a source to sort would make a shelf's order a
/// property of whichever source filled it.
/// </remarks>
/// <param name="Name">
/// What is being asked for, in the shelf's vocabulary rather than the source's.
/// </param>
/// <param name="Kind">
/// Which sort of title is wanted. Carried rather than left to the name, because
/// most sources ask for films and series through different questions and an
/// adapter that had to parse the kind back out of a name would be reading a
/// composed string, which this plugin does not make.
/// </param>
/// <param name="StartIndex">
/// How many titles to skip, or null for the beginning.
/// </param>
/// <param name="Limit">
/// How many titles to ask for at most, or null to let the source answer with
/// its own page.
/// </param>
public readonly record struct SourceQuery(
    string Name,
    DiscoverTitleKind Kind,
    int? StartIndex,
    int? Limit)
{
    /// <summary>
    /// Refuses a question no source could be asked.
    /// </summary>
    /// <returns>The same query, so a caller can validate and use it in one step.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the name is null, empty or whitespace. A source asked for
    /// nothing in particular either answers with whatever its default is or
    /// refuses, and both spend a request against a rate budget for an answer
    /// nobody asked for.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the kind is not one this plugin carries, when the start
    /// index is negative, or when the limit is zero or negative. A limit of
    /// zero is refused rather than treated as "no limit": it reads as a request
    /// for nothing, and a caller that meant no limit has null to say so.
    /// </exception>
    public SourceQuery Validated()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException(
                "A source query names what is being asked for. A blank name is a shelf that was built without one rather than a question about everything.",
                nameof(Name));
        }

        if (Kind is not (DiscoverTitleKind.Movie or DiscoverTitleKind.Series))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Kind),
                Kind,
                "A source query asks for one kind of title. None is what an unset field reads as, and an adapter handed it would have to choose one.");
        }

        if (StartIndex is { } start)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start, nameof(StartIndex));
        }

        if (Limit is { } limit)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit, nameof(Limit));
        }

        return this;
    }
}
