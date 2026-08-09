using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Catalogue;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What a discover title is, and what makes two of them one title.
/// </summary>
/// <remarks>
/// Every record below is built the way an adapter would build one out of a
/// response, by hand. There is no parser and no adapter in the tree yet, so
/// what is proven here is the record and its identity rather than the mapping
/// from a source's wire format onto them, which is #73 and #74.
/// </remarks>
public class DiscoverTitleTests
{
    /// <summary>
    /// Two responses describing the same title produce records that compare equal on identity.
    /// </summary>
    /// <remarks>
    /// The case a refresh meets every time it runs, and the one a shelf meets
    /// when a title is on two of them. Everything a response is free to vary is
    /// varied here: the order the identifiers were listed in, the name, the
    /// description and whether artwork came with it. If any of that reached
    /// identity, a title would change its identity between two refreshes that
    /// found the same film, and the server would create it again and orphan
    /// what a user had marked on the first one.
    /// </remarks>
    [Fact]
    public void TwoResponsesForOneTitleCompareEqualOnIdentity()
    {
        var first = new DiscoverTitle
        {
            Identity = IdentityOf((MetadataSource.Tmdb, "329865"), (MetadataSource.Imdb, "tt2543164")),
            Kind = DiscoverTitleKind.Movie,
            Name = "Arrival",
            ReleaseYear = 2016,
            Summary = "A linguist is asked to talk to something that has landed.",
            ArtworkLocation = new Uri("https://image.example/poster/329865.jpg")
        };

        var second = new DiscoverTitle
        {
            Identity = IdentityOf((MetadataSource.Imdb, "tt2543164"), (MetadataSource.Tmdb, "329865")),
            Kind = DiscoverTitleKind.Movie,
            Name = "Ankunft",
            ReleaseYear = 2016
        };

        Assert.Equal(first.Identity, second.Identity);
        Assert.Equal(first.Identity.GetHashCode(), second.Identity.GetHashCode());
        Assert.Equal(IdentityAgreement.SameTitle, first.Identity.Agrees(second.Identity));
    }

    /// <summary>
    /// The record itself is not equal when anything on it differs.
    /// </summary>
    /// <remarks>
    /// The other half of the test above, and the reason identity is a field
    /// rather than the record's own equality. Whole-record equality answers
    /// "is this the same fetched thing"; identity answers "is this the same
    /// title". A reader who took one for the other would either merge two
    /// translations of one film or store one film twice.
    /// </remarks>
    [Fact]
    public void ARecordThatDiffersOnlyInItsNameIsADifferentValueAndTheSameTitle()
    {
        var english = ArrivalFromTmdb();
        var german = english with { Name = "Ankunft" };

        Assert.NotEqual(english, german);
        Assert.Equal(english.Identity, german.Identity);
    }

    /// <summary>
    /// Two sources that both know a title agree about it.
    /// </summary>
    /// <remarks>
    /// The sets are not the same set, so the identities are not equal, and that
    /// is correct: one of them holds an identifier the other does not.
    /// Whether they are one title is the separate question, and the answer is
    /// not equality because it is not transitive. A knows B, B knows C, A and C
    /// share nothing.
    /// </remarks>
    [Fact]
    public void TwoSourcesThatBothKnowATitleAgreeWithoutBeingEqual()
    {
        var fromTmdb = IdentityOf((MetadataSource.Tmdb, "329865"), (MetadataSource.Imdb, "tt2543164"));
        var fromTvdb = IdentityOf((MetadataSource.Tvdb, "352206"), (MetadataSource.Imdb, "tt2543164"));

        Assert.NotEqual(fromTmdb, fromTvdb);
        Assert.Equal(IdentityAgreement.SameTitle, fromTmdb.Agrees(fromTvdb));
        Assert.Equal(IdentityAgreement.SameTitle, fromTvdb.Agrees(fromTmdb));
    }

    /// <summary>
    /// Two sources that disagree about a shared identifier are kept apart.
    /// </summary>
    /// <remarks>
    /// They agree on TMDB and disagree on IMDb, so one of them is wrong and
    /// nothing here can say which. Merging on the identifier they agree about
    /// would carry the contradiction into one record where no later reader
    /// could see it.
    /// </remarks>
    [Fact]
    public void ASharedIdentifierWithTwoValuesIsAContradiction()
    {
        var one = IdentityOf((MetadataSource.Tmdb, "329865"), (MetadataSource.Imdb, "tt2543164"));
        var other = IdentityOf((MetadataSource.Tmdb, "329865"), (MetadataSource.Imdb, "tt0000001"));

        Assert.Equal(IdentityAgreement.Contradiction, one.Agrees(other));
        Assert.Equal(IdentityAgreement.Contradiction, other.Agrees(one));
    }

    /// <summary>
    /// Sharing no source is not the same answer as being different titles.
    /// </summary>
    /// <remarks>
    /// Two responses about one film, one carrying only a TMDB identifier and
    /// the other only a TheTVDB one, land here. Reporting them as different
    /// titles would be this plugin asserting something it has no way to know.
    /// </remarks>
    [Fact]
    public void NoSourceInCommonIsNotComparable()
    {
        var one = IdentityOf((MetadataSource.Tmdb, "329865"));
        var other = IdentityOf((MetadataSource.Tvdb, "352206"));

        Assert.Equal(IdentityAgreement.NotComparable, one.Agrees(other));
        Assert.Equal(IdentityAgreement.NotComparable, other.Agrees(one));
    }

    /// <summary>
    /// The identifier that stands for a title is the highest-precedence one it holds.
    /// </summary>
    /// <remarks>
    /// Stated as a test rather than as a comment on the list, because the order
    /// is the part of this type a later edit is most likely to change without
    /// meaning to, and #60 derives a stored value from what it returns.
    /// </remarks>
    [Fact]
    public void ThePrimaryIdentifierFollowsThePrecedenceOrder()
    {
        Assert.Equal(
            new[] { MetadataSource.Imdb, MetadataSource.Tmdb, MetadataSource.Tvdb },
            DiscoverTitleIdentity.Precedence);

        Assert.Equal(
            new ProviderIdentifier(MetadataSource.Imdb, "tt2543164"),
            IdentityOf((MetadataSource.Tvdb, "352206"), (MetadataSource.Tmdb, "329865"), (MetadataSource.Imdb, "tt2543164")).Primary);

        Assert.Equal(
            new ProviderIdentifier(MetadataSource.Tmdb, "329865"),
            IdentityOf((MetadataSource.Tvdb, "352206"), (MetadataSource.Tmdb, "329865")).Primary);

        Assert.Equal(
            new ProviderIdentifier(MetadataSource.Tvdb, "352206"),
            IdentityOf((MetadataSource.Tvdb, "352206")).Primary);
    }

    /// <summary>
    /// A source repeating one identifier is not a title with two of them.
    /// </summary>
    /// <remarks>
    /// A response listing the same pair twice is ordinary. A response listing
    /// one source twice with two values is a response disagreeing with itself,
    /// and storing either of the two would be picking one at random.
    /// </remarks>
    [Fact]
    public void OneSourceMayNotSupplyTwoIdentifiers()
    {
        var repeated = IdentityOf((MetadataSource.Tmdb, "329865"), (MetadataSource.Tmdb, "329865"));

        Assert.Equal(IdentityOf((MetadataSource.Tmdb, "329865")), repeated);

        Assert.Throws<ArgumentException>(
            () => IdentityOf((MetadataSource.Tmdb, "329865"), (MetadataSource.Tmdb, "1")));
    }

    /// <summary>
    /// An identity that identifies nothing is refused rather than stored.
    /// </summary>
    /// <remarks>
    /// The empty set is the dangerous one: two of them compare equal, so every
    /// title a source gave no identifier for would collapse into one record.
    /// The unset source and the blank value are the two ways a field that was
    /// never filled in arrives here.
    /// </remarks>
    [Fact]
    public void AnIdentityWithNothingInItIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new DiscoverTitleIdentity(Array.Empty<ProviderIdentifier>()));
        Assert.Throws<ArgumentException>(() => IdentityOf((MetadataSource.None, "329865")));
        Assert.Throws<ArgumentException>(() => IdentityOf((MetadataSource.Tmdb, "   ")));
        Assert.Throws<ArgumentNullException>(() => new DiscoverTitleIdentity(null!));
    }

    /// <summary>
    /// The record carries a schema version from its first commit.
    /// </summary>
    /// <remarks>
    /// A catalogue written before there is a version rule is a catalogue
    /// nobody can migrate afterwards, which is what #67 needs to switch on.
    /// </remarks>
    [Fact]
    public void TheRecordCarriesASchemaVersion()
    {
        Assert.Equal(1, DiscoverTitle.CurrentSchemaVersion);
        Assert.Equal(DiscoverTitle.CurrentSchemaVersion, ArrivalFromTmdb().SchemaVersion);
    }

    /// <summary>
    /// Absent is a null, and never an empty string or a zero.
    /// </summary>
    /// <remarks>
    /// A source that returned no year is a different thing from one that
    /// returned the year zero, and only one of the two is worth asking again
    /// about. The same for a description nobody has written yet and one that is
    /// empty.
    /// </remarks>
    [Fact]
    public void WhatASourceDidNotSupplyIsAbsentRatherThanBlank()
    {
        var sparse = new DiscoverTitle
        {
            Identity = IdentityOf((MetadataSource.Tmdb, "1241982")),
            Kind = DiscoverTitleKind.Series,
            Name = "Something announced and not released"
        };

        Assert.Null(sparse.OriginalName);
        Assert.Null(sparse.ReleaseYear);
        Assert.Null(sparse.Summary);
        Assert.Null(sparse.ArtworkLocation);
    }

    /// <summary>
    /// The fields that may not be absent refuse to be blank.
    /// </summary>
    /// <remarks>
    /// Each of these is what an unset field reads as. A record that got through
    /// with one of them would be drawn by a client as an empty row, an item of
    /// whatever type its default is, or an image that never loads, and each of
    /// those reads to a user as the plugin being broken rather than as the
    /// response having been.
    /// </remarks>
    [Fact]
    public void ARecordThatWasNotFilledInIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ArrivalFromTmdb() with { Identity = null! });
        Assert.Throws<ArgumentOutOfRangeException>(() => ArrivalFromTmdb() with { Kind = DiscoverTitleKind.None });
        Assert.Throws<ArgumentException>(() => ArrivalFromTmdb() with { Name = "  " });
        Assert.Throws<ArgumentException>(() => ArrivalFromTmdb() with { ArtworkLocation = new Uri("/poster/329865.jpg", UriKind.Relative) });
    }

    private static DiscoverTitle ArrivalFromTmdb() => new()
    {
        Identity = IdentityOf((MetadataSource.Tmdb, "329865"), (MetadataSource.Imdb, "tt2543164")),
        Kind = DiscoverTitleKind.Movie,
        Name = "Arrival",
        ReleaseYear = 2016
    };

    private static DiscoverTitleIdentity IdentityOf(params (MetadataSource Source, string Value)[] identifiers)
    {
        var pairs = new List<ProviderIdentifier>(identifiers.Length);

        foreach (var (source, value) in identifiers)
        {
            pairs.Add(new ProviderIdentifier(source, value));
        }

        return new DiscoverTitleIdentity(pairs);
    }
}
