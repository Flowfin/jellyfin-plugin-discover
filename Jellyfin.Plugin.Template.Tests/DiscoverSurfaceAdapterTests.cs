using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Surface;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The one crossing between this plugin's vocabulary and the server's, tested from both sides of it.
/// </summary>
/// <remarks>
/// This is the only test file in the tree that names a server channel type, and
/// it is excepted from `no-channel-type-outside-surface` by name for that
/// reason. Every other test about the surface goes through
/// <see cref="SurfaceThatAnswersFromWhatATestGaveIt"/> and names none, which is
/// what #49 asks for and what #52 exists to make possible.
///
/// What is asserted here is the conversion and nothing above it. Which shelves
/// there are is #86, what a title carries is #55, and what the surface is
/// called is #53. A test here that asserted any of those would be asserting a
/// value the fake was handed one line earlier.
/// </remarks>
public class DiscoverSurfaceAdapterTests
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

    private static readonly Guid _somebody = new Guid("2c1f0f4a-6d5e-4b2a-9f0c-73f5b8a1d900");
    private static readonly Guid _somebodyElse = new Guid("9b6e1d33-5a20-4f81-8c44-1e0d7a6f2b11");

    /// <summary>
    /// The first four bytes of a PNG, which is enough to tell one byte sequence from another.
    /// </summary>
    private static readonly byte[] _pngBytes = new byte[] { 137, 80, 78, 71 };

    /// <summary>
    /// The whole sequence of calls one request for a level is allowed to make.
    /// </summary>
    private static readonly string[] _oneListCall = new[] { "surface.ListAsync" };

    /// <summary>
    /// A request naming no folder is the top level.
    /// </summary>
    /// <remarks>
    /// The server asks this way every time a user opens the surface, and it is
    /// the one address that arrives as an absence rather than as a value. An
    /// adapter that turned it into an address spelled with an empty string
    /// would send the surface looking for a shelf nobody has.
    /// </remarks>
    /// <param name="folderId">The folder the server named, which is nothing in both spellings it uses.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ARequestNamingNoFolderIsTheTopLevel(string? folderId)
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        var adapter = new DiscoverSurfaceAdapter(surface);

        await adapter.GetChannelItems(
            new InternalChannelItemQuery { FolderId = folderId, UserId = _somebody },
            CancellationToken.None).ConfigureAwait(true);

        Assert.True(surface.LastRequest!.Value.Parent.IsRoot);
    }

    /// <summary>
    /// A request naming a folder asks the surface for that level, with the paging the server sent.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARequestNamingAFolderCarriesItAndItsPagingThrough()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        var adapter = new DiscoverSurfaceAdapter(surface);

        await adapter.GetChannelItems(
            new InternalChannelItemQuery
            {
                FolderId = "shelf:trending",
                UserId = _somebody,
                StartIndex = 20,
                Limit = 10
            },
            CancellationToken.None).ConfigureAwait(true);

        var request = surface.LastRequest!.Value;

        Assert.Equal("shelf:trending", request.Parent.Value);
        Assert.Equal(_somebody, request.UserId);
        Assert.Equal(20, request.StartIndex);
        Assert.Equal(10, request.Limit);
    }

    /// <summary>
    /// A level the surface does not recognise comes back empty rather than as a failure, and carries no total.
    /// </summary>
    /// <remarks>
    /// The case a client produces on its own: an address it kept from a version
    /// whose shelf has since been removed. #54 asks for empty rather than a
    /// throw, and this is the boundary where a throw would reach the server.
    /// The total is the second half of #54's answer and it survives this
    /// boundary: the server's own field is nullable, so "there is no such
    /// level" reaches a client as an absent total rather than being flattened
    /// to zero here.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ALevelTheSurfaceDoesNotRecogniseComesBackEmptyAndWithoutATotal()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        var adapter = new DiscoverSurfaceAdapter(surface);

        var result = await adapter.GetChannelItems(
            new InternalChannelItemQuery { FolderId = "shelf:that-was-removed", UserId = _somebody },
            CancellationToken.None).ConfigureAwait(true);

        Assert.Empty(result.Items);
        Assert.Null(result.TotalRecordCount);
    }

    /// <summary>
    /// A shelf that is configured and holds nothing comes back with a total of zero, not with no total.
    /// </summary>
    /// <remarks>
    /// The other half of #54's answer, at the same boundary and through the
    /// same method as the test above. Written as a pair on purpose: either one
    /// alone passes on a surface that flattens both cases into one answer, and
    /// what the pair asserts is that they stay two.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AShelfThatIsConfiguredAndEmptyComesBackWithATotalOfZero()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        surface.Put(SurfaceAddress.Of("shelf:configured-and-empty"), SurfaceListing.EmptyLevel);

        var result = await new DiscoverSurfaceAdapter(surface).GetChannelItems(
            new InternalChannelItemQuery { FolderId = "shelf:configured-and-empty", UserId = _somebody },
            CancellationToken.None).ConfigureAwait(true);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalRecordCount);
    }

    /// <summary>
    /// A shelf becomes a folder a client can open, carrying its address and the operator's name for it.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AShelfBecomesAFolderCarryingItsAddressAndName()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        surface.Put(
            SurfaceAddress.Root,
            new SurfaceListing(
                new[] { SurfaceEntry.Shelf(SurfaceAddress.Of("shelf:trending"), "Trending this week") },
                1));

        var result = await new DiscoverSurfaceAdapter(surface).GetChannelItems(
            new InternalChannelItemQuery { UserId = _somebody },
            CancellationToken.None).ConfigureAwait(true);

        var item = Assert.Single(result.Items);

        Assert.Equal("shelf:trending", item.Id);
        Assert.Equal("Trending this week", item.Name);
        Assert.Equal(ChannelItemType.Folder, item.Type);
        Assert.Equal(ChannelFolderType.Container, item.FolderType);
        Assert.Equal(1, result.TotalRecordCount);
    }

    /// <summary>
    /// A film becomes the shape the server materialises as a movie, with every field the record carried.
    /// </summary>
    /// <remarks>
    /// The identifiers are the part worth watching. They are keyed by the
    /// server's own spelling for each body, because #89 compares a shelf
    /// against the library on them and #94 hands them over the seam. A key this
    /// adapter invented would match nothing in either place and would look
    /// right in every test that also invented it.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task AFilmBecomesAMovieCarryingItsFieldsAndItsIdentifiers()
    {
        var title = new DiscoverTitle
        {
            Identity = new DiscoverTitleIdentity(
                new[]
                {
                    new ProviderIdentifier(MetadataSource.Tmdb, "329865"),
                    new ProviderIdentifier(MetadataSource.Imdb, "tt2543164")
                }),
            Kind = DiscoverTitleKind.Movie,
            FetchedAt = _fetched,
            Name = "Arrival",
            OriginalName = "Arrival",
            ReleaseYear = 2016,
            Summary = "A linguist is asked to talk to something that has landed.",
            ArtworkLocation = new Uri("https://cdn.example.invalid/poster/329865.jpg")
        };

        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        surface.Put(
            SurfaceAddress.Of("shelf:trending"),
            new SurfaceListing(new[] { SurfaceEntry.Of(SurfaceAddress.Of("tmdb:329865"), title) }, 1));

        var result = await new DiscoverSurfaceAdapter(surface).GetChannelItems(
            new InternalChannelItemQuery { FolderId = "shelf:trending", UserId = _somebody },
            CancellationToken.None).ConfigureAwait(true);

        var item = Assert.Single(result.Items);

        Assert.Equal("tmdb:329865", item.Id);
        Assert.Equal("Arrival", item.Name);
        Assert.Equal("Arrival", item.OriginalTitle);
        Assert.Equal(2016, item.ProductionYear);
        Assert.Equal("A linguist is asked to talk to something that has landed.", item.Overview);
        Assert.Equal("https://cdn.example.invalid/poster/329865.jpg", item.ImageUrl);
        Assert.Equal(ChannelItemType.Media, item.Type);
        Assert.Equal(ChannelMediaType.Video, item.MediaType);
        Assert.Equal(ChannelMediaContentType.Movie, item.ContentType);

        Assert.Equal("tt2543164", item.ProviderIds[MetadataProvider.Imdb.ToString()]);
        Assert.Equal("329865", item.ProviderIds[MetadataProvider.Tmdb.ToString()]);
    }

    /// <summary>
    /// A series becomes the folder shape, because that is the only one the server materialises as a series.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASeriesBecomesTheFolderShapeRatherThanMedia()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        surface.Put(
            SurfaceAddress.Root,
            new SurfaceListing(
                new[]
                {
                    SurfaceEntry.Of(
                        SurfaceAddress.Of("tmdb:1399"),
                        new DiscoverTitle
                        {
                            Identity = new DiscoverTitleIdentity(new[] { new ProviderIdentifier(MetadataSource.Tmdb, "1399") }),
                            Kind = DiscoverTitleKind.Series,
                            FetchedAt = _fetched,
                            Name = "A series nobody here has"
                        })
                },
                1));

        var result = await new DiscoverSurfaceAdapter(surface).GetChannelItems(
            new InternalChannelItemQuery { UserId = _somebody },
            CancellationToken.None).ConfigureAwait(true);

        var item = Assert.Single(result.Items);

        Assert.Equal(ChannelItemType.Folder, item.Type);
        Assert.Equal(ChannelFolderType.Series, item.FolderType);
    }

    /// <summary>
    /// A title the source gave no artwork for produces an item with no artwork rather than a broken location.
    /// </summary>
    /// <remarks>
    /// The absence has to survive the crossing. An empty string here is a
    /// location the server would try to fetch, which is the broken picture #62
    /// asks not to produce.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ATitleWithNoArtworkProducesAnItemWithNone()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        surface.Put(
            SurfaceAddress.Root,
            new SurfaceListing(
                new[]
                {
                    SurfaceEntry.Of(
                        SurfaceAddress.Of("tmdb:1"),
                        new DiscoverTitle
                        {
                            Identity = new DiscoverTitleIdentity(new[] { new ProviderIdentifier(MetadataSource.Tmdb, "1") }),
                            Kind = DiscoverTitleKind.Movie,
                            FetchedAt = _fetched,
                            Name = "Announced and not released"
                        })
                },
                1));

        var result = await new DiscoverSurfaceAdapter(surface).GetChannelItems(
            new InternalChannelItemQuery { UserId = _somebody },
            CancellationToken.None).ConfigureAwait(true);

        var item = Assert.Single(result.Items);

        Assert.Null(item.ImageUrl);
        Assert.Null(item.OriginalTitle);
        Assert.Null(item.Overview);
        Assert.Null(item.ProductionYear);
    }

    /// <summary>
    /// A surface that does not know how large a level is says so, rather than saying zero.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASurfaceThatDoesNotKnowTheTotalPassesThatOn()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        surface.Put(
            SurfaceAddress.Root,
            new SurfaceListing(new[] { SurfaceEntry.Shelf(SurfaceAddress.Of("shelf:one"), "One") }, null));

        var result = await new DiscoverSurfaceAdapter(surface).GetChannelItems(
            new InternalChannelItemQuery { UserId = _somebody },
            CancellationToken.None).ConfigureAwait(true);

        Assert.Null(result.TotalRecordCount);
    }

    /// <summary>
    /// A user identifier the server cannot have meant is answered no, and the surface is never asked.
    /// </summary>
    /// <remarks>
    /// This is asked while the server is assembling a user's list of libraries.
    /// A throw here is a user with no libraries at all rather than a user
    /// without this one, so the malformed case has to be answered rather than
    /// refused. The surface not being asked is the second half: a fake
    /// answering yes to everything must not be able to make this test pass.
    /// </remarks>
    [Fact]
    public void AUserIdentifierThatIsNotOneIsAnsweredNoWithoutAskingTheSurface()
    {
        var log = new CallLog();
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(log);
        surface.Allow(_somebody);

        Assert.False(new DiscoverSurfaceAdapter(surface).IsEnabledFor("not a user identifier"));
        Assert.Empty(log.Calls);
    }

    /// <summary>
    /// Whether a user sees the surface is the surface's answer and not the adapter's.
    /// </summary>
    [Fact]
    public void WhoSeesTheSurfaceIsTheSurfacesAnswer()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        surface.Allow(_somebody);

        var adapter = new DiscoverSurfaceAdapter(surface);

        Assert.True(adapter.IsEnabledFor(_somebody.ToString()));
        Assert.False(adapter.IsEnabledFor(_somebodyElse.ToString()));
    }

    /// <summary>
    /// What the surface calls itself reaches the server unchanged.
    /// </summary>
    [Fact]
    public void WhatTheSurfaceCallsItselfReachesTheServerUnchanged()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog())
        {
            Description = new SurfaceDescription
            {
                Name = "Discover",
                Summary = "Titles this server does not have.",
                CatalogueVersion = "2026-08-10T00:00:00Z",
                Audience = SurfaceAudience.General,
                HomePage = new Uri("https://cdn.example.invalid/discover")
            }
        };

        var adapter = new DiscoverSurfaceAdapter(surface);

        Assert.Equal("Discover", adapter.Name);
        Assert.Equal("Titles this server does not have.", adapter.Description);
        Assert.Equal("2026-08-10T00:00:00Z", adapter.DataVersion);
        Assert.Equal("https://cdn.example.invalid/discover", adapter.HomePageUrl);
        Assert.Equal(ChannelParentalRating.GeneralAudience, adapter.ParentalRating);
    }

    /// <summary>
    /// A surface naming no home page hands the server an empty string rather than nothing at all.
    /// </summary>
    [Fact]
    public void ASurfaceNamingNoHomePageHandsOverAnEmptyString()
    {
        var adapter = new DiscoverSurfaceAdapter(new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog()));

        Assert.Equal(string.Empty, adapter.HomePageUrl);
    }

    /// <summary>
    /// An adults-only surface says so to the server.
    /// </summary>
    [Fact]
    public void AnAdultsOnlySurfaceSaysSo()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog())
        {
            Description = new SurfaceDescription
            {
                Name = "Discover",
                Summary = "Titles this server does not have.",
                CatalogueVersion = "1",
                Audience = SurfaceAudience.Adult
            }
        };

        Assert.Equal(ChannelParentalRating.Adult, new DiscoverSurfaceAdapter(surface).ParentalRating);
    }

    /// <summary>
    /// The features the server is told are the ones the surface stated, and the rest are off.
    /// </summary>
    /// <remarks>
    /// The sort fields are the part that matters. The order is the plugin's,
    /// per #54, so a declared sort field is a control a client would offer that
    /// changes nothing, and an empty list is the difference between a surface
    /// that says it cannot sort and one that says it can and does not.
    /// </remarks>
    [Fact]
    public void TheFeaturesTheServerIsToldAreTheOnesTheSurfaceStated()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog())
        {
            Capabilities = new SurfaceCapabilities
            {
                TitleKinds = new[] { DiscoverTitleKind.Movie },
                MaximumPageSize = 50
            }
        };

        var features = new DiscoverSurfaceAdapter(surface).GetChannelFeatures();

        Assert.Equal(50, features.MaxPageSize);
        Assert.Empty(features.DefaultSortFields);
        Assert.False(features.SupportsSortOrderToggle);
        Assert.False(features.SupportsContentDownloading);
        Assert.Null(features.AutoRefreshLevels);
        Assert.Null(features.DailyDownloadLimit);
        Assert.Equal(new[] { ChannelMediaContentType.Movie }, features.ContentTypes);
        Assert.Equal(new[] { ChannelMediaType.Video }, features.MediaTypes);
    }

    /// <summary>
    /// A surface offering only series declares no content type, because the server has none for one.
    /// </summary>
    [Fact]
    public void ASurfaceOfferingOnlySeriesDeclaresNoContentType()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog())
        {
            Capabilities = new SurfaceCapabilities { TitleKinds = new[] { DiscoverTitleKind.Series } }
        };

        var features = new DiscoverSurfaceAdapter(surface).GetChannelFeatures();

        Assert.Empty(features.ContentTypes);
        Assert.Empty(features.MediaTypes);
    }

    /// <summary>
    /// The pictures the surface has are the ones the server is offered.
    /// </summary>
    [Fact]
    public void ThePicturesTheSurfaceHasAreTheOnesTheServerIsOffered()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        surface.Put(SurfaceImageKind.Primary, new SurfaceImage(SurfaceImageFormat.Png, new byte[] { 1, 2, 3 }));
        surface.Put(SurfaceImageKind.Thumb, new SurfaceImage(SurfaceImageFormat.Jpeg, new byte[] { 4 }));

        var offered = new DiscoverSurfaceAdapter(surface).GetSupportedChannelImages().ToArray();

        Assert.Equal(2, offered.Length);
        Assert.Contains(ImageType.Primary, offered);
        Assert.Contains(ImageType.Thumb, offered);
    }

    /// <summary>
    /// A picture the surface has arrives with its bytes and its format.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task APictureTheSurfaceHasArrivesWithItsBytesAndItsFormat()
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        surface.Put(SurfaceImageKind.Primary, new SurfaceImage(SurfaceImageFormat.Png, _pngBytes));

        var response = await new DiscoverSurfaceAdapter(surface)
            .GetChannelImage(ImageType.Primary, CancellationToken.None).ConfigureAwait(true);

        Assert.True(response.HasImage);
        Assert.Equal(ImageFormat.Png, response.Format);

        using var read = new MemoryStream();
        await response.Stream.CopyToAsync(read, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(_pngBytes, read.ToArray());
    }

    /// <summary>
    /// A picture the surface does not have, and one it has no member for at all, are both answered as absent.
    /// </summary>
    /// <remarks>
    /// The server asks for every kind of picture it knows about, most of which
    /// this surface has no member for. Refusing those would turn an ordinary
    /// question into an error in the server's log on every scan.
    /// </remarks>
    /// <param name="type">The kind of picture the server asked for.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData(ImageType.Backdrop)]
    [InlineData(ImageType.Logo)]
    [InlineData(ImageType.Banner)]
    public async Task APictureTheSurfaceDoesNotHaveIsAnsweredAsAbsent(ImageType type)
    {
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog());
        surface.Put(SurfaceImageKind.Primary, new SurfaceImage(SurfaceImageFormat.Png, new byte[] { 1 }));

        var response = await new DiscoverSurfaceAdapter(surface)
            .GetChannelImage(type, CancellationToken.None).ConfigureAwait(true);

        Assert.False(response.HasImage);
    }

    /// <summary>
    /// The adapter refuses a surface that is not there rather than failing on the first call.
    /// </summary>
    [Fact]
    public void TheAdapterRefusesASurfaceThatIsNotThere()
    {
        Assert.Throws<ArgumentNullException>(() => new DiscoverSurfaceAdapter(null!));
    }

    /// <summary>
    /// The adapter refuses a query that is not there rather than reading it.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task TheAdapterRefusesAQueryThatIsNotThere()
    {
        var adapter = new DiscoverSurfaceAdapter(new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog()));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => adapter.GetChannelItems(null!, CancellationToken.None)).ConfigureAwait(true);
    }

    /// <summary>
    /// Paging the server could not have meant is refused rather than carried into the surface.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task PagingTheServerCouldNotHaveMeantIsRefused()
    {
        var adapter = new DiscoverSurfaceAdapter(new SurfaceThatAnswersFromWhatATestGaveIt(new CallLog()));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.GetChannelItems(
                new InternalChannelItemQuery { UserId = _somebody, StartIndex = -1 },
                CancellationToken.None)).ConfigureAwait(true);
    }

    /// <summary>
    /// One request for a level is one call to the surface, and no call is made until it is asked for.
    /// </summary>
    /// <remarks>
    /// The counting #49 exists for. A conversion that read the level twice
    /// would answer identically and cost a source call per browse once the
    /// surface is fetching rather than answering from a fixture.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task OneRequestForALevelIsOneCallToTheSurface()
    {
        var log = new CallLog();
        var surface = new SurfaceThatAnswersFromWhatATestGaveIt(log);
        surface.Put(
            SurfaceAddress.Root,
            new SurfaceListing(new[] { SurfaceEntry.Shelf(SurfaceAddress.Of("shelf:one"), "One") }, 1));

        var adapter = new DiscoverSurfaceAdapter(surface);

        Assert.Empty(log.Calls);

        await adapter.GetChannelItems(
            new InternalChannelItemQuery { UserId = _somebody },
            CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(_oneListCall, log.Calls);
    }
}
