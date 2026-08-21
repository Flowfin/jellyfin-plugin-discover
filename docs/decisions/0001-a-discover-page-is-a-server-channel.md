# 0001 A discover page is a server channel

Decided. Raised in
[#51](https://github.com/Flowfin/jellyfin-plugin-discover/issues/51).

## The requirement that decides it

A discover page has to be browsable on a television client that nobody is going
to change. That is not a preference between three pleasant options. It removes
one of them outright and leaves the other two differing by how much of the
server they disturb.

## What was decided

The plugin implements the server's channel interface and registers it through
[#18](https://github.com/Flowfin/jellyfin-plugin-discover/issues/18). The server
then offers it to every client as one more entry in the list of libraries.

## Where the evidence comes from

Every `git` command below is run in a clone of `jellyfin/jellyfin`, not in this
repository, at two tags:

    git rev-parse v10.11.11 v12.0-rc4
    1fbd8739292cce610231be93daf43368733edf63
    b3a06113029585594fe7a44becbfae7d2bdd9974

A reader whose clone prints two different commit ids is reading different bytes
from the ones quoted here, and the outputs below are claims about those two.

One reading here is not of that kind, and it is marked where it sits rather than
only here. The third option is rejected on what another project publishes about
itself, which is a page on the network and not a tag in a clone, so it is quoted
at a pinned commit with the date it was read beside it. Everything else on this
page can be re-derived offline and that one cannot.

## Why a channel

The server adds channels to the views it returns for a user, on both lines, in
the same file at the same line:

    git grep -n 'GetChannelsInternalAsync' v10.11.11 v12.0-rc4 -- Emby.Server.Implementations/Library/UserViewManager.cs
    v10.11.11:Emby.Server.Implementations/Library/UserViewManager.cs:121:                var channelResult = _channelManager.GetChannelsInternalAsync(new ChannelQuery
    v12.0-rc4:Emby.Server.Implementations/Library/UserViewManager.cs:121:                var channelResult = _channelManager.GetChannelsInternalAsync(new ChannelQuery

A client that draws libraries draws this, because to the client it is a library.
Nothing is asked of the client at all.

The interface is also effectively the same on both lines, which is why one
implementation is expected to serve both:

    git diff --stat v10.11.11 v12.0-rc4 -- MediaBrowser.Controller/Channels MediaBrowser.Model/Channels
     .../Channels/ChannelItemResult.cs                    | 14 ++++++++++++--
     MediaBrowser.Controller/Channels/ChannelItemType.cs  | 11 +++++++++--
     .../Channels/ChannelLatestMediaSearch.cs             |  8 ++++++--
     .../Channels/ChannelParentalRating.cs                | 20 ++++++++++++++++++--
     .../Channels/ChannelSearchInfo.cs                    | 11 +++++++++--
     MediaBrowser.Controller/Channels/IHasCacheKey.cs     |  7 ++++---
     MediaBrowser.Controller/Channels/ISupportsDelete.cs  | 16 ++++++++++++++--
     .../Channels/ISupportsLatestMedia.cs                 |  5 +++--
     MediaBrowser.Model/Channels/ChannelFeatures.cs       | 13 +++++++------
     9 files changed, 82 insertions(+), 23 deletions(-)

All of that is nullable annotation and documentation.
[#31](https://github.com/Flowfin/jellyfin-plugin-discover/issues/31) is what
tells us the day that stops being true, by compiling against both package sets
rather than reasoning about the diff.

## Why not a library of placeholder files

This is what the nearest prior art does. It fails on two counts that are
properties of the server rather than of any implementation.

First, a plugin cannot declare a library type of its own. The set is a closed
enum in the server's own assembly:

    git grep -n 'public enum CollectionType' v10.11.11 v12.0-rc4 -- Jellyfin.Data/Enums/CollectionType.cs
    v10.11.11:Jellyfin.Data/Enums/CollectionType.cs:9:public enum CollectionType
    v12.0-rc4:Jellyfin.Data/Enums/CollectionType.cs:9:public enum CollectionType

So a discover library has to claim to be a movie or a series library, which is
precisely what makes its contents indistinguishable from titles the server
actually has.

Second, it needs files on a real path, and the resolver turns them into shortcut
items:

    git grep -n 'IsShortcut = extension.Equals' v10.11.11 -- Emby.Server.Implementations/Library/Resolvers/BaseVideoResolver.cs
    v10.11.11:Emby.Server.Implementations/Library/Resolvers/BaseVideoResolver.cs:147:            video.IsShortcut = extension.Equals(".strm", StringComparison.OrdinalIgnoreCase);

Those files are then scanned, backed up, indexed, and seen by every other plugin
the operator runs, and the operator's own library scanner becomes this plugin's
dependency. The cost is not the writing of files. It is that the plugin has put
content into the operator's library that looks owned and is not.

## Why not a surface of the plugin's own

An API of this plugin's, plus assets injected into the web client, is the most
freedom and the least reach. The prior art in this shape states the limit
itself: it works on the web client and on anything embedding it, and not on
native or television clients. That is the one requirement this plugin exists to
satisfy, so this is not a trade-off against the channel. It is a different
product.

The prior art is named here rather than alluded to, because a rejection resting
on what somebody else says about their own software is worth nothing to a reader
who cannot go and read it. It is
[`CodeDevMLH/jellyfin-plugin-media-bar-enhanced`](https://github.com/CodeDevMLH/jellyfin-plugin-media-bar-enhanced),
which draws a featured-content bar on the Jellyfin home screen by putting its
own JavaScript and CSS into the web interface. Read on 2026-08-21, at the commit
that last touched that file, `3c589261572836d4aead62b3664e74a1858798cb`, dated
2026-07-25:

    curl -sS -L https://raw.githubusercontent.com/CodeDevMLH/jellyfin-plugin-media-bar-enhanced/3c589261572836d4aead62b3664e74a1858798cb/README.md | grep -n -A 14 '^## Client Compatibility'
    178:## Client Compatibility
    179-
    180-Because this plugin relies on injecting JavaScript and CSS into the web interface, it works best on clients that use the web wrapper.
    181-
    182-| Client Platform | Status | Notes |
    183-| :--- | :---: | :--- |
    184-| **Web Browsers** (Firefox, Chrome etc.) | ✅ | Direct JS injection |
    185-| **Jellyfin Media Player** (Windows/Linux/macOS) | ✅ | Uses jellyfin web |
    186-| **Android App** | ✅ | Uses a web wrapper |
    187-| **iOS App** | ✅ | Uses a web wrapper |
    188-| **Android TV / Fire TV** | ❌ | **Not supported.** Uses a native Java/Kotlin UI. |
    189-| **Tizen OS** | ❌ | **Not supported.** Uses a native UI. |
    190-| **Roku** | ❌ | **Not supported.** Uses a native UI. |
    191-| **Swiftfin** (iOS/tvOS) | ❌ | **Not supported.** Uses a native Swift UI. |
    192-| **Kodi** (via Jellyfin Addon) | ❌ | **Not supported.** Uses Kodi's native skinning engine. |

The four platforms it claims are the web client and three things that wrap it.
The five it refuses are refused for one reason given five times, that the client
draws its own interface, and every television client this plugin has to reach is
among them. So the sentence above is that project's own table rather than a
reading of it.

What the quotation does not carry, so it is not taken wider than it is. That
project is a featured-content bar rather than a discover surface, and its page
states no API of its own, so it is prior art for the injected-assets half of the
shape and not for the whole of it. That is the half the reach hangs on: the
limit the table records is a property of putting assets into the web interface,
and an API beside them moves no row of it. The table is also that project's
claim about clients rather than anything measured here, and nothing on this page
has been measured against it.

It can also move under this page in a way no `git` command here can catch. It is
a third party's file, edited on their schedule, and a repository holding a
quotation of it has no way to notice. That is why the commit is pinned rather
than the branch: the pinned bytes are the ones read, and a reader who compares
the pin against the branch is comparing what was read against what is published
now.

## What the decision costs

These are not objections. They are the work the rest of M4 is, and each one has
an issue that carries it.

Channel items are written into the library database as real items, and a
movie-shaped one is materialised as a movie:

    git grep -n '_libraryManager.CreateItem' v10.11.11 v12.0-rc4 -- src/Jellyfin.LiveTv/Channels/ChannelManager.cs
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:492:                _libraryManager.CreateItem(item, null);
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:1145:                _libraryManager.CreateItem(item, parentFolder);
    v12.0-rc4:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:490:                _libraryManager.CreateItem(item, null);
    v12.0-rc4:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:1142:                _libraryManager.CreateItem(item, parentFolder);

    git grep -n 'ChannelMediaContentType.Movie' v10.11.11 v12.0-rc4 -- src/Jellyfin.LiveTv/Channels/ChannelManager.cs
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:991:                    ChannelMediaContentType.Movie => GetItemById<Movie>(info.Id, channelProvider.Name, out isNew),
    v12.0-rc4:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:989:                    ChannelMediaContentType.Movie => GetItemById<Movie>(info.Id, channelProvider.Name, out isNew),

So the database grows, and other item queries can see these items.
[#58](https://github.com/Flowfin/jellyfin-plugin-discover/issues/58) bounds the
first and [#59](https://github.com/Flowfin/jellyfin-plugin-discover/issues/59)
decides and tests the second.

The server caches what a channel returned for three hours, on both lines, so the
delay a user sees is the sum of this plugin's cadence and the server's:

    git grep -n 'CacheLength =>' v10.11.11 v12.0-rc4 -- src/Jellyfin.LiveTv/Channels/ChannelManager.cs
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:97:        private static TimeSpan CacheLength => TimeSpan.FromHours(3);
    v12.0-rc4:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:98:        private static TimeSpan CacheLength => TimeSpan.FromHours(3);

[#61](https://github.com/Flowfin/jellyfin-plugin-discover/issues/61) owns that.

An item's identity is derived from the external identifier and the channel's
name together, so renaming the channel orphans every item it ever created:

    git grep -n -A4 'private T GetItemById<T>' v10.11.11 -- src/Jellyfin.LiveTv/Channels/ChannelManager.cs
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:930:        private T GetItemById<T>(string idString, string channelName, out bool isNew)
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs-931-            where T : BaseItem, new()
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs-932-        {
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs-933-            var id = _libraryManager.GetNewItemId(GetIdToHash(idString, channelName), typeof(T));
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs-934-

[#60](https://github.com/Flowfin/jellyfin-plugin-discover/issues/60) owns that.

What any of these costs an operator, rather than a developer, belongs in
[#114](https://github.com/Flowfin/jellyfin-plugin-discover/issues/114) and is not
repeated here.

## What would reverse this

Any one of these, and none of them is in sight today.

A server line that removes channels, or that stops adding them to a user's
views. The first command above is the one to re-run against a later tag: if
`GetChannelsInternalAsync` is no longer called out of `UserViewManager`, a
channel is no longer a library a client draws, and the whole basis of this note
is gone.

A server line that lets a plugin declare a library type of its own. That removes
the first objection to placeholder files, and with it the reason those files have
to lie about what they are. The `CollectionType` command is the one to re-run.

Television and native clients gaining a way to render a surface a plugin brings
with it. That removes the objection to a surface of this plugin's own, which was
never that the approach is worse but that it does not reach the clients this
plugin is for.

A requirement change. If browsing on an unmodified television client stops being
the requirement, all three options are open again and this note decides nothing.
