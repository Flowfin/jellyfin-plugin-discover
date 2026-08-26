using System;
using System.Linq;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Seam;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The message that crosses the seam, against the contract that fixes it.
/// </summary>
/// <remarks>
/// The field list is authoritative in
/// `docs/decisions/0004-what-crosses-the-seam-to-a-requests-plugin.md`. What is
/// asserted here is the half a reader of that page cannot check: that the type
/// carries those fields and no others, and that each field refuses the value an
/// unset one reads as.
/// </remarks>
public class WantTests
{
    private static readonly string[] _theFieldsThatCross =
    {
        "AskingUser", "ContractVersion", "Identity", "Kind", "Name", "ReleaseYear", "WantIdentifier"
    };

    private static DiscoverTitleIdentity AnIdentity() =>
        new DiscoverTitleIdentity(new[] { new ProviderIdentifier(MetadataSource.Tmdb, "329865") });

    private static Want AWant() => new Want
    {
        Identity = AnIdentity(),
        Kind = DiscoverTitleKind.Movie,
        Name = "Arrival",
        ReleaseYear = 2016,
        AskingUser = new Guid("2f1d0f9a-6f66-4a51-9a3f-9f5a6e2c1b74"),
        WantIdentifier = "tmdb:329865:2f1d0f9a"
    };

    /// <summary>
    /// The message carries the seven fields the contract names and nothing else.
    /// </summary>
    /// <remarks>
    /// Counted off the type rather than listed against a hand-written set, so a
    /// field added here fails until somebody has decided whether it crosses. The
    /// three that deliberately stay behind - the summary, the artwork location
    /// and the original-language name - are named in the assertion so that the
    /// failure says which side of the seam it is about.
    /// </remarks>
    [Fact]
    public void TheMessageCarriesTheSevenFieldsTheContractNames()
    {
        var carried = typeof(Want)
            .GetProperties()
            .Select(property => property.Name)
            .Where(name => !string.Equals(name, "EqualityContract", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(_theFieldsThatCross, carried);

        Assert.DoesNotContain("Summary", carried, StringComparer.Ordinal);
        Assert.DoesNotContain("ArtworkLocation", carried, StringComparer.Ordinal);
        Assert.DoesNotContain("OriginalName", carried, StringComparer.Ordinal);
        Assert.DoesNotContain("SchemaVersion", carried, StringComparer.Ordinal);
    }

    /// <summary>
    /// A want written by this build carries this build's contract version.
    /// </summary>
    [Fact]
    public void AWantCarriesTheContractVersionThisBuildWrites()
    {
        Assert.Equal(WantContract.CurrentVersion, AWant().ContractVersion);
        Assert.Equal(1, WantContract.CurrentVersion);
    }

    /// <summary>
    /// A missing release year is missing rather than zero.
    /// </summary>
    /// <remarks>
    /// Absence is absence, which is the rule the catalogue record holds and the
    /// contract repeats. A receiver meeting a zero cannot tell it from a year,
    /// and a receiver meeting an absence knows the source gave none.
    /// </remarks>
    [Fact]
    public void AMissingReleaseYearIsAbsentRatherThanZero()
    {
        var want = AWant() with { ReleaseYear = null };

        Assert.Null(want.ReleaseYear);
        Assert.Throws<ArgumentOutOfRangeException>(() => AWant() with { ReleaseYear = 0 });
    }

    /// <summary>
    /// Every field an unset one would read as is refused.
    /// </summary>
    /// <remarks>
    /// One test rather than five, because each of these is the same defect in a
    /// different field: a value the compiler is happy with that means "nobody
    /// filled this in" and that a receiver would act on. The kind is the one
    /// worth naming: a receiver acting on <see cref="DiscoverTitleKind.None"/>
    /// acts on whichever of the two its own default is, silently.
    /// </remarks>
    [Fact]
    public void TheValuesAnUnsetFieldReadsAsAreRefused()
    {
        Assert.Throws<ArgumentNullException>(() => AWant() with { Identity = null! });
        Assert.Throws<ArgumentException>(() => AWant() with { Kind = DiscoverTitleKind.None });
        Assert.Throws<ArgumentException>(() => AWant() with { Name = "   " });
        Assert.Throws<ArgumentException>(() => AWant() with { AskingUser = Guid.Empty });
        Assert.Throws<ArgumentException>(() => AWant() with { WantIdentifier = "  " });
        Assert.Throws<ArgumentOutOfRangeException>(() => AWant() with { ContractVersion = 0 });
    }

    /// <summary>
    /// Two wants with one identifier are one want.
    /// </summary>
    /// <remarks>
    /// The property a receiver keys on, asserted on this side so that the
    /// contract's strongest rule is not held only by prose. It is record
    /// equality doing the work rather than anything written here, which is the
    /// reason this is a record: a class would compare by reference and two
    /// handovers of one want would be two things to every collection that held
    /// them.
    /// </remarks>
    [Fact]
    public void TwoWantsAlikeInEveryFieldAreOneWant()
    {
        Assert.Equal(AWant(), AWant());
        Assert.NotEqual(AWant(), AWant() with { WantIdentifier = "something-else" });
    }
}
