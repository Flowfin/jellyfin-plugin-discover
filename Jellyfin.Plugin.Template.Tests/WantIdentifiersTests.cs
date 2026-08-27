using System;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Seam;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// One identifier for one want, and two for two.
/// </summary>
/// <remarks>
/// #99's cases, asserted at the derivation rather than at a refresh. There is no
/// refresh, which is #87, so the case that issue calls a refresh recreating the
/// item is written here as the identity being built a second time from the same
/// identifiers, which is what a refresh does to it. That is a weaker statement
/// than watching a refresh and it is the strongest one this tree can make:
/// whether the identity itself survives a refresh is #60's, and nothing here can
/// be stronger than that.
/// </remarks>
public class WantIdentifiersTests
{
    private static readonly Guid _oneUser = new Guid("2f1d0f9a-6f66-4a51-9a3f-9f5a6e2c1b74");
    private static readonly Guid _anotherUser = new Guid("7b3c5e21-4d18-4f70-8c2a-1e6d9b0a4f33");

    /// <summary>
    /// The same title wanted by the same user is one identifier, however many
    /// times the identity is built.
    /// </summary>
    /// <remarks>
    /// This is the first condition's stability, in the form this tree can
    /// assert. A refresh builds a second <see cref="DiscoverTitleIdentity"/> from
    /// the same response and a restart builds one in a second process; both are
    /// the same statement about a derivation that reads nothing but its two
    /// arguments. No second process is needed for the restart half, because
    /// `no-random` and `no-wall-clock` already refuse the two things that would
    /// make one run of it differ from another.
    /// </remarks>
    [Fact]
    public void OneTitleAndOneUserDeriveOneIdentifierHoweverOftenTheIdentityIsRebuilt()
    {
        var first = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "329865")), _oneUser);
        var second = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "329865")), _oneUser);

        Assert.Equal(first, second, StringComparer.Ordinal);
    }

    /// <summary>
    /// The order a source listed the identifiers in does not reach the value.
    /// </summary>
    /// <remarks>
    /// Nothing says the identifiers inside one entry arrive in one order.
    /// <see cref="DiscoverTitleIdentity"/> orders them itself, and this is that
    /// property observed from the far end rather than a second copy of it.
    /// </remarks>
    [Fact]
    public void TheOrderASourceListedTheIdentifiersInDoesNotReachTheValue()
    {
        var oneWay = WantIdentifiers.For(
            Identity(
                new ProviderIdentifier(MetadataSource.Tmdb, "329865"),
                new ProviderIdentifier(MetadataSource.Imdb, "tt2543164")),
            _oneUser);

        var theOther = WantIdentifiers.For(
            Identity(
                new ProviderIdentifier(MetadataSource.Imdb, "tt2543164"),
                new ProviderIdentifier(MetadataSource.Tmdb, "329865")),
            _oneUser);

        Assert.Equal(oneWay, theOther, StringComparer.Ordinal);
    }

    /// <summary>
    /// Two users wanting one title are two wants.
    /// </summary>
    /// <remarks>
    /// The third condition. A receiver tells them apart by this value alone,
    /// without having to read the asking user beside it and without this plugin
    /// having to explain the relationship between the two fields.
    /// </remarks>
    [Fact]
    public void TwoUsersWantingOneTitleAreTwoWants()
    {
        var mine = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "329865")), _oneUser);
        var yours = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "329865")), _anotherUser);

        Assert.NotEqual(mine, yours, StringComparer.Ordinal);
    }

    /// <summary>
    /// One user wanting two titles is two wants.
    /// </summary>
    [Fact]
    public void OneUserWantingTwoTitlesIsTwoWants()
    {
        var one = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "329865")), _oneUser);
        var other = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "329866")), _oneUser);

        Assert.NotEqual(one, other, StringComparer.Ordinal);
    }

    /// <summary>
    /// Two sources that spell one number the same way are still two titles.
    /// </summary>
    /// <remarks>
    /// The identifier a source issues means nothing without the body that issued
    /// it, which is why <see cref="ProviderIdentifier"/> is one value rather than
    /// two fields side by side. Dropping the source from the derivation is the
    /// one-line mistake this refuses, and it stays invisible until two sources
    /// collide on a number.
    /// </remarks>
    [Fact]
    public void TwoSourcesSpellingOneNumberTheSameWayAreStillTwoTitles()
    {
        var fromOne = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "1399")), _oneUser);
        var fromAnother = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tvdb, "1399")), _oneUser);

        Assert.NotEqual(fromOne, fromAnother, StringComparer.Ordinal);
    }

    /// <summary>
    /// A refresh that brought one identifier more does not produce a second want,
    /// where the one it brought is not the primary.
    /// </summary>
    /// <remarks>
    /// This is the case that decides the derivation. A response carrying a
    /// TheTVDB identifier this week and not last week is ordinary, and a value
    /// derived from the whole set would move for it and hand a receiver a second
    /// want for a title nobody asked for twice.
    /// </remarks>
    [Fact]
    public void ARefreshThatAddsALowerPrecedenceIdentifierDoesNotMoveTheValue()
    {
        var before = WantIdentifiers.For(
            Identity(new ProviderIdentifier(MetadataSource.Tmdb, "1399")),
            _oneUser);

        var after = WantIdentifiers.For(
            Identity(
                new ProviderIdentifier(MetadataSource.Tmdb, "1399"),
                new ProviderIdentifier(MetadataSource.Tvdb, "121361")),
            _oneUser);

        Assert.Equal(before, after, StringComparer.Ordinal);
    }

    /// <summary>
    /// A refresh that brought an identifier the precedence puts first does move
    /// the value, and that is the residual rather than a defect.
    /// </summary>
    /// <remarks>
    /// Asserted so it is a property somebody chose rather than one a later reader
    /// discovers. The server's own item identity is derived from the same
    /// primary, which is #60, so the moment this moves is the moment the item a
    /// user was looking at is a different item, and a second want is what a
    /// second item means.
    /// </remarks>
    [Fact]
    public void ARefreshThatAddsAHigherPrecedenceIdentifierMovesTheValue()
    {
        var before = WantIdentifiers.For(
            Identity(new ProviderIdentifier(MetadataSource.Tmdb, "329865")),
            _oneUser);

        var after = WantIdentifiers.For(
            Identity(
                new ProviderIdentifier(MetadataSource.Tmdb, "329865"),
                new ProviderIdentifier(MetadataSource.Imdb, "tt2543164")),
            _oneUser);

        Assert.NotEqual(before, after, StringComparer.Ordinal);
    }

    /// <summary>
    /// A source identifier carrying the separator comes back whole, and two of
    /// them do not collide.
    /// </summary>
    /// <remarks>
    /// <see cref="ProviderIdentifier"/> keeps a value exactly as the source
    /// spelled it and normalises nothing, so nothing in this plugin promises a
    /// value free of any particular character. The free-form part is therefore
    /// last, and everything after the second separator is it. What this refuses
    /// is the value being split or shortened on the way in: taking the part
    /// before the first separator is the plausible normalisation, and it turns
    /// two titles into one.
    /// </remarks>
    [Fact]
    public void AnIdentifierCarryingTheSeparatorDoesNotCollideWithAnother()
    {
        var one = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "1399:2")), _oneUser);
        var other = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "1399")), _oneUser);

        Assert.NotEqual(one, other, StringComparer.Ordinal);
        Assert.EndsWith("1399:2", one, StringComparison.Ordinal);
    }

    /// <summary>
    /// The value is the source, the user and the identifier, in that order.
    /// </summary>
    /// <remarks>
    /// The shape is part of the contract rather than an implementation detail,
    /// which the decision note states in as many words, so it is asserted rather
    /// than left to be read off the method. A release that recomputes this is
    /// breaking, and this is the test that says so out loud.
    /// </remarks>
    [Fact]
    public void TheValueNamesTheSourceTheUserAndTheIdentifierInThatOrder()
    {
        var value = WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "329865")), _oneUser);

        Assert.Equal("Tmdb:2f1d0f9a6f664a519a3f9f5a6e2c1b74:329865", value, StringComparer.Ordinal);
    }

    /// <summary>
    /// A want with no user is refused rather than derived.
    /// </summary>
    /// <remarks>
    /// <see cref="Guid.Empty"/> is what an unset field reads as, and every want
    /// derived from it would carry one identifier, which is every user's wants
    /// collapsing into one. <see cref="Want.AskingUser"/> refuses the same value
    /// at the message; refusing it here as well means the collapse cannot be
    /// built in the first place.
    /// </remarks>
    [Fact]
    public void AWantWithNoUserIsRefused()
    {
        Assert.Throws<ArgumentException>(
            () => WantIdentifiers.For(Identity(new ProviderIdentifier(MetadataSource.Tmdb, "329865")), Guid.Empty));
    }

    /// <summary>
    /// No identity at all is refused rather than read as a title with none.
    /// </summary>
    [Fact]
    public void NoIdentityAtAllIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => WantIdentifiers.For(null!, _oneUser));
    }

    /// <summary>
    /// What this derives is what the message carries.
    /// </summary>
    /// <remarks>
    /// The two are written apart on purpose: <see cref="Want"/> refuses a blank
    /// identifier and stores what it was handed, and deriving one is this type's.
    /// A test that never put the two together would let the derivation produce
    /// something the message refuses.
    /// </remarks>
    [Fact]
    public void TheDerivedValueIsWhatTheMessageCarries()
    {
        var identity = Identity(new ProviderIdentifier(MetadataSource.Tmdb, "329865"));

        var want = new Want
        {
            Identity = identity,
            Kind = DiscoverTitleKind.Movie,
            Name = "Arrival",
            ReleaseYear = 2016,
            AskingUser = _oneUser,
            WantIdentifier = WantIdentifiers.For(identity, _oneUser)
        };

        Assert.Equal(WantIdentifiers.For(identity, _oneUser), want.WantIdentifier, StringComparer.Ordinal);
    }

    private static DiscoverTitleIdentity Identity(params ProviderIdentifier[] identifiers) =>
        new DiscoverTitleIdentity(identifiers);
}
