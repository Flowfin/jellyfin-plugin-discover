using System;
using Jellyfin.Plugin.Template.Shelves;

namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// Which document in the catalogue directory a shelf's titles are kept in.
/// </summary>
/// <remarks>
/// #65's third condition, and the answer is one document per shelf. The note is
/// `docs/decisions/0005-the-catalogue-is-one-document-per-shelf.md` and it
/// carries the argument; what is here is the derivation and the reasons that
/// belong beside the code.
///
/// The name is built from the question and the kind and from nothing else,
/// because those two are what tells one shelf from another and neither of them
/// is text anybody typed. A name derived from a display name would put an
/// operator's characters into a file name, which is the case
/// <see cref="CatalogueDirectory.DocumentPath"/> exists to refuse and which
/// arrives differently on each of the two server platforms; the refusal is a
/// second guard rather than the first one.
///
/// So a document name cannot be made unwritable by anything a configuration
/// carries. Both inputs are closed sets, every member of both maps here, and
/// the unset member of each is refused rather than spelled.
/// </remarks>
public static class CatalogueLayout
{
    /// <summary>
    /// The document one shelf's titles are kept in.
    /// </summary>
    /// <param name="question">What the shelf asks for.</param>
    /// <param name="kind">Which sort of title it holds.</param>
    /// <returns>The document's name, for <see cref="CatalogueDirectory.DocumentPath"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either argument is the unset member of its set. A document
    /// named for a shelf that could not be asked for anything is a file nothing
    /// would ever fill.
    /// </exception>
    /// <remarks>
    /// The two parts are joined by a hyphen and both are lowercase ASCII, so
    /// the name is the same byte sequence on every server and needs no
    /// culture's casing rules to produce. A name folded by the running
    /// culture is the defect where a Turkish server writes one document and
    /// reads for another.
    ///
    /// No extension. The store appends its own suffix to the name while a write
    /// is in flight, and a second extension would only be a convention nothing
    /// reads.
    /// </remarks>
    public static string DocumentName(ShelfQuestion question, DiscoverTitleKind kind) =>
        Question(question) + "-" + Kind(kind);

    /// <summary>
    /// The document one shelf's titles are kept in.
    /// </summary>
    /// <param name="shelf">The shelf.</param>
    /// <returns>The document's name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shelf"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the shelf could not be asked for anything, by
    /// <see cref="Shelf.Validated"/>.
    /// </exception>
    /// <remarks>
    /// The overload callers use. It validates first, so a shelf that cannot be
    /// asked for anything also cannot claim a document, and it reads only the
    /// two fields the name is derived from: what a shelf is called and how many
    /// titles it holds do not move its document, so renaming a row or changing
    /// its cap does not orphan what was already fetched for it.
    /// </remarks>
    public static string DocumentName(Shelf shelf)
    {
        ArgumentNullException.ThrowIfNull(shelf);

        shelf.Validated();

        return DocumentName(shelf.Question, shelf.Kind);
    }

    /// <summary>
    /// How a question is spelled in a document name.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The spelling.</returns>
    /// <remarks>
    /// Its own spelling rather than the one a source is asked with. The two are
    /// the same string today for two of the three, and tying a file name on an
    /// operator's disk to the word an adapter happens to send would make a
    /// change to that word a rename of every document under it.
    /// </remarks>
    private static string Question(ShelfQuestion question) => question switch
    {
        ShelfQuestion.Trending => "trending",
        ShelfQuestion.Popular => "popular",
        ShelfQuestion.TopRated => "top-rated",
        _ => throw new ArgumentOutOfRangeException(
            nameof(question),
            question,
            "A catalogue document is named after the question its shelf asks. None is what an unset field reads as, and no shelf asking it could be filled.")
    };

    /// <summary>
    /// How a kind is spelled in a document name.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The spelling.</returns>
    private static string Kind(DiscoverTitleKind kind) => kind switch
    {
        DiscoverTitleKind.Movie => "movie",
        DiscoverTitleKind.Series => "series",
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "A catalogue document is named after the kind of title its shelf holds. None is what an unset field reads as.")
    };
}
