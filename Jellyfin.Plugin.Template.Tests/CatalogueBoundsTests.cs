using System;
using System.Globalization;
using Jellyfin.Plugin.Template.Configuration;
using Jellyfin.Plugin.Template.Shelves;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The bound on what this plugin writes into the operator's library database.
/// </summary>
/// <remarks>
/// #58 is what these are for. The defect they are written against is not a
/// wrong number, which somebody notices, but a pair of numbers that contradict
/// each other and are found at a refresh rather than at the save: a total below
/// the per-shelf bound, or a set of shelves that cannot fit inside the total.
/// Both of those read plausibly on a configuration page.
///
/// Nothing here reaches a source, a server or a database. What is judged is the
/// arithmetic and the refusals, and what a row actually costs on disk is #71 and
/// is unmeasured.
/// </remarks>
public static class CatalogueBoundsTests
{
    /// <summary>
    /// The two defaults are the ones the configuration ships with, so a fresh
    /// install is bounded rather than bounded once somebody visits the page.
    /// </summary>
    [Fact]
    public static void AFreshConfigurationCarriesTheDefaultBounds()
    {
        var fresh = new PluginConfiguration();

        Assert.Equal(CatalogueBounds.DefaultTitlesPerShelf, fresh.MaximumTitlesPerShelf);
        Assert.Equal(CatalogueBounds.DefaultTitlesAcrossAllShelves, fresh.MaximumTitlesAcrossAllShelves);

        var bounds = fresh.Bounds();

        Assert.Equal(20, bounds.TitlesPerShelf);
        Assert.Equal(120, bounds.TitlesAcrossAllShelves);
    }

    /// <summary>
    /// The total default is the shipped set at the per-shelf default rather than
    /// a second number somebody chose, and this is what holds it that way.
    /// </summary>
    /// <remarks>
    /// The one guard here that bites on a change nobody would connect to this
    /// file. A seventh shelf added to the shipped set makes the default
    /// configuration one <see cref="CatalogueBounds.ThrowIfShelvesDoNotFit"/>
    /// refuses, so every fresh install would fail to save its own defaults. That
    /// arrives as a red test here rather than as a plugin nobody can configure.
    /// </remarks>
    [Fact]
    public static void TheTotalDefaultIsTheShippedSetAtThePerShelfDefault()
    {
        var shipped = ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf);

        Assert.Equal(
            shipped.Count * CatalogueBounds.DefaultTitlesPerShelf,
            CatalogueBounds.DefaultTitlesAcrossAllShelves);

        new PluginConfiguration().Bounds().ThrowIfShelvesDoNotFit(shipped.Count);
    }

    /// <summary>
    /// Neither number may be zero or negative, and the refusal says which one it
    /// is about.
    /// </summary>
    /// <remarks>
    /// Zero is the interesting half. It reads as "hold nothing", and a plugin
    /// that holds nothing is one that is turned off, which is #109 rather than a
    /// bound. Accepting it would give an operator a surface of shelves that are
    /// all empty with nothing anywhere saying why.
    /// </remarks>
    [Fact]
    public static void ABoundOfZeroOrLessIsRefusedOnEitherNumber()
    {
        var perShelf = Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueBounds.Of(maximumTitlesPerShelf: 0, maximumTitlesAcrossAllShelves: 120));
        Assert.Equal("maximumTitlesPerShelf", perShelf.ParamName);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueBounds.Of(maximumTitlesPerShelf: -1, maximumTitlesAcrossAllShelves: 120));

        var total = Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueBounds.Of(maximumTitlesPerShelf: 20, maximumTitlesAcrossAllShelves: 0));
        Assert.Equal("maximumTitlesAcrossAllShelves", total.ParamName);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueBounds.Of(maximumTitlesPerShelf: 20, maximumTitlesAcrossAllShelves: -120));
    }

    /// <summary>
    /// A total below one shelf's own bound is the pair no set satisfies, and it
    /// is refused when the pair is read rather than when a shelf is filled.
    /// </summary>
    /// <remarks>
    /// The ordinary way to arrive here is lowering the total and forgetting the
    /// per-shelf number beside it. The message carries both, because an operator
    /// who sees only "out of range" has to guess which of the two they typed is
    /// the one being objected to.
    /// </remarks>
    [Fact]
    public static void ATotalBelowOneShelfsBoundIsRefusedWithBothNumbers()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueBounds.Of(maximumTitlesPerShelf: 40, maximumTitlesAcrossAllShelves: 30));

        Assert.Equal("maximumTitlesAcrossAllShelves", refusal.ParamName);
        Assert.Contains("40", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("30", refusal.Message, StringComparison.Ordinal);

        // Equal is not below, so a single shelf holding the whole allowance is a
        // configuration rather than a contradiction.
        var exact = CatalogueBounds.Of(maximumTitlesPerShelf: 30, maximumTitlesAcrossAllShelves: 30);
        Assert.Equal(30, exact.TitlesPerShelf);
    }

    /// <summary>
    /// More shelves than the total covers is refused, with the arithmetic in the
    /// message, and one shelf fewer is not.
    /// </summary>
    /// <remarks>
    /// The boundary is asserted on both sides rather than on the failing one
    /// alone. A comparison written the other way round would refuse the set that
    /// exactly fills the allowance, which is the configuration the defaults
    /// themselves are.
    /// </remarks>
    [Fact]
    public static void ShelvesThatDoNotFitAreRefusedAndTheOneThatFitsIsNot()
    {
        var bounds = CatalogueBounds.Of(maximumTitlesPerShelf: 20, maximumTitlesAcrossAllShelves: 120);

        bounds.ThrowIfShelvesDoNotFit(6);

        var refusal = Assert.Throws<ArgumentException>(
            () => bounds.ThrowIfShelvesDoNotFit(7));

        // No argument of the call is wrong, so the refusal carries no parameter
        // name: the count is the shipped set's own and what does not fit is the
        // saved pair. A parameter name here would be "shelfCount", which is not
        // a setting and not a word in the document.
        Assert.Null(refusal.ParamName);
        Assert.Contains("140", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("120", refusal.Message, StringComparison.Ordinal);

        Assert.Throws<ArgumentOutOfRangeException>(() => bounds.ThrowIfShelvesDoNotFit(-1));

        // No shelves is not a contradiction. It is a surface with nothing on it,
        // which is #63 rather than a bound being breached.
        bounds.ThrowIfShelvesDoNotFit(0);
    }

    /// <summary>
    /// A per-shelf bound and a shelf count whose product does not fit an int is
    /// refused rather than wrapping into a small number that passes.
    /// </summary>
    /// <remarks>
    /// The one-character mistake this is for is multiplying in
    /// <see cref="int"/>. Two hundred million titles a shelf across twenty
    /// shelves overflows to a negative number, and a comparison against the
    /// total then passes: the bound this plugin exists to enforce would be
    /// silently absent at exactly the sizes it matters at.
    /// </remarks>
    [Fact]
    public static void AProductTooLargeForAnIntIsRefusedRatherThanWrapped()
    {
        var bounds = CatalogueBounds.Of(
            maximumTitlesPerShelf: 200_000_000,
            maximumTitlesAcrossAllShelves: int.MaxValue);

        var refusal = Assert.Throws<ArgumentException>(
            () => bounds.ThrowIfShelvesDoNotFit(20));

        Assert.Contains(
            4_000_000_000L.ToString(CultureInfo.InvariantCulture),
            refusal.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The refusal happens at the save rather than at a refresh, which is #58's
    /// third condition and the reason the check is on the configuration path.
    /// </summary>
    /// <remarks>
    /// Driven through <see cref="PluginConfiguration.Bounds"/> rather than
    /// through the plugin class, because constructing the plugin needs the
    /// server's application paths and its serialiser, and what is being asserted
    /// is which numbers are refused rather than how the server calls in.
    /// <c>Plugin.UpdateConfiguration</c> is where this is called from, and that
    /// route has not been run against a server here.
    /// </remarks>
    [Fact]
    public static void AConfigurationWhoseNumbersContradictEachOtherIsRefused()
    {
        var configuration = new PluginConfiguration
        {
            MaximumTitlesPerShelf = 50,
            MaximumTitlesAcrossAllShelves = 120
        };

        // The pair itself is legal: fifty is below a hundred and twenty.
        var bounds = configuration.Bounds();

        // The shipped set at fifty a shelf is three hundred, which it is not.
        var refusal = Assert.Throws<ArgumentException>(
            () => bounds.ThrowIfShelvesDoNotFit(
                ShippedShelves.Bounded(configuration.MaximumTitlesPerShelf).Count));

        Assert.Contains("300", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every refusal names the setting as the configuration document spells it,
    /// which is #105's first condition and the rule <see cref="PluginConfiguration"/>
    /// states.
    /// </summary>
    /// <remarks>
    /// The one-word mistake this is for is a refusal naming the method's own
    /// parameter. The runtime appends that name to the message of an
    /// <see cref="ArgumentOutOfRangeException"/> whatever the text says, so a
    /// message that dropped the setting would still carry a word that looks
    /// like it. The comparison is ordinal and the setting is spelled with its
    /// capital, which the parameter is not, so that appended word does not
    /// satisfy this and the sentence has to.
    ///
    /// The spelling is taken from the property by <c>nameof</c> rather than
    /// typed, so a renamed setting reddens the refusal that still says the old
    /// name instead of the test that still expects it.
    /// </remarks>
    /// <param name="perShelf">The per-shelf bound offered.</param>
    /// <param name="total">The bound across all shelves offered.</param>
    /// <param name="setting">The setting the refusal has to name, as the document spells it.</param>
    [Theory]
    [InlineData(0, 120, nameof(PluginConfiguration.MaximumTitlesPerShelf))]
    [InlineData(20, 0, nameof(PluginConfiguration.MaximumTitlesAcrossAllShelves))]
    [InlineData(40, 30, nameof(PluginConfiguration.MaximumTitlesAcrossAllShelves))]
    public static void ARefusedBoundIsNamedAsTheDocumentSpellsIt(int perShelf, int total, string setting)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueBounds.Of(perShelf, total));

        Assert.Contains(setting, refusal.Message, StringComparison.Ordinal);
        Assert.StartsWith(setting, refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A set of shelves that does not fit is refused naming both settings, so
    /// the operator can choose which of the two to change.
    /// </summary>
    /// <remarks>
    /// Both, because the arithmetic in the message has two settings in it and
    /// an operator who is told only one of them has been told which number to
    /// look at but not what it is compared against. The old refusal named a
    /// count that is not a setting at all, and this is what stops that coming
    /// back.
    /// </remarks>
    [Fact]
    public static void ShelvesThatDoNotFitAreRefusedNamingBothSettings()
    {
        var bounds = CatalogueBounds.Of(maximumTitlesPerShelf: 20, maximumTitlesAcrossAllShelves: 120);

        var refusal = Assert.Throws<ArgumentException>(
            () => bounds.ThrowIfShelvesDoNotFit(7));

        Assert.Contains(nameof(PluginConfiguration.MaximumTitlesPerShelf), refusal.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(PluginConfiguration.MaximumTitlesAcrossAllShelves), refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("shelfCount", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// #105's fourth condition at the lower edge of both numbers. The refusals
    /// above are proven at zero and below; nothing above proved that one, the
    /// smallest count either bound accepts, is inside. A refusal written as
    /// "less than two" would have passed every test in this file.
    /// </summary>
    [Fact]
    public static void ABoundOfOneIsTheSmallestAcceptedOnEitherNumber()
    {
        var bounds = CatalogueBounds.Of(maximumTitlesPerShelf: 1, maximumTitlesAcrossAllShelves: 1);

        Assert.Equal(1, bounds.TitlesPerShelf);
        Assert.Equal(1, bounds.TitlesAcrossAllShelves);

        // One shelf of one title fills that allowance exactly, and a second
        // shelf is one step beyond it.
        bounds.ThrowIfShelvesDoNotFit(1);
        Assert.Throws<ArgumentException>(() => bounds.ThrowIfShelvesDoNotFit(2));
    }

    /// <summary>
    /// The edge between the two numbers, one step apart rather than ten. The
    /// test above that refuses forty against thirty would pass a rule that
    /// allowed a total one short of the per-shelf bound.
    /// </summary>
    [Fact]
    public static void ATotalOneBelowThePerShelfBoundIsRefusedAndEqualIsNot()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueBounds.Of(maximumTitlesPerShelf: 30, maximumTitlesAcrossAllShelves: 29));

        Assert.Equal("maximumTitlesAcrossAllShelves", refusal.ParamName);
        Assert.Contains("29", refusal.Message, StringComparison.Ordinal);

        var equal = CatalogueBounds.Of(maximumTitlesPerShelf: 30, maximumTitlesAcrossAllShelves: 30);
        Assert.Equal(30, equal.TitlesAcrossAllShelves);
    }

    /// <summary>
    /// The edge of the total against the shipped set, derived from that set
    /// rather than typed, so a seventh shelf moves the edge and not the test.
    /// The total the set fills exactly is inside; one title fewer is refused,
    /// and the refusal names both numbers so an operator can see which to move.
    /// </summary>
    [Fact]
    public static void ATotalTheShippedSetFillsExactlyIsAcceptedAndOneBelowIsRefused()
    {
        var shipped = ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf).Count;
        var exactly = shipped * CatalogueBounds.DefaultTitlesPerShelf;

        CatalogueBounds.Of(CatalogueBounds.DefaultTitlesPerShelf, exactly).ThrowIfShelvesDoNotFit(shipped);

        var refusal = Assert.Throws<ArgumentException>(
            () => CatalogueBounds.Of(CatalogueBounds.DefaultTitlesPerShelf, exactly - 1).ThrowIfShelvesDoNotFit(shipped));

        Assert.Contains((exactly - 1).ToString(CultureInfo.InvariantCulture), refusal.Message, StringComparison.Ordinal);
        Assert.Contains(exactly.ToString(CultureInfo.InvariantCulture), refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same two edges through the save itself rather than through the
    /// type, which is where #105 says a bad setting is refused. A value at the
    /// edge reaches the serialiser and a value one step beyond does not, and
    /// what was written last is still the last accepted document.
    /// </summary>
    [Fact]
    public static void ASaveAtTheEdgeIsWrittenAndOneStepBeyondIsNot()
    {
        var shipped = ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf).Count;
        var exactly = shipped * CatalogueBounds.DefaultTitlesPerShelf;

        var log = new CallLog();
        var serialiser = new XmlSerializerThatRecordsWhatIsWritten(log);
        var plugin = new Plugin(new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(log), serialiser);

        var atTheEdge = new PluginConfiguration
        {
            MaximumTitlesPerShelf = CatalogueBounds.DefaultTitlesPerShelf,
            MaximumTitlesAcrossAllShelves = exactly
        };
        plugin.UpdateConfiguration(atTheEdge);
        Assert.Same(atTheEdge, serialiser.LastWritten);

        var oneBeyond = new PluginConfiguration
        {
            MaximumTitlesPerShelf = CatalogueBounds.DefaultTitlesPerShelf,
            MaximumTitlesAcrossAllShelves = exactly - 1
        };
        Assert.Throws<ArgumentException>(() => plugin.UpdateConfiguration(oneBeyond));
        Assert.Same(atTheEdge, serialiser.LastWritten);

        var smallest = new PluginConfiguration
        {
            MaximumTitlesPerShelf = 1,
            MaximumTitlesAcrossAllShelves = shipped
        };
        plugin.UpdateConfiguration(smallest);
        Assert.Same(smallest, serialiser.LastWritten);

        var belowTheSmallest = new PluginConfiguration
        {
            MaximumTitlesPerShelf = 0,
            MaximumTitlesAcrossAllShelves = shipped
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => plugin.UpdateConfiguration(belowTheSmallest));
        Assert.Same(smallest, serialiser.LastWritten);
    }
}
