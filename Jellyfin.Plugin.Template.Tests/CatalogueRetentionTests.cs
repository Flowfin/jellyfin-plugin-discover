using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The number this plugin keeps a fetched record for, and the ceiling it is checked against.
/// </summary>
/// <remarks>
/// #68's third condition is that a configured retention cannot be set above the
/// ceiling any active source imposes, with the refusal naming the source and the
/// ceiling, and its fourth is that the boundary is proved one tick either side.
/// Both are here.
///
/// The retention itself is a decision rather than a derivation: ninety days,
/// answered on #2 on 2026-08-24. It is asserted as the shipped default and as
/// something under a real adapter's ceiling, so a later edit upward has to meet
/// both rather than only compile.
/// </remarks>
public class CatalogueRetentionTests
{
    /// <summary>
    /// The instant these records were fetched at.
    /// </summary>
    /// <remarks>
    /// A fixed value rather than a read of any clock, so the ages computed below
    /// are the same on every run.
    /// </remarks>
    private static readonly DateTimeOffset _fetched = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The shipped retention is ninety days, and it is inside what the first real source allows.
    /// </summary>
    /// <remarks>
    /// Two assertions rather than one. The literal is the decision and has to be
    /// readable as one. Being under the adapter's own ceiling is the property
    /// that would break if either number moved, and it is asserted against the
    /// adapter rather than against a copy of its value.
    /// </remarks>
    [Fact]
    public void TheShippedRetentionIsNinetyDaysAndIsUnderTheFirstSourcesCeiling()
    {
        Assert.Equal(TimeSpan.FromDays(90), CatalogueRetention.Default);

        var tmdb = new TmdbSourceAdapter(
            (address, cancellationToken) => throw new InvalidOperationException("Nothing here asks this source anything."),
            configured: true,
            new ClockATestAdvances(_fetched),
            SourceLocale.Unstated);

        Assert.True(CatalogueRetention.Default < tmdb.RetentionCeiling);
        Assert.Equal(
            CatalogueRetention.Default,
            CatalogueRetention.Of(CatalogueRetention.Default, new IMetadataSource[] { tmdb }).Duration);
    }

    /// <summary>
    /// A retention longer than a source allows is refused, and the refusal names that source and its ceiling.
    /// </summary>
    /// <remarks>
    /// The near-miss is which source objects. The refusing one is second in the
    /// list and the permissive one is first, so a check that reads only the
    /// first source, or that stops at the first source it is happy with, lets
    /// the breach through.
    /// </remarks>
    [Fact]
    public void ARetentionLongerThanAnActiveSourceAllowsIsRefusedNamingItAndItsCeiling()
    {
        var permissive = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb, TimeSpan.FromDays(180));
        var strict = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tvdb, TimeSpan.FromDays(30));

        var refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueRetention.Of(TimeSpan.FromDays(90), new IMetadataSource[] { permissive, strict }));

        Assert.Contains("Tvdb", refused.Message, StringComparison.Ordinal);
        Assert.Contains("30", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A retention exactly at a source's ceiling is allowed, because the ceiling is what that source allows.
    /// </summary>
    [Fact]
    public void ARetentionExactlyAtTheCeilingIsAllowed()
    {
        var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb, TimeSpan.FromDays(30));

        Assert.Equal(
            TimeSpan.FromDays(30),
            CatalogueRetention.Of(TimeSpan.FromDays(30), new IMetadataSource[] { source }).Duration);
    }

    /// <summary>
    /// A retention of zero or less is refused rather than read as keeping nothing.
    /// </summary>
    /// <param name="days">The duration offered, in days.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ARetentionThatIsNotAPositiveDurationIsRefused(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueRetention.Of(TimeSpan.FromDays(days), Array.Empty<IMetadataSource>()));
    }

    /// <summary>
    /// A record exactly as old as the retention is still held, and one tick older is not.
    /// </summary>
    /// <remarks>
    /// #68's fourth condition, and the whole of it: the condition it holds is
    /// that a record OLDER than the retention is not served, so the boundary
    /// itself is inside. One tick is the smallest step this type can be asked
    /// about, so a comparison typed as the wrong one of four fails here rather
    /// than after a server has served something it should not have.
    /// </remarks>
    [Fact]
    public void TheBoundaryIsInclusiveAndOneTickPastItIsNot()
    {
        var retention = CatalogueRetention.Of(
            TimeSpan.FromDays(90),
            new IMetadataSource[] { new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb, TimeSpan.FromDays(180)) });

        Assert.True(retention.Holds(_fetched, _fetched + TimeSpan.FromDays(90)));
        Assert.False(retention.Holds(_fetched, _fetched + TimeSpan.FromDays(90) + TimeSpan.FromTicks(1)));
    }

    /// <summary>
    /// A record fetched a moment ago is held, which is the case the boundary test cannot cover on its own.
    /// </summary>
    [Fact]
    public void ARecordJustFetchedIsHeld()
    {
        var retention = CatalogueRetention.Of(
            TimeSpan.FromDays(90),
            Array.Empty<IMetadataSource>());

        Assert.True(retention.Holds(_fetched, _fetched));
    }

    /// <summary>
    /// No list of sources at all is refused rather than read as no source objecting.
    /// </summary>
    [Fact]
    public void NoListOfSourcesAtAllIsRefusedRatherThanReadAsNoneObjecting()
    {
        Assert.Throws<ArgumentNullException>(
            () => CatalogueRetention.Of(TimeSpan.FromDays(1), null!));
    }

    /// <summary>
    /// A null inside the list of sources is refused rather than skipped.
    /// </summary>
    [Fact]
    public void ANullSourceInTheListIsRefusedRatherThanSkipped()
    {
        var sources = new List<IMetadataSource>
        {
            new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb, TimeSpan.FromDays(180)),
            null!
        };

        Assert.Throws<ArgumentNullException>(() => CatalogueRetention.Of(TimeSpan.FromDays(1), sources));
    }
}
