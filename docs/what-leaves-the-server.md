# What leaves the server, and to whom

An operator who runs a server so that nothing leaves their network deserves to
know what this plugin sends out, where it goes, and what a third party can work
out from it. This page is that disclosure. It is written from this tree rather
than from a running server, and every claim on it carries the command behind it.

Read it against the commit it ships with. Where a sentence here and the tree
disagree, the tree is right and the sentence is a defect.

## Today, an installed plugin sends nothing anywhere

This is the first thing to say and the easiest to read too widely. The plugin
that installs today can speak to the source, and nothing in it does.

The type that speaks is `TmdbSourceAdapter`. No other file in the plugin names
it:

    git grep -n 'TmdbSourceAdapter' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ':!Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs' ; echo "exit=$?"
    exit=1

so nothing constructs one, and the container the server builds from this plugin
holds four registrations, none of which is a source:

    git show origin/master:Jellyfin.Plugin.Template/PluginServiceRegistrator.cs | grep -n 'AddSingleton'
    38:        serviceCollection.AddSingleton<IClock, SystemClock>();
    43:        serviceCollection.AddSingleton<IRandomSource, SystemRandomSource>();
    50:        serviceCollection.AddSingleton<IDiscoverSurface, DiscoverSurface>();
    63:        serviceCollection.AddSingleton<IChannel, DiscoverSurfaceAdapter>();

Browsing does not reach one either. Every level of the surface answers empty,
the top level included:

    git grep -n 'return Task.FromResult(SurfaceListing.Empty);' origin/master -- Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs:156:        return Task.FromResult(SurfaceListing.Empty);

So an operator installing this build gets no outbound traffic at all, from an
install, from a browse, or from a schedule. The rest of this page describes what
the adapter sends when something finally asks it to, because that shape is fixed
in the tree now and an operator deciding whether to install is deciding about it
rather than about the silence above.

## Two hosts, and no third

Both are declared on the source's own terms page and both are literals in the
adapter:

    git grep -n 'https://' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:66:    private static readonly Uri _baseAddress = new("https://api.themoviedb.org/3/", UriKind.Absolute);
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:68:    private static readonly Uri _artworkBase = new("https://image.tmdb.org/t/p/w500/", UriKind.Absolute);

The first is where the server asks its questions. The second is where artwork
sits, and the server is not what fetches it, which is the section below.

Nothing else in the plugin can open a connection. The one outbound client in the
project is inside the adapter and there is no second one:

    git grep -nE 'HttpClient|IHttpClientFactory|HttpRequestMessage|Socket|Dns\.' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ':!Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs' ; echo "exit=$?"
    exit=1

That absence is held by a rule rather than by anybody remembering it:

    git grep -n '^Id:\|^Subject:\|^Except:' origin/master -- tools/invariants/rules/no-network-outside-source-adapter.rule
    origin/master:tools/invariants/rules/no-network-outside-source-adapter.rule:1:Id: no-network-outside-source-adapter
    origin/master:tools/invariants/rules/no-network-outside-source-adapter.rule:3:Subject: *.cs
    origin/master:tools/invariants/rules/no-network-outside-source-adapter.rule:4:Except: :!*SourceAdapter.cs

## What one request to the source carries

A request is a `GET`, and the whole of what it says is a path, one query
parameter and three headers.

The path is one of six literals chosen by a switch, and no value a caller
supplied reaches it as text:

    git grep -n '"trending" =>\|"popular" =>\|"top-rated" =>' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:477:            "trending" => series ? "trending/tv/week" : "trending/movie/week",
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:478:            "popular" => series ? "tv/popular" : "movie/popular",
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:479:            "top-rated" => series ? "tv/top_rated" : "movie/top_rated",

The query is a page number and nothing else:

    git grep -n 'Query = FormattableString.Invariant' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:490:            Query = FormattableString.Invariant($"page={page}")

The headers are three:

    git grep -n 'TryAddWithoutValidation' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:561:        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:562:        request.Headers.TryAddWithoutValidation("Accept", "application/json");
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:563:        request.Headers.TryAddWithoutValidation("User-Agent", Identity());

The first is a credential, and whose it is and where it is stored is
[#77](https://github.com/Flowfin/jellyfin-plugin-discover/issues/77). The third
names this plugin and its version and nothing about the server or the operator,
which the source's terms require and which is derived rather than typed:

    git grep -n -A 5 'private static string Identity()' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:584:    private static string Identity()
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs-585-    {
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs-586-        var assembly = typeof(TmdbSourceAdapter).Assembly.GetName();
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs-587-
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs-588-        return FormattableString.Invariant($"{assembly.Name}/{assembly.Version}");
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs-589-    }

## What a request does not carry

No user. Nothing about a person can reach a request, because the question the
adapter is handed has four fields and none of them is one:

    git show origin/master:Jellyfin.Plugin.Template/Sources/SourceQuery.cs | sed -n '44,48p'
    public readonly record struct SourceQuery(
        string Name,
        DiscoverTitleKind Kind,
        int? StartIndex,
        int? Limit)

No library. The adapter asks for a shelf of titles and sends nothing about what
the server holds, what anybody watched, or how many accounts exist. It cannot:
the four fields above are its whole vocabulary.

No server identity beyond what a connection discloses on its own. The plugin
sends no server name, no installation identifier and no version of the server.

## What the source can work out anyway

An absence of fields in a request is not an absence of information, and this is
the part of the disclosure a reader is owed rather than reassured about.

The source sees the connection, so it sees the network address the server calls
from and the time of every call. Over a schedule that is a pattern: which shelves
an operator has enabled, in which language and region once
[#81](https://github.com/Flowfin/jellyfin-plugin-discover/issues/81) lands, how
often the server refreshes, and when it is switched off. The credential ties all
of that to whoever registered it.

None of that is avoidable while the plugin is used at all. It is the cost of
asking somebody else a question.

## Artwork, and who actually fetches it

The plugin never fetches an image. What it stores is a location at the source's
image host, turned from the path the source gave:

    git grep -n 'return new Uri(_artworkBase' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:440:        return new Uri(_artworkBase, path.AsSpan(1).ToString());

and it hands that location to the server as the item's picture:

    git grep -n 'ImageUrl = title.ArtworkLocation' origin/master -- Jellyfin.Plugin.Template/Surface/DiscoverSurfaceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurfaceAdapter.cs:277:            ImageUrl = title.ArtworkLocation?.AbsoluteUri

Whether the image is then fetched by the server or by each client, and therefore
whether a user's own device contacts `image.tmdb.org` directly, is not
established here. It is read from this tree rather than watched happening, no
adapter is wired to anything that would produce a location, and
[#62](https://github.com/Flowfin/jellyfin-plugin-discover/issues/62) is where
that observation is owed. An operator reading this page today should assume the
question is open rather than that the answer is the comfortable one.

## What is held about a person

Nothing. The plugin persists no record of any kind, because the one thing in it
that writes to disk is reached by nothing:

    git grep -n 'new CatalogueDocumentStore(' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ; echo "exit=$?"
    exit=1

A user identifier reaches the plugin when somebody browses, and it is answered
with rather than kept. Every site it appears at is a per-user answer being asked
for:

    git grep -n 'UserId\|userId' origin/master -- 'Jellyfin.Plugin.Template/*.cs'
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs:133:    public bool IsAvailableTo(Guid userId) => true;
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurfaceAdapter.cs:167:    public bool IsEnabledFor(string userId) =>
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurfaceAdapter.cs:168:        Guid.TryParse(userId, out var parsed) && _surface.IsAvailableTo(parsed);
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurfaceAdapter.cs:177:            query.UserId,
    origin/master:Jellyfin.Plugin.Template/Surface/IDiscoverSurface.cs:52:    /// <param name="userId">Who is asking.</param>
    origin/master:Jellyfin.Plugin.Template/Surface/IDiscoverSurface.cs:60:    bool IsAvailableTo(Guid userId);
    origin/master:Jellyfin.Plugin.Template/Surface/SurfaceLevelRequest.cs:19:/// <param name="UserId">
    origin/master:Jellyfin.Plugin.Template/Surface/SurfaceLevelRequest.cs:33:    Guid UserId,

The register this list is kept in is
[#70](https://github.com/Flowfin/jellyfin-plugin-discover/issues/70), and it is
the input to this page rather than something restated here. Two things in the
plan take it off zero when they land: recording what a user asked for, which is
[#97](https://github.com/Flowfin/jellyfin-plugin-discover/issues/97), and
personalised shelves, which is question 7 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) and is not
answered.

Sources that would need a user's own authorisation, which are the largest thing
that could ever leave this server about a named person, are deferred, and the
argument is on its own page rather than summarised here:
[`per-user-sources.md`](per-user-sources.md).

## The configuration page

An administrator opening the plugin's page sends nothing outside the server, and
a test refuses the change rather than anybody remembering it:

    git grep -n 'ThePageRequestsNothingFromAHostOutsideTheServer' origin/master -- Jellyfin.Plugin.Template.Tests/ConfigurationPageTests.cs
    origin/master:Jellyfin.Plugin.Template.Tests/ConfigurationPageTests.cs:174:    public void ThePageRequestsNothingFromAHostOutsideTheServer()

## What a user can turn off for themselves

Nothing, and the heading is here rather than left out so the answer is readable.
Everything this plugin can be told is one server-wide record with one field in
it:

    git grep -n 'public .* { get; set; }' origin/master -- Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs
    origin/master:Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:36:    public int SchemaVersion { get; set; }

The page an administrator opens is not a page a user opens, and the neighbouring
control on the server, which decides who sees a surface at all, is set by an
administrator too. Whether a user ever gets a switch of their own is
[#57](https://github.com/Flowfin/jellyfin-plugin-discover/issues/57) and
question 7 on [#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2).

## What an operator cannot avoid

Today, nothing, because nothing is sent.

Once a source is wired up and a key is entered, three things come with using the
plugin at all and no setting removes them. The source learns the server's network
address and the times it calls. The source learns which shelves are being asked
for. And the credential ties both to the person who registered it. An operator
who cannot accept those should not enter a key, and the plugin with no key
entered asks nothing, which is what
[#104](https://github.com/Flowfin/jellyfin-plugin-discover/issues/104) holds.

## What this page is not

It is read rather than watched. No server was booted, no request was made to the
source, and no client was pointed at an image host. Booting a server is
[#38](https://github.com/Flowfin/jellyfin-plugin-discover/issues/38) and putting
a client in front of it is
[#115](https://github.com/Flowfin/jellyfin-plugin-discover/issues/115), and the
one claim on this page that needs a running client, whether an image fetch
reaches the source's host directly, is marked above as unestablished rather than
guessed at.

No route in this tree reads this page. It goes stale silently, and what catches
that is somebody running the commands on it. That has happened once already:
six of the quotations above were re-derived after the adapter moved under
[#68](https://github.com/Flowfin/jellyfin-plugin-discover/issues/68) and
[#251](https://github.com/Flowfin/jellyfin-plugin-discover/issues/251), and the
sentences they support did not move with them.

One of those six failed in a way the others could not, and it is the form to
avoid here rather than a detail of that repair. Five were commands that find
their subject by content, so a moved subject changes the line number beside an
answer that is still the right answer. The sixth asked for a range of lines by
number, and a range that no longer holds what it was written for prints
different code with nothing to say it has: it had come to print the header block
quoted above it.

The quotations here therefore address their subject by content, with one
exception left standing on purpose: the four fields of a query, under "What a
request does not carry", are still asked for as a range of lines. They agree
with the tree today, and the range is kept because the surrounding form is what
makes those four readable as a whole.

That exception is a claim rather than a measurement, and deliberately so. A
command searching this page for that form matches the paragraph naming it, and
each quotation of the result adds another match, so the number a reader would be
handed counts this warning rather than the page's quotations. Counting them is
a person reading the page.
