using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Surface;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The values the surface states about itself, and what it answers while it holds nothing.
/// </summary>
/// <remarks>
/// No server type is named here and none is needed. The surface is this
/// plugin's own interface, which is what #49 asks for and what #52 built the
/// adapter to make possible, so every assertion below is about the plugin
/// rather than about a conversion.
///
/// What is not asserted here is what a client draws. That needs a client, a
/// screen and a person looking at it, which is refused under #42 and replaced
/// by the matrix in #115.
/// </remarks>
public class DiscoverSurfaceTests
{
    private static readonly Guid _somebody = new Guid("6a1b8e70-2f43-4c19-9a7e-5c2d0f8b3341");

    /// <summary>
    /// The name is what it is recorded as here, and it is written out rather
    /// than read from the surface.
    /// </summary>
    /// <remarks>
    /// A second, independent record of the value, for the same reason the
    /// plugin identifier has one under #107: a test that derives the expected
    /// value from the place the code reads it asserts that a value equals
    /// itself. This one is worth more than most, because the server hashes an
    /// item's identity out of this name together with the external identifier,
    /// per #60. Moving it orphans every item every user marked anything on, and
    /// the move is otherwise a one-word edit that looks like tidying.
    /// </remarks>
    [Fact]
    public void TheNameIsTheOneRecordedHere()
    {
        Assert.Equal("Discover", new DiscoverSurface().Description.Name);
    }

    /// <summary>
    /// No two of the values the surface states about itself are the same string.
    /// </summary>
    /// <remarks>
    /// The shape #53 refuses is one value repeated across every field, which
    /// draws as a library called after the plugin, described by its own name,
    /// under a picture of the plugin. Distinctness is not quality, and nothing
    /// here claims the summary is a good one; it does refuse the failure by
    /// name.
    /// </remarks>
    [Fact]
    public void TheSurfaceStatesThreeDifferentThingsAboutItself()
    {
        var description = new DiscoverSurface().Description;

        var stated = new[] { description.Name, description.Summary, description.CatalogueVersion };

        Assert.Equal(stated.Length, stated.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The surface states no ceiling of its own on one answer.
    /// </summary>
    /// <remarks>
    /// A regression guard rather than a restatement. The server derives whether
    /// a client may filter the surface at all from whether this is set, so
    /// setting it is a decision about filtering as much as about paging:
    ///
    ///     git grep -n 'CanFilter = ' v10.11.11 v12.0-rc4 -- src/Jellyfin.LiveTv/Channels/ChannelManager.cs
    ///     v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:574:                CanFilter = !features.MaxPageSize.HasValue,
    ///     v12.0-rc4:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:572:                CanFilter = !features.MaxPageSize.HasValue,
    ///
    /// Whoever sets a number here reddens this and reads the reason, which is
    /// the whole of what this assertion is for.
    /// </remarks>
    [Fact]
    public void TheSurfaceStatesNoCeilingOfItsOwn()
    {
        Assert.Null(new DiscoverSurface().Capabilities.MaximumPageSize);
    }

    /// <summary>
    /// The surface offers both kinds of title the catalogue record carries.
    /// </summary>
    [Fact]
    public void TheSurfaceOffersBothKindsOfTitle()
    {
        var kinds = new DiscoverSurface().Capabilities.TitleKinds;

        Assert.Contains(DiscoverTitleKind.Movie, kinds);
        Assert.Contains(DiscoverTitleKind.Series, kinds);
        Assert.DoesNotContain(DiscoverTitleKind.None, kinds);
    }

    /// <summary>
    /// The surface has no picture of its own, and it says so the same way twice.
    /// </summary>
    /// <remarks>
    /// Two answers rather than one, because a client can reach the second
    /// without the first: the server asks for the kinds it was offered and it
    /// also asks for kinds nobody offered. A surface that listed no kind and
    /// then produced a picture for one would be answering two different things
    /// about itself, and the second answer is the one a client draws.
    /// </remarks>
    [Fact]
    public void TheSurfaceHasNoPictureOfItsOwn()
    {
        var surface = new DiscoverSurface();

        Assert.Empty(surface.ImageKinds);

        foreach (var kind in Enum.GetValues<SurfaceImageKind>())
        {
            Assert.Null(surface.Image(kind));
        }
    }

    /// <summary>
    /// The top level is recognised and holds nothing, which is a total of zero rather than no total.
    /// </summary>
    /// <remarks>
    /// This is the state #54 changes, and it is asserted rather than left
    /// implicit so that landing the shelves reddens something. Zero is the
    /// assertion rather than "no entries" because the entries are what the two
    /// answers of #54 have in common and the total is what separates them.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task TheTopLevelIsRecognisedAndHoldsNothingUntilTheShelvesExist()
    {
        var listing = await new DiscoverSurface()
            .ListAsync(new SurfaceLevelRequest(SurfaceAddress.Root, _somebody, null, null), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Empty(listing.Entries);
        Assert.Equal(0, listing.TotalCount);
    }

    /// <summary>
    /// An address this surface does not recognise says so, and says it without throwing.
    /// </summary>
    /// <remarks>
    /// An address from an older version whose shelf no longer exists is an
    /// ordinary thing for a client to send, so it is answered rather than
    /// refused, which is #54's third condition. What #54's answer adds is that
    /// the answer is distinguishable from a shelf standing empty: no entries in
    /// both, and no total here against a total of zero above. Without the
    /// second assertion this test passes on whatever the recognised case
    /// happens to return, which is what it did while both cases were one value.
    /// </remarks>
    /// <param name="folder">The address asked for.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData("a-shelf-nobody-has")]
    [InlineData("shelf:that-was-removed")]
    public async Task AnAddressThisSurfaceDoesNotRecogniseIsAnsweredWithNoTotalRatherThanZero(string folder)
    {
        var listing = await new DiscoverSurface()
            .ListAsync(
                new SurfaceLevelRequest(SurfaceAddress.Of(folder), _somebody, null, null),
                CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Empty(listing.Entries);
        Assert.Null(listing.TotalCount);
    }

    /// <summary>
    /// A request that could not be answered is refused rather than answered empty.
    /// </summary>
    /// <remarks>
    /// The paging numbers arrive from outside this plugin, and an empty level
    /// is already what this surface answers for several legitimate reasons. A
    /// fault answered the same way would be a fault nobody ever sees, so it is
    /// refused here as well as at the adapter, which is the other side of the
    /// same boundary and cannot vouch for a caller inside the process.
    /// </remarks>
    /// <param name="startIndex">Where the request starts.</param>
    /// <param name="limit">How much it asks for.</param>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, -1)]
    public async Task ARequestThatCouldNotBeAnsweredIsRefused(int? startIndex, int? limit)
    {
        var request = new SurfaceLevelRequest(SurfaceAddress.Root, _somebody, startIndex, limit);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => new DiscoverSurface().ListAsync(request, CancellationToken.None)).ConfigureAwait(true);
    }

    /// <summary>
    /// The surface exists for every user today.
    /// </summary>
    /// <remarks>
    /// Recorded rather than endorsed. #57 decides this per user and against a
    /// configuration that does not exist, and what makes the permissive answer
    /// safe meanwhile is that every level is empty. This assertion is what
    /// makes #57's change visible rather than silent.
    /// </remarks>
    [Fact]
    public void TheSurfaceExistsForEveryUserToday()
    {
        Assert.True(new DiscoverSurface().IsAvailableTo(_somebody));
    }
}
