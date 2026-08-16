using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Surface;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What a title's address is made of, and what it survives.
/// </summary>
/// <remarks>
/// The server hashes an item's identity out of the address this plugin supplies
/// and the surface's own name. So an address that moves for any reason other
/// than the title being a different title costs a user whatever they had marked
/// on the old item, and the tests below are the shapes that would move it.
///
/// Nothing here talks to a server. What the server does with the address is
/// read out of the server's own source in <c>docs/title-identity.md</c>, with
/// the commands that read it, and it is a claim rather than something this
/// suite observes.
/// </remarks>
public class TitleAddressTests
{
    /// <summary>
    /// The instant these fixtures were fetched at.
    /// </summary>
    /// <remarks>
    /// A fixed value rather than a read of any clock, so a record built here
    /// carries the same age on every run. Nothing in this file asserts against
    /// it; it is here because the record refuses to be built without one.
    /// </remarks>
    private static readonly DateTimeOffset _fetched = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Two refreshes that found the same title address it the same way.
    /// </summary>
    /// <remarks>
    /// Everything a response is free to vary between two refreshes is varied
    /// here: the order the identifiers arrived in, the name, the year, the
    /// description and whether artwork came with it. None of it may reach the
    /// address.
    /// </remarks>
    [Fact]
    public void TwoRefreshesOfUnchangedSourceDataAddressTheTitleTheSameWay()
    {
        var first = new DiscoverTitle
        {
            Identity = IdentityOf((MetadataSource.Tmdb, "329865"), (MetadataSource.Imdb, "tt2543164")),
            Kind = DiscoverTitleKind.Movie,
            FetchedAt = _fetched,
            Name = "Arrival",
            ReleaseYear = 2016,
            Summary = "A linguist is asked to talk to something that has landed.",
            ArtworkLocation = new Uri("https://cdn.example.invalid/poster/329865.jpg")
        };

        var second = new DiscoverTitle
        {
            Identity = IdentityOf((MetadataSource.Imdb, "tt2543164"), (MetadataSource.Tmdb, "329865")),
            Kind = DiscoverTitleKind.Movie,
            FetchedAt = _fetched,
            Name = "Ankunft"
        };

        Assert.Equal(TitleAddress.For(first.Identity), TitleAddress.For(second.Identity));
    }

    /// <summary>
    /// A title that moved between shelves keeps its address.
    /// </summary>
    /// <remarks>
    /// A title leaves one shelf and joins another every time a source's
    /// trending list moves, which is most refreshes. If the shelf were in the
    /// address, an ordinary week would orphan most of the surface.
    ///
    /// The shelf is not a parameter of the address, so what is shown here is
    /// that a caller has nothing to put one in with. Below, one title is
    /// listed under two shelves and both listings address it identically. That
    /// the surface really does list it this way is #53 and #54 rather than
    /// something this test reaches.
    /// </remarks>
    [Fact]
    public void AShelfIsNotPartOfATitlesAddress()
    {
        var title = new DiscoverTitle
        {
            Identity = IdentityOf((MetadataSource.Tmdb, "329865")),
            Kind = DiscoverTitleKind.Movie,
            FetchedAt = _fetched,
            Name = "Arrival"
        };

        var trending = new SurfaceListing(
            [SurfaceEntry.Of(TitleAddress.For(title.Identity), title)],
            1);

        var arrivingSoon = new SurfaceListing(
            [
                SurfaceEntry.Of(TitleAddress.For(IdentityOf((MetadataSource.Tmdb, "693134"))), Another()),
                SurfaceEntry.Of(TitleAddress.For(title.Identity), title)
            ],
            2);

        Assert.Equal(trending.Entries[0].Address, arrivingSoon.Entries[1].Address);
        Assert.Equal("tmdb:329865", trending.Entries[0].Address.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// No part of what a client draws reaches the address.
    /// </summary>
    /// <remarks>
    /// A name is translated, so an address holding one changes the day the
    /// server's language is changed and takes every item with it. The record
    /// below carries a name, an original name and a description, and none of
    /// the three is in the address.
    /// </remarks>
    [Fact]
    public void NothingADisplayCarriesReachesTheAddress()
    {
        var title = new DiscoverTitle
        {
            Identity = IdentityOf((MetadataSource.Imdb, "tt2543164")),
            Kind = DiscoverTitleKind.Movie,
            FetchedAt = _fetched,
            Name = "Ankunft",
            OriginalName = "Arrival",
            ReleaseYear = 2016,
            Summary = "A linguist is asked to talk to something that has landed."
        };

        var address = TitleAddress.For(title.Identity).Value;

        Assert.DoesNotContain(title.Name, address, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(title.OriginalName!, address, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(title.Summary!, address, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2016", address, StringComparison.Ordinal);
    }

    /// <summary>
    /// The address carries the body that issued the identifier and the identifier itself.
    /// </summary>
    /// <param name="source">The body.</param>
    /// <param name="value">The identifier as that body spells it.</param>
    /// <param name="expected">The address.</param>
    /// <remarks>
    /// The three words are pinned deliberately. They are not a formatting
    /// choice that a later tidy-up may take: changing one changes the address
    /// of every title that body identifies, on every server that already holds
    /// them. This is the test that turns such a tidy-up red.
    /// </remarks>
    [Theory]
    [InlineData(MetadataSource.Imdb, "tt2543164", "imdb:tt2543164")]
    [InlineData(MetadataSource.Tmdb, "329865", "tmdb:329865")]
    [InlineData(MetadataSource.Tvdb, "121361", "tvdb:121361")]
    public void TheAddressIsTheBodyAndTheIdentifier(MetadataSource source, string value, string expected)
    {
        Assert.Equal(expected, TitleAddress.For(IdentityOf((source, value))).Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// The identifier an address was made from is readable back out of it.
    /// </summary>
    /// <param name="source">The body.</param>
    /// <param name="value">The identifier as that body spells it.</param>
    /// <remarks>
    /// The address is the only thing that survives the round trip through the
    /// server and a client, so a level request for a series folder arrives
    /// carrying this and nothing else.
    /// </remarks>
    [Theory]
    [InlineData(MetadataSource.Imdb, "tt2543164")]
    [InlineData(MetadataSource.Tmdb, "329865")]
    [InlineData(MetadataSource.Tvdb, "121361")]
    public void AnAddressReadsBackAsTheIdentifierItWasMadeFrom(MetadataSource source, string value)
    {
        var identifier = TitleAddress.IdentifierIn(TitleAddress.For(IdentityOf((source, value))));

        Assert.Equal(new ProviderIdentifier(source, value), identifier);
    }

    /// <summary>
    /// An identifier a body spells with a colon in it still reads back whole.
    /// </summary>
    /// <remarks>
    /// None of the three bodies does that today. It is covered because a
    /// separator read at the last colon rather than the first is the mistake
    /// that costs nothing until the day a source's form changes, and then costs
    /// every title from that source.
    /// </remarks>
    [Fact]
    public void AnIdentifierCarryingTheSeparatorReadsBackWhole()
    {
        var identifier = TitleAddress.IdentifierIn(
            TitleAddress.For(IdentityOf((MetadataSource.Tvdb, "series:121361"))));

        Assert.Equal(new ProviderIdentifier(MetadataSource.Tvdb, "series:121361"), identifier);
    }

    /// <summary>
    /// An address that is not a title's reads back as no identifier.
    /// </summary>
    /// <param name="address">An address a client could send.</param>
    /// <remarks>
    /// The root, a shelf, an address from an older version, and a body this
    /// build does not know. All of them are ordinary things for a client to
    /// send back, so the answer is an absent identifier rather than a throw.
    /// </remarks>
    [Theory]
    [InlineData("trending")]
    [InlineData("shelf:trending")]
    [InlineData("letterboxd:arrival")]
    [InlineData("imdb:")]
    [InlineData(":tt2543164")]
    [InlineData("imdbtt2543164")]
    public void AnAddressThatIsNotATitlesReadsBackAsNothing(string address)
    {
        Assert.Null(TitleAddress.IdentifierIn(SurfaceAddress.Of(address)));
    }

    /// <summary>
    /// The root is not a title, and asking is not an error.
    /// </summary>
    [Fact]
    public void TheRootReadsBackAsNothing()
    {
        Assert.Null(TitleAddress.IdentifierIn(SurfaceAddress.Root));
    }

    /// <summary>
    /// A title whose identity gains an identifier from a higher-precedence body is addressed differently.
    /// </summary>
    /// <remarks>
    /// This is the residual rather than a property worth having, and it is
    /// asserted so that it is a known cost rather than a surprise. A response
    /// that carried only a TMDB identifier on Monday and carries an IMDb one as
    /// well on Tuesday is one title throughout, and
    /// <see cref="DiscoverTitleIdentity.Agrees(DiscoverTitleIdentity)"/> says
    /// so, but its primary has moved and so has its address. What that costs
    /// and what would remove it are in <c>docs/title-identity.md</c>.
    /// </remarks>
    [Fact]
    public void AnIdentityThatGainsAHigherPrecedenceIdentifierMoves()
    {
        var onMonday = IdentityOf((MetadataSource.Tmdb, "329865"));
        var onTuesday = IdentityOf((MetadataSource.Tmdb, "329865"), (MetadataSource.Imdb, "tt2543164"));

        Assert.Equal(IdentityAgreement.SameTitle, onMonday.Agrees(onTuesday));
        Assert.NotEqual(TitleAddress.For(onMonday), TitleAddress.For(onTuesday));
    }

    /// <summary>
    /// There is no address for a title with no identity.
    /// </summary>
    [Fact]
    public void AnAbsentIdentityIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => TitleAddress.For(null!));
    }

    private static DiscoverTitle Another() => new DiscoverTitle
    {
        Identity = IdentityOf((MetadataSource.Tmdb, "693134")),
        Kind = DiscoverTitleKind.Movie,
        FetchedAt = _fetched,
        Name = "Dune: Part Two"
    };

    private static DiscoverTitleIdentity IdentityOf(params (MetadataSource Source, string Value)[] identifiers)
    {
        var supplied = new List<ProviderIdentifier>(identifiers.Length);

        foreach (var identifier in identifiers)
        {
            supplied.Add(new ProviderIdentifier(identifier.Source, identifier.Value));
        }

        return new DiscoverTitleIdentity(supplied);
    }
}
