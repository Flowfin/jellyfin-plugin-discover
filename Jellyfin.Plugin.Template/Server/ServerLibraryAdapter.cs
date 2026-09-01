using System;
using System.Collections.Generic;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Template.Catalogue;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Template.Server;

/// <summary>
/// The one place this plugin speaks the server's library vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// The second adapter beside <see cref="Surface.DiscoverSurfaceAdapter"/> and
/// built for the same reason: everything above <see cref="IServerLibrary"/> is
/// written in this plugin's own words and is testable with no server behind it,
/// and the server's item vocabulary stops here. #89's fourth condition asks
/// that the comparison go through a seam rather than reach into the server's
/// library types, and this file is the far side of that seam rather than an
/// exception to it.
/// </para>
/// <para>
/// WHAT CROSSES INTO THIS CLASS IS AN IDENTITY AND A KIND, AND THAT IS WHAT
/// KEEPS THE LOOKUP OFF TITLE TEXT. <see cref="IServerLibrary"/> carries no
/// name, so the query built below has no name to put in it: every question
/// asked of the server is keyed on the identifiers a source supplied. #89's
/// second condition is held by that signature rather than by the care taken
/// here.
/// </para>
/// <para>
/// A PART IS A FILM FOR A MOVIE AND AN EPISODE FOR A SERIES, and both are
/// counted with the server's virtual items excluded. A server showing missing
/// episodes carries a row per episode it does not have, and an operator running
/// with that on would otherwise have every series they ever added counted as
/// owned. The rule the count serves is that a title is owned when the server
/// holds at least one part of it, and a row nobody can play is not a part.
/// </para>
/// <para>
/// A SERIES COSTS TWO QUESTIONS RATHER THAN ONE. An episode carries no
/// provider identifier for the series it belongs to, so the series is found by
/// identifier first and its episodes are then counted underneath it. A series
/// the server does not carry at all is answered without asking the second
/// question, which is the ordinary case on a discover shelf.
/// </para>
/// <para>
/// The identifiers are handed over as the set rather than one at a time, and
/// the server matches an item carrying ANY of them. Two identifiers that
/// disagree about which title they name are the source contradicting itself,
/// which <see cref="DiscoverTitleIdentity"/> refuses on the way in, and a
/// server whose own item carries one of them and not the other is the server's
/// record rather than something this plugin can arbitrate.
/// </para>
/// </remarks>
public sealed class ServerLibraryAdapter : IServerLibrary
{
    private readonly ILibraryManager _library;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerLibraryAdapter"/> class.
    /// </summary>
    /// <param name="library">The server's own library.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="library"/> is null.</exception>
    /// <remarks>
    /// Nothing is asked of it here. A registrator runs while the server is
    /// still building its container, so an adapter that read anything off the
    /// library at construction would be reading a half-built server, which is
    /// the property <c>RegisteringReachesNothingOnTheHost</c> already holds one
    /// seam over.
    /// </remarks>
    public ServerLibraryAdapter(ILibraryManager library)
    {
        ArgumentNullException.ThrowIfNull(library);

        _library = library;
    }

    /// <inheritdoc />
    public int PartsHeld(DiscoverTitleIdentity identity, DiscoverTitleKind kind)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return kind switch
        {
            DiscoverTitleKind.Movie => _library.GetCount(Asking(BaseItemKind.Movie, identity)),
            DiscoverTitleKind.Series => EpisodesUnderTheSeries(identity),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                $"A discover title of kind {kind} has no part this adapter knows how to count. A member added to {nameof(DiscoverTitleKind)} is added here in the same change, because a kind that fell through to a default would report every title of it as one the server does not have.")
        };
    }

    /// <summary>
    /// The query that asks this server for one title, by its identifiers and nothing else.
    /// </summary>
    /// <param name="kind">The server's own item kind.</param>
    /// <param name="identity">The identifiers the title is looked up by.</param>
    /// <returns>The query.</returns>
    /// <remarks>
    /// <c>Recursive</c> is set because a library query that is not recursive
    /// answers about one level of one folder, and a discover title is somewhere
    /// in whatever tree the operator arranged. <c>IsVirtualItem</c> is set
    /// false for the reason at the top of this file: a row the server carries
    /// for something it does not have is not a part.
    /// </remarks>
    private static InternalItemsQuery Asking(BaseItemKind kind, DiscoverTitleIdentity identity)
    {
        var wanted = new Dictionary<string, string>(identity.Identifiers.Count, StringComparer.Ordinal);

        foreach (var identifier in identity.Identifiers)
        {
            wanted[Named(identifier.Source)] = identifier.Value;
        }

        return new InternalItemsQuery
        {
            IncludeItemTypes = new[] { kind },
            HasAnyProviderId = wanted,
            IsVirtualItem = false,
            Recursive = true
        };
    }

    /// <summary>
    /// What the server calls the body that issued an identifier.
    /// </summary>
    /// <param name="source">The source, in this plugin's vocabulary.</param>
    /// <returns>The key the server stores that body's identifiers under.</returns>
    /// <remarks>
    /// Mapped rather than spelled, because the two vocabularies are separate
    /// registers that happen to agree today. This plugin's set is closed by
    /// <see cref="MetadataSource"/> and is about which bodies a response may
    /// name; the server's is about which providers it was built with. A member
    /// added to one is not a member of the other, and a cast between them would
    /// be an agreement nobody stated.
    /// </remarks>
    private static string Named(MetadataSource source) => source switch
    {
        MetadataSource.Imdb => MetadataProvider.Imdb.ToString(),
        MetadataSource.Tmdb => MetadataProvider.Tmdb.ToString(),
        MetadataSource.Tvdb => MetadataProvider.Tvdb.ToString(),
        _ => throw new ArgumentOutOfRangeException(
            nameof(source),
            source,
            $"This server has no provider name for {source}. A member added to {nameof(MetadataSource)} is given one here in the same change, because a source that fell through to a default would be asked about under somebody else's identifiers.")
    };

    /// <summary>
    /// How many episodes this server holds of one series.
    /// </summary>
    /// <param name="identity">The series' identifiers.</param>
    /// <returns>The episode count, and zero where the server carries no such series.</returns>
    /// <remarks>
    /// The early return is the ordinary case rather than a shortcut: a discover
    /// shelf is mostly titles the server does not have, so the second question
    /// is the exception. It also matters for the answer and not only for the
    /// price - an ancestor query with no ancestors in it is a query with no
    /// bound, and the count it comes back with would be every episode on the
    /// server.
    /// </remarks>
    private int EpisodesUnderTheSeries(DiscoverTitleIdentity identity)
    {
        var series = _library.GetItemIds(Asking(BaseItemKind.Series, identity));

        if (series.Count == 0)
        {
            return 0;
        }

        var under = new Guid[series.Count];

        for (var next = 0; next < series.Count; next++)
        {
            under[next] = series[next];
        }

        return _library.GetCount(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            AncestorIds = under,
            IsVirtualItem = false,
            Recursive = true
        });
    }
}
