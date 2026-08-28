using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Shelves;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Which document a shelf's titles are kept in, and what a name cannot be made
/// out of.
/// </summary>
/// <remarks>
/// #65's third condition. One document per shelf means the name has to tell one
/// shelf from another, and the failure this holds against is two shelves
/// resolving to one document, which is one shelf overwriting the other's titles
/// on every refresh and neither being wrong on its own.
///
/// The second failure is a name that cannot be written. A name derived from
/// anything an operator typed carries whatever they typed into a file name, and
/// the two server platforms disagree about which of those characters is a
/// separator, so a document written on one is refused on the other. That is why
/// both inputs here are closed sets and why the names are asserted against the
/// directory's own refusal rather than eyeballed.
/// </remarks>
public class CatalogueLayoutTests
{
    /// <summary>
    /// Gets every shelf a shipped set could be built from.
    /// </summary>
    /// <remarks>
    /// Derived from the two closed sets rather than listed, so a member added
    /// to either arrives here without anybody remembering to add a row.
    /// </remarks>
    public static TheoryData<ShelfQuestion, DiscoverTitleKind> EveryPair
    {
        get
        {
            var data = new TheoryData<ShelfQuestion, DiscoverTitleKind>();

            foreach (var pair in Pairs())
            {
                data.Add(pair.Question, pair.Kind);
            }

            return data;
        }
    }

    /// <summary>
    /// No two shelves share a document.
    /// </summary>
    /// <remarks>
    /// The near-miss this file is worth having. Two shelves resolving to one
    /// name is one shelf overwriting the other's titles on every refresh, with
    /// nothing about either shelf wrong, and it is exactly what a name that
    /// dropped the kind or the question would produce.
    ///
    /// Watched failing rather than assumed to bite: spelling the two kinds the
    /// same, or returning the question alone, leaves three of the six names
    /// colliding and reds this.
    /// </remarks>
    [Fact]
    public void TwoShelvesNeverShareADocument()
    {
        var names = Pairs()
            .Select(pair => CatalogueLayout.DocumentName(pair.Question, pair.Kind))
            .ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Every document name is one the catalogue directory will write.
    /// </summary>
    /// <param name="question">What the shelf asks for.</param>
    /// <param name="kind">Which sort of title it holds.</param>
    /// <remarks>
    /// Asserted against the directory's own refusal rather than against a list
    /// of characters, because the refusal is the thing that decides and it
    /// names both platforms' separators. A name this layout produced and that
    /// directory would not take is a shelf that cannot be stored at all, and it
    /// would be found on whichever server the plugin was installed on first.
    ///
    /// The path is only computed. Nothing is created and nothing is written, so
    /// the folder below never has to exist.
    /// </remarks>
    [Theory]
    [MemberData(nameof(EveryPair))]
    public void EveryDocumentNameResolvesInsideTheCatalogueDirectory(
        ShelfQuestion question,
        DiscoverTitleKind kind)
    {
        var folder = Path.Combine(Path.GetTempPath(), "a-folder-nothing-creates");
        var directory = new CatalogueDirectory(folder);
        var name = CatalogueLayout.DocumentName(question, kind);

        var path = directory.DocumentPath(name);

        Assert.Equal(Path.Combine(directory.FullPath, name), path, StringComparer.Ordinal);
        Assert.Equal(name, Path.GetFileName(path), StringComparer.Ordinal);
    }

    /// <summary>
    /// A document name is the same bytes on every server.
    /// </summary>
    /// <param name="question">What the shelf asks for.</param>
    /// <param name="kind">Which sort of title it holds.</param>
    /// <remarks>
    /// Lowercase ASCII and hyphens, so producing the name needs no culture's
    /// casing rules. A name folded by the running culture is the defect where a
    /// server under a Turkish locale writes one document and then reads for
    /// another, and it is invisible on the machine that wrote the code.
    /// </remarks>
    [Theory]
    [MemberData(nameof(EveryPair))]
    public void ADocumentNameIsLowercaseAsciiAndHyphens(ShelfQuestion question, DiscoverTitleKind kind)
    {
        var name = CatalogueLayout.DocumentName(question, kind);

        Assert.NotEmpty(name);
        Assert.All(name, character => Assert.True(
            (character >= 'a' && character <= 'z') || character == '-',
            $"The document name {name} carries a character outside lowercase ASCII and the hyphen."));
    }

    /// <summary>
    /// A shelf keeps its document when the things a name does not read change.
    /// </summary>
    /// <remarks>
    /// What a row is called and how many titles it holds are not in the name,
    /// so renaming a shelf or moving its cap does not orphan what was already
    /// fetched for it. The opposite assertion is beside it: the kind is in the
    /// name, so two shelves asking one question about the two kinds are two
    /// documents.
    /// </remarks>
    [Fact]
    public void ADocumentFollowsTheShelfRatherThanItsName()
    {
        var shelf = new Shelf
        {
            DisplayName = "Popular films",
            Question = ShelfQuestion.Popular,
            Kind = DiscoverTitleKind.Movie,
            Source = MetadataSource.Tmdb,
            Cap = 20
        };

        var renamed = shelf with { DisplayName = "Beliebte Filme", Cap = 40, Enabled = false };
        var series = shelf with { Kind = DiscoverTitleKind.Series };

        Assert.Equal(
            CatalogueLayout.DocumentName(shelf),
            CatalogueLayout.DocumentName(renamed),
            StringComparer.Ordinal);

        Assert.NotEqual(
            CatalogueLayout.DocumentName(shelf),
            CatalogueLayout.DocumentName(series),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// A shelf that could not be asked for anything claims no document.
    /// </summary>
    [Fact]
    public void AShelfThatCouldNotBeAskedForAnythingClaimsNoDocument()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueLayout.DocumentName(ShelfQuestion.None, DiscoverTitleKind.Movie));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueLayout.DocumentName(ShelfQuestion.Popular, DiscoverTitleKind.None));

        Assert.Throws<ArgumentNullException>(() => CatalogueLayout.DocumentName(null!));

        var unaskable = new Shelf
        {
            DisplayName = "A row built out of an unset field",
            Question = ShelfQuestion.None,
            Kind = DiscoverTitleKind.Movie,
            Source = MetadataSource.Tmdb,
            Cap = 20
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogueLayout.DocumentName(unaskable));
    }

    /// <summary>
    /// Every shelf the two closed sets admit.
    /// </summary>
    /// <returns>The pairs.</returns>
    private static IEnumerable<(ShelfQuestion Question, DiscoverTitleKind Kind)> Pairs()
    {
        foreach (var question in Enum.GetValues<ShelfQuestion>())
        {
            if (question == ShelfQuestion.None)
            {
                continue;
            }

            foreach (var kind in Enum.GetValues<DiscoverTitleKind>())
            {
                if (kind == DiscoverTitleKind.None)
                {
                    continue;
                }

                yield return (question, kind);
            }
        }
    }
}
