using System;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Shelves;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The six shelves a first install browses, and the page that argues for them.
/// </summary>
/// <remarks>
/// #86's fourth condition is what these are for. The defect they are written
/// against is the one <c>docs/shelves.md</c> declares about itself: the set was
/// prose beside code, so a question added to <see cref="ShelfQuestion"/> and not
/// to the page, or a row on the page naming something no build ships, was caught
/// by a reader or not at all. Both directions are refused below.
///
/// Two of #86's conditions have no subject here and are not pretended at. There
/// is no setting that turns a shelf off, so <see cref="Shelf.Enabled"/> is
/// asserted as the state a fresh set arrives in rather than as something an
/// operator has moved. And the total item count this set implies is undefended,
/// because the measurement it would be weighed against is #71 and does not
/// exist. Both are recorded on the issue.
/// </remarks>
public static class ShippedShelvesTests
{
    /// <summary>
    /// A cap standing in for #58's number, which is not decided anywhere yet.
    /// Any positive value does; nothing below asserts this one is right.
    /// </summary>
    private const int SomeCap = 20;

    /// <summary>
    /// The set covers every question this plugin can ask against both kinds of
    /// title, once each. Derived from the enum rather than counted, so a fourth
    /// question added to the vocabulary and not shipped reddens here instead of
    /// becoming a name no shelf ever asks.
    /// </summary>
    [Fact]
    public static void EveryQuestionShipsAgainstBothKinds()
    {
        var shipped = ShippedShelves.Bounded(SomeCap)
            .Select(shelf => (shelf.Question, shelf.Kind))
            .OrderBy(pair => pair.Question)
            .ThenBy(pair => pair.Kind)
            .ToArray();

        var wanted = (
            from question in Enum.GetValues<ShelfQuestion>().Where(q => q != ShelfQuestion.None)
            from kind in new[] { DiscoverTitleKind.Movie, DiscoverTitleKind.Series }
            select (question, kind)).ToArray();

        Assert.Equal(wanted, shipped);
    }

    /// <summary>
    /// The other direction of the same property. A pair shipped twice is two
    /// rows asking one question, which reaches an operator as a duplicated
    /// shelf rather than as an error.
    /// </summary>
    [Fact]
    public static void NoQuestionShipsTwiceForOneKind()
    {
        var shipped = ShippedShelves.Bounded(SomeCap);

        Assert.Equal(
            shipped.Count,
            shipped.Select(shelf => (shelf.Question, shelf.Kind)).Distinct().Count());
    }

    /// <summary>
    /// Names are what an operator picks a row out by, so two rows carrying one
    /// name is a configuration page nobody can act on.
    /// </summary>
    [Fact]
    public static void EveryShelfIsCalledSomethingOfItsOwn()
    {
        var names = ShippedShelves.Bounded(SomeCap).Select(shelf => shelf.DisplayName).ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The bound reaches every shelf rather than some of them, and it is the
    /// caller's number rather than one this class holds. #58 owns the value;
    /// this owns that nothing here quietly answers it.
    /// </summary>
    /// <param name="cap">A bound to build the set against.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(500)]
    public static void EveryShelfHoldsTheBoundItWasBuiltWith(int cap)
    {
        Assert.All(ShippedShelves.Bounded(cap), shelf => Assert.Equal(cap, shelf.Cap));
    }

    /// <summary>
    /// A set built for a bound that admits nothing is refused rather than
    /// shipped. The near miss is the one-character mistake of handing over a
    /// count that has already been decremented to zero, which would otherwise
    /// produce six rows that are empty on every server.
    /// </summary>
    /// <param name="cap">A bound no shelf can be built against.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public static void ASetThatCouldHoldNothingIsRefused(int cap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShippedShelves.Bounded(cap));
    }

    /// <summary>
    /// Every shipped shelf is one a server set up with the one implemented
    /// source can actually ask. A shelf naming a source nobody asks is a row
    /// that is empty for a reason no operator can see, which is the refusal
    /// <see cref="Shelf.ValidatedAgainst"/> exists for.
    /// </summary>
    [Fact]
    public static void EveryShelfNamesASourceAServerCanAsk()
    {
        var configured = new[] { new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb) };

        Assert.All(ShippedShelves.Bounded(SomeCap), shelf => shelf.ValidatedAgainst(configured));
    }

    /// <summary>
    /// A fresh set is on. An operator turning a row off is the setting #86's
    /// fourth condition asks for and there is none, so what is asserted is the
    /// state a first install arrives in rather than anything reading the flag.
    /// </summary>
    [Fact]
    public static void EveryShelfIsOnUntilSomethingTurnsItOff()
    {
        Assert.All(ShippedShelves.Bounded(SomeCap), shelf => Assert.True(shelf.Enabled));
    }

    /// <summary>
    /// The set is rebuilt per call rather than held, so a bound that changes
    /// after somebody has already asked once is honoured. A cached set is the
    /// shape a stale bound takes, and it reads identically to a fresh one.
    /// </summary>
    [Fact]
    public static void ASecondBoundIsNotTheFirstOne()
    {
        Assert.All(ShippedShelves.Bounded(40), shelf => Assert.Equal(40, shelf.Cap));
        Assert.All(ShippedShelves.Bounded(20), shelf => Assert.Equal(20, shelf.Cap));
    }

    /// <summary>
    /// The page's table is this set, row for row and in the same order. This is
    /// the direction that catches a shelf added to the code and argued for
    /// nowhere, which is a row an operator meets with no reason beside it.
    /// </summary>
    [Fact]
    public static void EveryShelfThisBuildShipsHasARowOnThePage()
    {
        Assert.Equal(
            ShippedShelves.Bounded(SomeCap).Select(Described).ToArray(),
            Rows());
    }

    /// <summary>
    /// The other direction. A row for a shelf that has been renamed or dropped
    /// is a page describing a build nobody is running, and it reads exactly like
    /// a current one.
    /// </summary>
    [Fact]
    public static void EveryRowOnThePageIsAShelfThisBuildShips()
    {
        Assert.Equal(
            Rows(),
            ShippedShelves.Bounded(SomeCap).Select(Described).ToArray());
    }

    /// <summary>
    /// A shelf as the page states it. The question is taken from the query the
    /// shelf composes rather than from its enum member, so the column is held
    /// against the spelling an adapter actually receives.
    /// </summary>
    /// <param name="shelf">The shelf.</param>
    /// <returns>The row the page is expected to carry.</returns>
    private static Row Described(Shelf shelf) =>
        new Row(shelf.DisplayName, shelf.Ask().Name, shelf.Kind.ToString());

    /// <summary>
    /// Reads the set out of the page. The rows are the lines between the header
    /// and the first line that is not a row, so a second table elsewhere on the
    /// page is not read as shelves.
    /// </summary>
    /// <returns>One row per shelf the page states.</returns>
    private static Row[] Rows()
    {
        var lines = File.ReadAllLines(RepositoryFile(Path.Combine("docs", "shelves.md")));

        var header = Array.FindIndex(
            lines,
            line => Cells(line) is { Length: 4 } cells
                && string.Equals(cells[0], "Shelf", StringComparison.Ordinal));

        Assert.True(
            header >= 0,
            "docs/shelves.md carries no table whose first column is Shelf, so there is nothing to check the shipped set against.");

        return lines
            .Skip(header + 2)
            .TakeWhile(line => line.TrimStart().StartsWith('|'))
            .Select(Cells)
            .Select(cells => new Row(cells[0], cells[1], cells[2]))
            .ToArray();
    }

    /// <summary>
    /// Splits one table line into its cells, dropping the empty pieces the
    /// leading and trailing pipes produce, and the backticks the page sets its
    /// question names in.
    /// </summary>
    /// <param name="line">A line of the page.</param>
    /// <returns>The cells, or an empty array for a line that is not a row.</returns>
    private static string[] Cells(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('|'))
        {
            return [];
        }

        return trimmed
            .Trim('|')
            .Split('|')
            .Select(cell => cell.Trim().Trim('`').Trim())
            .ToArray();
    }

    /// <summary>
    /// Walks up from the test assembly to the directory holding the named
    /// repository file.
    /// </summary>
    /// <param name="name">Path of the file, relative to the repository root.</param>
    /// <returns>The full path to that file.</returns>
    private static string RepositoryFile(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Walked up from {AppContext.BaseDirectory} without finding {name}. The test reads it out of the repository root.",
            name);
    }

    /// <summary>
    /// One row of the page's table, and the three columns of it a build can
    /// answer for. What a shelf is for is the fourth column and is prose.
    /// </summary>
    /// <param name="Shelf">What the row is called.</param>
    /// <param name="Question">The question, in the spelling an adapter reads.</param>
    /// <param name="Kind">Which sort of title the row holds.</param>
    private sealed record Row(string Shelf, string Question, string Kind);
}
