using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Surface;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The discover surface, answering from what a test put in it and recording every call it received.
/// </summary>
/// <remarks>
/// The fake #49 asks for, and the reason #52 exists. Every test about the
/// adapter goes through this rather than through anything the server supplies,
/// so those tests need no server, no database and no library, and they can say
/// what the adapter asked for rather than only what came back.
///
/// Hand written rather than produced by a mocking framework. The interface has
/// six members and the fake is shorter than the configuration a framework would
/// need, and what a test reads here is C# rather than a second vocabulary.
///
/// It answers levels out of a dictionary keyed by address, and an address it
/// was given nothing for is answered with the empty listing. That is the same
/// answer a real surface gives a level it does not recognise, per #54, so a
/// test for that case sets nothing up rather than setting up a special case.
/// </remarks>
internal sealed class SurfaceThatAnswersFromWhatATestGaveIt : IDiscoverSurface
{
    private readonly CallLog _log;
    private readonly Dictionary<string, SurfaceListing> _levels = new Dictionary<string, SurfaceListing>(StringComparer.Ordinal);
    private readonly Dictionary<SurfaceImageKind, SurfaceImage> _images = new Dictionary<SurfaceImageKind, SurfaceImage>();
    private readonly HashSet<Guid> _allowed = new HashSet<Guid>();

    private SurfaceListing? _root;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurfaceThatAnswersFromWhatATestGaveIt"/> class.
    /// </summary>
    /// <param name="log">The log this fake records into, shared with the other fakes in the run.</param>
    public SurfaceThatAnswersFromWhatATestGaveIt(CallLog log)
    {
        _log = log;
    }

    /// <summary>
    /// Gets or sets what the surface calls itself.
    /// </summary>
    public SurfaceDescription Description { get; set; } = new SurfaceDescription
    {
        Name = "Discover",
        Summary = "Titles this server does not have.",
        CatalogueVersion = "1",
        Audience = SurfaceAudience.General
    };

    /// <summary>
    /// Gets or sets what the surface tells the server it can do.
    /// </summary>
    public SurfaceCapabilities Capabilities { get; set; } = new SurfaceCapabilities
    {
        TitleKinds = new[] { DiscoverTitleKind.Movie }
    };

    /// <summary>
    /// Gets the kinds of picture this surface has, which are the ones a test put in it.
    /// </summary>
    public IReadOnlyList<SurfaceImageKind> ImageKinds
    {
        get
        {
            _log.Record("surface.ImageKinds");
            return new List<SurfaceImageKind>(_images.Keys);
        }
    }

    /// <summary>
    /// Gets the last request this fake was asked to answer, or null when it has not been asked.
    /// </summary>
    public SurfaceLevelRequest? LastRequest { get; private set; }

    /// <summary>
    /// Lets one user see the surface.
    /// </summary>
    /// <param name="userId">Who.</param>
    public void Allow(Guid userId) => _allowed.Add(userId);

    /// <summary>
    /// Puts a level behind one address.
    /// </summary>
    /// <param name="address">Which level.</param>
    /// <param name="listing">What it holds.</param>
    public void Put(SurfaceAddress address, SurfaceListing listing)
    {
        if (address.IsRoot)
        {
            _root = listing;
            return;
        }

        _levels[address.Value] = listing;
    }

    /// <summary>
    /// Puts a picture behind one kind.
    /// </summary>
    /// <param name="kind">Which kind.</param>
    /// <param name="image">The picture.</param>
    public void Put(SurfaceImageKind kind, SurfaceImage image) => _images[kind] = image;

    /// <inheritdoc />
    public bool IsAvailableTo(Guid userId)
    {
        _log.Record("surface.IsAvailableTo");
        return _allowed.Contains(userId);
    }

    /// <inheritdoc />
    public SurfaceImage? Image(SurfaceImageKind kind)
    {
        _log.Record("surface.Image");
        return _images.TryGetValue(kind, out var image) ? image : null;
    }

    /// <inheritdoc />
    public Task<SurfaceListing> ListAsync(SurfaceLevelRequest request, CancellationToken cancellationToken)
    {
        _log.Record("surface.ListAsync");
        LastRequest = request;

        if (request.Parent.IsRoot)
        {
            return Task.FromResult(_root ?? SurfaceListing.Empty);
        }

        return Task.FromResult(
            _levels.TryGetValue(request.Parent.Value, out var listing) ? listing : SurfaceListing.Empty);
    }
}
