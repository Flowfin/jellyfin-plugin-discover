# Where an item can come back out of the server

A discover title is a channel item, which is decided in
[0001](decisions/0001-a-discover-page-is-a-server-channel.md). A channel item is
a row in the same item table as the operator's media, so anything that queries
items can find one. This page is the list of places that query items, how the
list was produced, and what each of them does with a channel item today.

It is a reading of the server, not of this plugin. None of the answers below is
implemented. The surface is registered with the server, and every level it
answers is empty, so no discover title has reached any of the places listed
here:

    git grep -n 'AddSingleton<IChannel' -- Jellyfin.Plugin.Template/PluginServiceRegistrator.cs
    Jellyfin.Plugin.Template/PluginServiceRegistrator.cs:74:        serviceCollection.AddSingleton<IChannel, DiscoverSurfaceAdapter>();

    git grep -n 'IsRoot ? SurfaceListing.EmptyLevel' -- Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs
    Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs:166:            asked.Parent.IsRoot ? SurfaceListing.EmptyLevel : SurfaceListing.NoSuchLevel);

What this page is for is that the reading is expensive to reconstruct later and
is the input the implementation needs, and a list that cannot be re-derived goes
stale in silence.

## How this was produced

Two server lines, both read at a tag rather than at a branch:

    git -C <a jellyfin checkout> rev-parse v10.11.11 v12.0-rc4
    1fbd8739292cce610231be93daf43368733edf63
    b3a06113029585594fe7a44becbfae7d2bdd9974

The candidate list is every API controller that can put an item on the wire,
which is every one that names the item transfer type:

    git grep -l 'BaseItemDto' v10.11.11 -- Jellyfin.Api/Controllers | sed 's#.*/##'

Twenty-one files, and the same twenty-one at `v12.0-rc4`. The controller set
itself is identical across the two lines:

    git ls-tree -r --name-only v10.11.11 -- Jellyfin.Api/Controllers | sed 's#.*/##' | sort > /tmp/a
    git ls-tree -r --name-only v12.0-rc4  -- Jellyfin.Api/Controllers | sed 's#.*/##' | sort > /tmp/b
    diff /tmp/a /tmp/b ; echo "exit=$?"
    exit=0

Re-run those three against a later tag and the list below is either confirmed or
shown to have moved. That is the point of writing the commands down rather than
the answers alone.

## The one lever, and who pulls it

A channel item is not a separate kind of row. `BaseItem.SourceType` reports
`Channel` when the item carries a channel identifier and `Library` otherwise:

    git grep -n 'return SourceType' v10.11.11 -- MediaBrowser.Controller/Entities/BaseItem.cs
    v10.11.11:MediaBrowser.Controller/Entities/BaseItem.cs:267:                    return SourceType.Channel;
    v10.11.11:MediaBrowser.Controller/Entities/BaseItem.cs:270:                return SourceType.Library;

The query type carries a matching filter, and it is the only way to say "library
items only" in one place:

    git grep -n 'public SourceType\[\] SourceTypes' v10.11.11 v12.0-rc4 -- MediaBrowser.Controller/Entities/InternalItemsQuery.cs
    v10.11.11:MediaBrowser.Controller/Entities/InternalItemsQuery.cs:258:        public SourceType[] SourceTypes { get; set; }
    v12.0-rc4:MediaBrowser.Controller/Entities/InternalItemsQuery.cs:377:        public SourceType[] SourceTypes { get; set; }

Who sets it is the part that matters here. On both lines it is set only by the
server's own background work, and by nothing a client can call:

    git grep -rl 'SourceTypes = ' v10.11.11 -- '*.cs' | sed 's/^v10.11.11://'
    Emby.Server.Implementations/ScheduledTasks/Tasks/ChapterImagesTask.cs
    Emby.Server.Implementations/ScheduledTasks/Tasks/MediaSegmentExtractionTask.cs
    Jellyfin.Server/Migrations/Routines/MoveTrickplayFiles.cs
    MediaBrowser.Controller/Entities/InternalItemsQuery.cs
    MediaBrowser.Providers/Lyric/LyricScheduledTask.cs
    MediaBrowser.Providers/MediaInfo/SubtitleScheduledTask.cs
    MediaBrowser.Providers/Trickplay/TrickplayImagesTask.cs
    MediaBrowser.Providers/Trickplay/TrickplayMoveImagesTask.cs
    src/Jellyfin.MediaEncoding.Hls/ScheduledTasks/KeyframeExtractionScheduledTask.cs

Not one of the twenty-one controllers is in that list, on either line:

    for f in <the twenty-one>; do git show v10.11.11:Jellyfin.Api/Controllers/$f | grep -c 'SourceTypes'; done
    # every answer: 0

So the server's default is inclusion. Trickplay, chapter images, subtitles,
lyrics and keyframe extraction all opt out of channel items deliberately; every
browsing and searching endpoint does not opt out, because it never says
anything about source type at all. **A discover title is visible to these
surfaces unless this plugin does something about it, and the something cannot
be a flag on the item.**

## The surfaces

The answer column is the conservative default from
[#59](https://github.com/Flowfin/jellyfin-plugin-discover/issues/59): a discover
title does not appear anywhere except the discover pages themselves, until a
decision says otherwise for a named surface. Question 6 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) is the one
that could change the search row and has no answer.

| Controller  | What it returns                                      | Reaches a channel item?                                                                                                                                   | Answer                                                                                    |
| ----------- | ---------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Search      | search hints                                         | yes, and the hint has a field for it: `SearchHint.ChannelId`                                                                                              | does not appear; this is the row the issue is named for and the row question 6 could move |
| Suggestions | random items of a type                               | yes: one recursive query, no parent, no source-type filter                                                                                                | does not appear; a suggestion the server cannot play is the worst case of all of these    |
| Items       | the general item query                               | yes: it is the query type itself, and every filter is the caller's                                                                                        | does not appear unless the caller asked for the discover parent                           |
| UserViews   | the top-level views                                  | no query of its own; a discover page is a view because it is a channel                                                                                    | appears as the discover view itself                                                       |
| UserLibrary | one item by id, intros, local trailers, latest media | yes by id, which is how a client opens a discover title; for latest media under a channel parent the channel manager answers instead of the library query | appears by id, which is the path a user takes into a discover title                       |
| Library     | similar items, ancestors, media folders              | yes: four queries, none filtered by source type                                                                                                           | does not appear                                                                           |
| Movies      | recommendations                                      | yes; on `v10.11.11` the query is in the controller, on `v12.0-rc4` it moved behind `ISimilarItemsManager`                                                 | does not appear                                                                           |
| TvShows     | next up, upcoming, episodes                          | yes by query shape; in practice it needs a series the server holds                                                                                        | does not appear                                                                           |
| Collection  | a collection's contents                              | only what a user or an operator put in one                                                                                                                | does not appear unless somebody adds one by hand                                          |
| Playlists   | a playlist's contents                                | same                                                                                                                                                      | same                                                                                      |
| Genres      | genres and their items                               | yes: four queries, unfiltered                                                                                                                             | does not appear                                                                           |
| MusicGenres | music genres and their items                         | yes: four queries, unfiltered                                                                                                                             | does not appear                                                                           |
| Studios     | studios and their items                              | yes: one query, unfiltered                                                                                                                                | does not appear                                                                           |
| Years       | years and their items                                | yes: one query, unfiltered                                                                                                                                | does not appear                                                                           |
| Persons     | people                                               | people are by-name items rather than titles                                                                                                               | does not appear                                                                           |
| Artists     | music artists                                        | yes by query shape, and only for audio                                                                                                                    | does not appear                                                                           |
| InstantMix  | a generated audio mix                                | audio only                                                                                                                                                | does not appear                                                                           |
| Trailers    | trailers                                             | a discover title has no media source, so it has no trailer of its own                                                                                     | does not appear                                                                           |
| Videos      | additional parts, and the streams                    | a discover title has nothing to stream                                                                                                                    | does not appear                                                                           |
| ItemUpdate  | metadata editing                                     | reachable by id, like any item                                                                                                                            | not decided here; editing a discover title is an operator action and belongs with #58     |
| Channels    | channels and their contents                          | this is the discover pages' own endpoint                                                                                                                  | appears, and that is the point                                                            |
| LiveTv      | live tv channels, recordings, programmes             | its three queries are scoped by `ChannelIds` the caller supplies from the live tv channel list, and ask for programmes                                    | does not appear                                                                           |

The "reaches" column is a reading of the query each controller builds, counted
per file:

    git show v10.11.11:Jellyfin.Api/Controllers/SuggestionsController.cs | grep -n 'new InternalItemsQuery' -A 11

which prints the one recursive, unparented, unfiltered query the Suggestions row
is about.

## What differs between the two lines

Two things, and neither changes an answer above.

**Search was rebuilt.** On `v10.11.11` it is one file. On `v12.0-rc4` that file
is gone and search is a provider model:

    git ls-tree -r --name-only v10.11.11 | grep -i searchengine
    Emby.Server.Implementations/Library/SearchEngine.cs
    MediaBrowser.Controller/Library/ISearchEngine.cs

    git ls-tree -r --name-only v12.0-rc4 | grep -iE 'Library/Search/|IInternalSearchProvider|ChannelLatestMediaSearch'
    Emby.Server.Implementations/Library/Search/SearchManager.cs
    Emby.Server.Implementations/Library/Search/SqlSearchProvider.cs
    MediaBrowser.Controller/Channels/ChannelLatestMediaSearch.cs
    MediaBrowser.Controller/Library/IInternalSearchProvider.cs

The newer line has a channel-facing search type of its own, which means whatever
this plugin does about search has two shapes rather than one. Both lines are
carried: question 1 on #2 was answered on 2026-08-24 with 10.11 and 12.0, so
this is that answer's cost rather than a cost the answer might avoid.

**Movie recommendations moved out of the controller.** The endpoint is on both
lines; the query construction is in `MoviesController` on `v10.11.11` and behind
`ISimilarItemsManager` on `v12.0-rc4`.

## What this page does not establish

The "reaches" column is derived from the query each controller builds, not from
a running server. Nothing here was observed against a server, because there is
nothing to observe: no discover title exists yet.

It covers the HTTP surface. A plugin that walks the library through
`ILibraryManager` is not an API controller and is not in this list, and it is
the case the issue names as the one nobody thinks of. There is no way to
enumerate what a third-party plugin does, so that stays a limit rather than a
row, and it belongs in the limits page under
[#114](https://github.com/Flowfin/jellyfin-plugin-discover/issues/114).

Whether the server offers any way to keep a channel item out of a given surface
is answered above only in the negative direction: no controller sets
`SourceTypes`. Whether a plugin can make one do so is a different question and
is not read here.

The answers are the conservative default. None of them is implemented, none is
held by a test, and the issue this page belongs to stays open for exactly that
reason.
