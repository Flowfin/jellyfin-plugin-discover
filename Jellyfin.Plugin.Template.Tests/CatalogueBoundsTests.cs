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
            () => CatalogueBounds.Of(titlesPerShelf: 0, titlesAcrossAllShelves: 120));
        Assert.Equal("titlesPerShelf", perShelf.ParamName);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueBounds.Of(titlesPerShelf: -1, titlesAcrossAllShelves: 120));

        var total = Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueBounds.Of(titlesPerShelf: 20, titlesAcrossAllShelves: 0));
        Assert.Equal("titlesAcrossAllShelves", total.ParamName);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueBounds.Of(titlesPerShelf: 20, titlesAcrossAllShelves: -120));
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
            () => CatalogueBounds.Of(titlesPerShelf: 40, titlesAcrossAllShelves: 30));

        Assert.Equal("titlesAcrossAllShelves", refusal.ParamName);
        Assert.Contains("40", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("30", refusal.Message, StringComparison.Ordinal);

        // Equal is not below, so a single shelf holding the whole allowance is a
        // configuration rather than a contradiction.
        var exact = CatalogueBounds.Of(titlesPerShelf: 30, titlesAcrossAllShelves: 30);
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
        var bounds = CatalogueBounds.Of(titlesPerShelf: 20, titlesAcrossAllShelves: 120);

        bounds.ThrowIfShelvesDoNotFit(6);

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => bounds.ThrowIfShelvesDoNotFit(7));

        Assert.Equal("shelfCount", refusal.ParamName);
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
            titlesPerShelf: 200_000_000,
            titlesAcrossAllShelves: int.MaxValue);

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
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
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => bounds.ThrowIfShelvesDoNotFit(
                ShippedShelves.Bounded(configuration.MaximumTitlesPerShelf).Count));

        Assert.Contains("300", refusal.Message, StringComparison.Ordinal);
    }
}
