using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// The one place this plugin speaks the server's channel vocabulary.
/// </summary>
/// <remarks>
/// Everything above this file is written in the plugin's own words, in
/// <see cref="IDiscoverSurface"/> and the types beside it. This class turns
/// those into the shapes the server's channel interface asks for and turns the
/// server's query back the other way. Nothing else in the tree references a
/// channel type, and `no-channel-type-outside-surface` is what keeps that true
/// as files are added rather than as a thing to remember.
///
/// The seam is not being built for a difference between the two server lines
/// that exists today. Their channel surface differs only in nullable
/// annotations and documentation, so one implementation serves both:
///
///     git diff --stat v10.11.11 v12.0-rc4 -- MediaBrowser.Controller/Channels MediaBrowser.Model/Channels
///      9 files changed, 82 insertions(+), 23 deletions(-)
///
/// It is built because it is where the first difference lands. Where the lines
/// ever do differ, the repair is a second class beside this one, chosen by the
/// line the artefact was built for, which `Directory.Build.props` states, and
/// never by asking the server it happens to be loaded into what it is.
///
/// This class does not register itself, and nothing here decides what the
/// surface is called or what it offers. Registration is #53 and so are the
/// values, which arrive through the <see cref="IDiscoverSurface"/> handed to
/// the constructor.
/// </remarks>
public sealed class DiscoverSurfaceAdapter : IChannel
{
    private readonly IDiscoverSurface _surface;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoverSurfaceAdapter"/> class.
    /// </summary>
    /// <param name="surface">The surface, in this plugin's own vocabulary.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
    public DiscoverSurfaceAdapter(IDiscoverSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        _surface = surface;
    }

    /// <inheritdoc />
    public string Name => _surface.Description.Name;

    /// <inheritdoc />
    public string Description => _surface.Description.Summary;

    /// <inheritdoc />
    public string DataVersion => _surface.Description.CatalogueVersion;

    /// <inheritdoc />
    /// <remarks>
    /// An empty string rather than null where the surface names no page. The
    /// server's own declaration of this member predates its nullable
    /// annotations and a client is handed whatever it is given, so the value
    /// that draws as nothing everywhere is the one with no second behaviour to
    /// find out about.
    /// </remarks>
    public string HomePageUrl =>
        _surface.Description.HomePage?.AbsoluteUri ?? string.Empty;

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => _surface.Description.Audience switch
    {
        SurfaceAudience.General => ChannelParentalRating.GeneralAudience,
        SurfaceAudience.Adult => ChannelParentalRating.Adult,
        _ => throw new InvalidOperationException(
            $"The surface states an audience of {_surface.Description.Audience}, which this adapter has no server band for. A member added to {nameof(SurfaceAudience)} is added here in the same change.")
    };

    /// <inheritdoc />
    /// <remarks>
    /// Five of the server's feature fields are set here and none is left at
    /// whatever the record's constructor happens to produce, because a default
    /// nobody chose reads afterwards exactly like a decision. What each one is
    /// and why is beside it below.
    /// </remarks>
    public InternalChannelFeatures GetChannelFeatures()
    {
        var capabilities = _surface.Capabilities;
        var features = new InternalChannelFeatures
        {
            // The surface's own ceiling on one answer. Null means it states
            // none, and the server then pages however it likes.
            MaxPageSize = capabilities.MaximumPageSize,

            // Nothing is offered for download. This plugin holds no media at
            // all: a discover title is one the server does not have, so there
            // is nothing on disk for the server to hand anybody.
            SupportsContentDownloading = false,

            // No sort toggle and no sort fields, below. The order is the
            // plugin's, per #54, and a stable one is #91. Declaring a field the
            // surface does not honour is a client offering a control that
            // changes nothing.
            SupportsSortOrderToggle = false,

            // No level refreshes itself on the server's timer. When the
            // catalogue is refreshed is this plugin's schedule, which is #87,
            // and the server's own three-hour hold on what a surface answered
            // is #61. A third cadence here would be one nobody could reason
            // about.
            AutoRefreshLevels = null,

            // No daily download limit, because nothing is downloadable. Stated
            // rather than left, so it is visibly the same decision as the
            // field above rather than an oversight.
            DailyDownloadLimit = null
        };

        features.DefaultSortFields.Clear();

        foreach (var kind in capabilities.TitleKinds)
        {
            switch (kind)
            {
                case DiscoverTitleKind.Movie:
                    // A film is media the server materialises as a Movie, so it
                    // is video content and is declared as both.
                    features.ContentTypes.Add(ChannelMediaContentType.Movie);
                    features.MediaTypes.Add(ChannelMediaType.Video);
                    break;

                case DiscoverTitleKind.Series:
                    // A series is a folder rather than media: the server
                    // materialises a folder declaring ChannelFolderType.Series
                    // as a Series, and there is no content type for one. So a
                    // surface offering only series declares no content type,
                    // which is the honest answer rather than a borrowed one.
                    break;

                default:
                    throw new InvalidOperationException(
                        $"The surface offers titles of kind {kind}, which this adapter has no server shape for. A member added to {nameof(DiscoverTitleKind)} is added here in the same change.");
            }
        }

        return features;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The server hands the user identifier over as text. A value that is not
    /// one is answered with false rather than with an exception: this is asked
    /// while the server is building a user's list of libraries, and the failure
    /// a throw here produces is a user with no libraries at all rather than a
    /// user without this one.
    /// </remarks>
    public bool IsEnabledFor(string userId) =>
        Guid.TryParse(userId, out var parsed) && _surface.IsAvailableTo(parsed);

    /// <inheritdoc />
    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var request = new SurfaceLevelRequest(
            string.IsNullOrEmpty(query.FolderId) ? SurfaceAddress.Root : SurfaceAddress.Of(query.FolderId),
            query.UserId,
            query.StartIndex,
            query.Limit).Validated();

        var listing = await _surface.ListAsync(request, cancellationToken).ConfigureAwait(false);

        var items = new List<ChannelItemInfo>(listing.Entries.Count);

        foreach (var entry in listing.Entries)
        {
            items.Add(ItemFor(entry));
        }

        return new ChannelItemResult
        {
            Items = items,
            TotalRecordCount = listing.TotalCount
        };
    }

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        var image = KindFor(type) is { } kind ? _surface.Image(kind) : null;

        if (image is null)
        {
            return Task.FromResult(new DynamicImageResponse { HasImage = false });
        }

        return Task.FromResult(new DynamicImageResponse
        {
            HasImage = true,
            Format = image.Format switch
            {
                SurfaceImageFormat.Png => ImageFormat.Png,
                SurfaceImageFormat.Jpeg => ImageFormat.Jpg,
                _ => throw new InvalidOperationException(
                    $"The surface supplied an image in format {image.Format}, which this adapter has no server format for. A member added to {nameof(SurfaceImageFormat)} is added here in the same change.")
            },
            Stream = new MemoryStream(image.Bytes.ToArray(), writable: false)
        });
    }

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages()
    {
        foreach (var kind in _surface.ImageKinds)
        {
            yield return TypeFor(kind);
        }
    }

    private static ImageType TypeFor(SurfaceImageKind kind) => kind switch
    {
        SurfaceImageKind.Primary => ImageType.Primary,
        SurfaceImageKind.Thumb => ImageType.Thumb,
        SurfaceImageKind.Backdrop => ImageType.Backdrop,
        _ => throw new InvalidOperationException(
            $"The surface offers an image of kind {kind}, which this adapter has no server type for. A member added to {nameof(SurfaceImageKind)} is added here in the same change.")
    };

    private static SurfaceImageKind? KindFor(ImageType type) => type switch
    {
        ImageType.Primary => SurfaceImageKind.Primary,
        ImageType.Thumb => SurfaceImageKind.Thumb,
        ImageType.Backdrop => SurfaceImageKind.Backdrop,

        // Every other kind of picture the server knows about is one this
        // surface has no member for, so it is answered as absent rather than
        // refused. The server asks for what it likes and the honest answer to
        // most of it is that there is none.
        _ => null
    };

    private static ChannelItemInfo ItemFor(SurfaceEntry entry) => entry.Kind switch
    {
        SurfaceEntryKind.Shelf => new ChannelItemInfo
        {
            Id = entry.Address.Value,
            Name = entry.Name,
            Type = ChannelItemType.Folder,
            FolderType = ChannelFolderType.Container
        },

        SurfaceEntryKind.Title => TitleItemFor(entry.Address, entry.Title!),

        _ => throw new InvalidOperationException(
            $"A level held an entry of kind {entry.Kind}, which this adapter has no server shape for. A member added to {nameof(SurfaceEntryKind)} is added here in the same change.")
    };

    private static ChannelItemInfo TitleItemFor(SurfaceAddress address, DiscoverTitle title)
    {
        var item = new ChannelItemInfo
        {
            Id = address.Value,
            Name = title.Name,
            OriginalTitle = title.OriginalName,
            Overview = title.Summary,
            ProductionYear = title.ReleaseYear,
            ImageUrl = title.ArtworkLocation?.AbsoluteUri
        };

        switch (title.Kind)
        {
            case DiscoverTitleKind.Movie:
                item.Type = ChannelItemType.Media;
                item.MediaType = ChannelMediaType.Video;
                item.ContentType = ChannelMediaContentType.Movie;
                break;

            case DiscoverTitleKind.Series:
                // A folder rather than media, because that is the only shape
                // the server materialises as a Series. It holds nothing: no
                // level is deeper than a shelf at 1.0, per #54, so a request
                // for this address is answered empty.
                item.Type = ChannelItemType.Folder;
                item.FolderType = ChannelFolderType.Series;
                break;

            default:
                throw new InvalidOperationException(
                    $"A title carried kind {title.Kind}, which this adapter has no server shape for. A member added to {nameof(DiscoverTitleKind)} is added here in the same change.");
        }

        foreach (var identifier in title.Identity.Identifiers)
        {
            item.ProviderIds[KeyFor(identifier.Source)] = identifier.Value;
        }

        return item;
    }

    private static string KeyFor(MetadataSource source) => source switch
    {
        // The server's own spelling for each body, taken from its enum rather
        // than written as a literal, so a rename on the server side is a build
        // failure here instead of identifiers that stop matching the library.
        MetadataSource.Imdb => MetadataProvider.Imdb.ToString(),
        MetadataSource.Tmdb => MetadataProvider.Tmdb.ToString(),
        MetadataSource.Tvdb => MetadataProvider.Tvdb.ToString(),
        _ => throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "A title carried an identifier from {0}, which this adapter has no server key for. A member added to {1} is added here in the same change.",
                source,
                nameof(MetadataSource)))
    };
}
