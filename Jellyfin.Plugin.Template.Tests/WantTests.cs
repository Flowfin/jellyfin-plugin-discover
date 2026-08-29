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
        "AskingUser", "ContractVersion", "Identity", "Kind", "Name", "ReleaseYear", "Replay", "WantIdentifier"
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
    /// The message carries the eight fields the contract names and nothing else.
    /// </summary>
    /// <remarks>
    /// Counted off the type rather than listed against a hand-written set, so a
    /// field added here fails until somebody has decided whether it crosses. The
    /// three that deliberately stay behind - the summary, the artwork location
    /// and the original-language name - are named in the assertion so that the
    /// failure says which side of the seam it is about.
    /// </remarks>
    [Fact]
    public void TheMessageCarriesTheEightFieldsTheContractNames()
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

    /// <summary>
    /// A live want does not carry the replay marker, and a replayed one carries it.
    /// </summary>
    /// <remarks>
    /// The two shapes the contract names, in one test because they are one rule
    /// read from either end: absence is live and presence is a replay. Asserted
    /// on the want this suite builds the ordinary way, so the live shape is the
    /// one a caller gets without knowing the field exists, which is what makes
    /// absence the safe default rather than a convention somebody has to keep.
    /// </remarks>
    [Fact]
    public void ALiveWantCarriesNoReplayMarkerAndAReplayedOneDoes()
    {
        Assert.Null(AWant().Replay);

        var replayed = AWant() with { Replay = true };

        Assert.True(replayed.Replay);
        Assert.Equal(WantContract.CurrentVersion, replayed.ContractVersion);
    }

    /// <summary>
    /// A false replay marker is refused rather than sent as a second spelling of live.
    /// </summary>
    /// <remarks>
    /// The same defect as a release year of zero and an empty asking user: a
    /// value the compiler is happy with that means nobody filled the field in. A
    /// receiver meeting a false has to decide whether the sender meant live or
    /// meant nothing, and the contract says absence already answers that, so
    /// there is no work for a third state to do.
    /// </remarks>
    [Fact]
    public void AFalseReplayMarkerIsRefused()
    {
        Assert.Throws<ArgumentException>(() => AWant() with { Replay = false });
    }

    /// <summary>
    /// The replay marker did not move the contract version.
    /// </summary>
    /// <remarks>
    /// The rule in
    /// `docs/decisions/0004-what-crosses-the-seam-to-a-requests-plugin.md` says
    /// a field a receiver may ignore does not raise the number, and that the
    /// field set is version 1's until this repository publishes a release. This
    /// holds the first half where a reader of the page cannot: the eighth field
    /// is on the type and the constant is unmoved.
    ///
    /// It is deliberately the same number
    /// <see cref="AWantCarriesTheContractVersionThisBuildWrites"/> asserts. That
    /// test says what this build writes; this one says the field's arrival did
    /// not change it, and a change that moved the constant would have to answer
    /// both.
    /// </remarks>
    [Fact]
    public void TheReplayMarkerArrivedWithoutMovingTheContractVersion()
    {
        Assert.Contains("Replay", _theFieldsThatCross, StringComparer.Ordinal);
        Assert.Equal(1, WantContract.CurrentVersion);
    }

    /// <summary>
    /// A replayed want and the same want live are two different messages.
    /// </summary>
    /// <remarks>
    /// Record equality reaching the new field, which matters because the want
    /// identifier is unchanged by a replay: the two are the same want to a
    /// receiver keying on that identifier, per #99, and they are not the same
    /// message. A receiver that stores messages rather than wants would collapse
    /// them if this were not so, and it is the collapse #99 exists against
    /// pointed the other way.
    /// </remarks>
    [Fact]
    public void AReplayedWantIsNotEqualToTheSameWantLive()
    {
        var live = AWant();
        var replayed = AWant() with { Replay = true };

        Assert.NotEqual(live, replayed);
        Assert.Equal(live.WantIdentifier, replayed.WantIdentifier);
    }
}
