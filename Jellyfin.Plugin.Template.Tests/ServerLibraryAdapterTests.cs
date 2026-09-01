using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Server;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What the adapter asks the server's library, and what it does with the answer.
/// </summary>
/// <remarks>
/// <para>
/// #89's first condition, the half that is a lookup. The rule the count serves
/// is one sentence over both kinds: a title is owned when the server holds at
/// least one part of it, where a part is the film for a movie and an episode
/// for a series.
/// </para>
/// <para>
/// The assertions are over the query the adapter built rather than over what it
/// returned, wherever a query is what can be got wrong. A count that came back
/// right from a query asking the wrong thing is a test agreeing with a
/// stand-in, and the stand-in is this file's own.
/// </para>
/// <para>
/// No server is started and no library is read. What is asserted is what this
/// adapter hands the server's interface and what it makes of what comes back.
/// </para>
/// </remarks>
public class ServerLibraryAdapterTests
{
    // Written out rather than generated, because no-random refuses a generated
    // identifier in a test for the reason that rule gives: an answer a test
    // cannot predict is one a failure cannot be reproduced from. What these
    // stand for is a row the server already had, so the value never matters and
    // only its identity across two questions does.
    private static readonly Guid _theSeriesTheServerCarries =
        new Guid("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// A film the server holds comes back as one part, asked for by identifier.
    /// </summary>
    [Fact]
    public void AFilmIsAskedForByIdentifierAndItsKind()
    {
        var library = ServerLibraryAdapterStandIn.Answering(
            _ => 1,
            _ => Array.Empty<Guid>(),
            out var asked);

        var parts = new ServerLibraryAdapter(library).PartsHeld(Identity("tt0000001", "603"), DiscoverTitleKind.Movie);

        Assert.Equal(1, parts);

        var query = Assert.Single(asked.Asked);

        Assert.Equal(new[] { BaseItemKind.Movie }, query.IncludeItemTypes);
        Assert.Equal(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataProvider.Imdb.ToString()] = "tt0000001",
                [MetadataProvider.Tmdb.ToString()] = "603"
            },
            query.HasAnyProviderId);
    }

    /// <summary>
    /// The provider names on the query are the server's own rather than this plugin's.
    /// </summary>
    /// <remarks>
    /// Asserted against the server's enumeration rather than against string
    /// literals, because the literals are what the two vocabularies agreeing
    /// today looks like. A test spelling them out would go on passing on the
    /// day one of them moved.
    /// </remarks>
    [Fact]
    public void EverySourceIsAskedForUnderTheServersOwnName()
    {
        var library = ServerLibraryAdapterStandIn.Answering(
            _ => 0,
            _ => Array.Empty<Guid>(),
            out var asked);

        new ServerLibraryAdapter(library).PartsHeld(
            new DiscoverTitleIdentity(new[]
            {
                new ProviderIdentifier(MetadataSource.Imdb, "tt0000002"),
                new ProviderIdentifier(MetadataSource.Tmdb, "604"),
                new ProviderIdentifier(MetadataSource.Tvdb, "77")
            }),
            DiscoverTitleKind.Movie);

        var query = Assert.Single(asked.Asked);

        Assert.Equal(
            new[]
            {
                MetadataProvider.Imdb.ToString(),
                MetadataProvider.Tmdb.ToString(),
                MetadataProvider.Tvdb.ToString()
            }.OrderBy(name => name, StringComparer.Ordinal),
            query.HasAnyProviderId!.Keys.OrderBy(name => name, StringComparer.Ordinal));
    }

    /// <summary>
    /// Every text on every question is an identifier or the body that issued it.
    /// </summary>
    /// <remarks>
    /// #89's second condition, at the far end of the seam. What holds it is the
    /// signature - no name crosses <c>IServerLibrary</c>, which
    /// <c>TitlesTheServerAlreadyHasTests.TheSeamCarriesNoTitleText</c> asserts
    /// - and this asserts the consequence at the other end: the only text this
    /// adapter puts in front of the server is the identifiers it was handed and
    /// the names of the bodies that issued them. A free-text field set on the
    /// query for any reason reddens this, and it is read over every property of
    /// the server's query type rather than over the two or three somebody would
    /// think to name, so a property added on a later line is covered on the day
    /// it arrives.
    /// </remarks>
    [Fact]
    public void EveryTextOnAQuestionIsAnIdentifierOrTheBodyThatIssuedIt()
    {
        var library = ServerLibraryAdapterStandIn.Answering(
            _ => 0,
            _ => new[] { _theSeriesTheServerCarries },
            out var asked);

        new ServerLibraryAdapter(library).PartsHeld(Identity("tt0000003", "605"), DiscoverTitleKind.Series);

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "tt0000003",
            "605",
            MetadataProvider.Imdb.ToString(),
            MetadataProvider.Tmdb.ToString()
        };

        Assert.NotEmpty(asked.Asked);

        foreach (var query in asked.Asked)
        {
            foreach (var property in typeof(InternalItemsQuery).GetProperties().Where(property => property.CanRead))
            {
                foreach (var text in Strings(property.GetValue(query)))
                {
                    Assert.Contains(text, allowed);
                }
            }
        }
    }

    /// <summary>
    /// A series is found by identifier and its episodes are counted underneath it.
    /// </summary>
    /// <remarks>
    /// Two questions rather than one, because an episode carries no identifier
    /// for the series it belongs to. The second is bounded by what the first
    /// answered, which is the whole of why the first is asked.
    /// </remarks>
    [Fact]
    public void ASeriesIsFoundFirstAndItsEpisodesCountedUnderIt()
    {
        var series = _theSeriesTheServerCarries;

        var library = ServerLibraryAdapterStandIn.Answering(
            _ => 9,
            _ => new[] { series },
            out var asked);

        var parts = new ServerLibraryAdapter(library).PartsHeld(Identity("tt0000004", "606"), DiscoverTitleKind.Series);

        Assert.Equal(9, parts);
        Assert.Equal(2, asked.Asked.Count);

        Assert.Equal(new[] { BaseItemKind.Series }, asked.Asked[0].IncludeItemTypes);
        Assert.NotNull(asked.Asked[0].HasAnyProviderId);

        Assert.Equal(new[] { BaseItemKind.Episode }, asked.Asked[1].IncludeItemTypes);
        Assert.Equal(new[] { series }, asked.Asked[1].AncestorIds);
        Assert.Null(asked.Asked[1].HasAnyProviderId);
    }

    /// <summary>
    /// A series this server does not carry is not asked about a second time.
    /// </summary>
    /// <remarks>
    /// The cheap half of this is a question saved on the ordinary case, since a
    /// discover shelf is mostly titles the server does not have. The expensive
    /// half is the answer: an ancestor query with no ancestors in it is a query
    /// with no bound, so a second question asked anyway would count every
    /// episode on the server and report an unheld series as the most owned
    /// thing on the shelf.
    /// </remarks>
    [Fact]
    public void ASeriesTheServerDoesNotCarryIsNotAskedAboutTwice()
    {
        var library = ServerLibraryAdapterStandIn.Answering(
            _ => 4000,
            _ => Array.Empty<Guid>(),
            out var asked);

        var parts = new ServerLibraryAdapter(library).PartsHeld(Identity("tt0000005", "607"), DiscoverTitleKind.Series);

        Assert.Equal(0, parts);
        Assert.Single(asked.Asked);
    }

    /// <summary>
    /// A row the server carries for something it does not have is not a part.
    /// </summary>
    /// <remarks>
    /// An operator with missing episodes shown has a row per episode the server
    /// does not hold, and a series would otherwise be owned from the moment it
    /// was added. Asserted on every question the adapter asks rather than on
    /// the one it is easiest to forget.
    /// </remarks>
    [Fact]
    public void NoQuestionCountsWhatTheServerOnlyHasARowFor()
    {
        var library = ServerLibraryAdapterStandIn.Answering(
            _ => 3,
            _ => new[] { _theSeriesTheServerCarries },
            out var asked);

        var adapter = new ServerLibraryAdapter(library);

        adapter.PartsHeld(Identity("tt0000006", "608"), DiscoverTitleKind.Movie);
        adapter.PartsHeld(Identity("tt0000007", "609"), DiscoverTitleKind.Series);

        Assert.Equal(3, asked.Asked.Count);

        foreach (var query in asked.Asked)
        {
            Assert.False(query.IsVirtualItem);
            Assert.True(query.Recursive);
        }
    }

    /// <summary>
    /// A kind this adapter has no part for is refused rather than answered with zero.
    /// </summary>
    /// <remarks>
    /// Zero is what the server does not hold, so a kind falling through to it
    /// would put every title of that kind on every shelf while looking exactly
    /// like a filter that ran.
    /// </remarks>
    [Fact]
    public void AKindWithNoPartIsRefused()
    {
        var library = ServerLibraryAdapterStandIn.Answering(
            _ => 1,
            _ => Array.Empty<Guid>(),
            out var asked);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ServerLibraryAdapter(library).PartsHeld(Identity("tt0000008", "610"), DiscoverTitleKind.None));

        Assert.Empty(asked.Asked);
    }

    /// <summary>
    /// Nothing that could not be composed is admitted, and composing asks the library nothing.
    /// </summary>
    /// <remarks>
    /// The second half is what the registrator depends on. A plugin's
    /// registrator runs while the server is still building its container, so an
    /// adapter that read anything off the library while being constructed would
    /// be reading a half-built server. The stand-in refuses every member, so the
    /// only way this passes is by asking it nothing.
    /// </remarks>
    [Fact]
    public void WhatCannotBeComposedIsRefusedAndComposingAsksNothing()
    {
        Assert.Throws<ArgumentNullException>(() => new ServerLibraryAdapter(null!));

        var exception = Record.Exception(
            () => new ServerLibraryAdapter(ServerLibraryAdapterStandIn.RefusingEveryCall()));

        Assert.Null(exception);

        Assert.Throws<ArgumentNullException>(
            () => new ServerLibraryAdapter(ServerLibraryAdapterStandIn.RefusingEveryCall())
                .PartsHeld(null!, DiscoverTitleKind.Movie));
    }

    private static DiscoverTitleIdentity Identity(string imdb, string tmdb) =>
        new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Imdb, imdb),
            new ProviderIdentifier(MetadataSource.Tmdb, tmdb)
        });

    /// <summary>
    /// Every string reachable off one property value, one level of collection deep.
    /// </summary>
    /// <param name="value">What the property held.</param>
    /// <returns>The strings.</returns>
    /// <remarks>
    /// One level rather than a walk of the whole graph. What is being looked
    /// for is a name this adapter was never handed, and the query's own
    /// properties are strings, arrays of strings and dictionaries of them, so a
    /// deeper walk would add reach the assertion has nothing to find in.
    /// </remarks>
    private static IEnumerable<string> Strings(object? value)
    {
        switch (value)
        {
            case null:
                yield break;

            case string text:
                yield return text;

                yield break;

            case IEnumerable items:
                foreach (var item in items)
                {
                    if (item is string inside)
                    {
                        yield return inside;
                    }
                    else if (item is DictionaryEntry entry)
                    {
                        if (entry.Key is string key)
                        {
                            yield return key;
                        }

                        if (entry.Value is string held)
                        {
                            yield return held;
                        }
                    }
                }

                yield break;

            default:
                yield break;
        }
    }
}
