# What fixes a discover title's identity

A discover title becomes a row in the operator's library database, and which row
it becomes is decided before this plugin gets a say in it. Three things go into
that decision. One of them this plugin chooses deliberately, one of them looks
to an operator like a label, and one of them arrives with the title from the
source.

This page is the reading of the server that
[#60](https://github.com/Flowfin/jellyfin-plugin-discover/issues/60) is built
on, the form the plugin chose, and what each of the three costs when it moves.

## How the server decides

Two server lines, both read at a tag rather than at a branch:

    git -C <a jellyfin checkout> rev-parse v10.11.11 v12.0-rc4
    1fbd8739292cce610231be93daf43368733edf63
    b3a06113029585594fe7a44becbfae7d2bdd9974

An item's identifier is hashed out of the identifier the plugin supplied and the
surface's own name, on both lines:

    git grep -n 'GetIdToHash' v10.11.11 v12.0-rc4 -- src/Jellyfin.LiveTv/Channels/ChannelManager.cs
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:923:        private static string GetIdToHash(string externalId, string channelName)
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:933:            var id = _libraryManager.GetNewItemId(GetIdToHash(idString, channelName), typeof(T));
    v12.0-rc4:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:921:        private static string GetIdToHash(string externalId, string channelName)
    v12.0-rc4:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:931:            var id = _libraryManager.GetNewItemId(GetIdToHash(idString, channelName), typeof(T));

What the two are concatenated with is a constant the server increments when it
wants every item downloaded again:

    git show v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs | sed -n '923,927p'
        private static string GetIdToHash(string externalId, string channelName)
        {
            // Increment this as needed to force new downloads
            // Incorporate Name because it's being used to convert channel entity to provider
            return externalId + (channelName ?? string.Empty) + "16";
        }

The third thing is the type, added by the hash itself:

    git show v10.11.11:Emby.Server.Implementations/Library/LibraryManager.cs | sed -n '650,657p'
                if (forceCaseInsensitive || !_configurationManager.Configuration.EnableCaseSensitiveItemIds)
                {
                    key = key.ToLowerInvariant();
                }

                key = type.FullName + key;

                return key.GetMD5();

So the identity of a discover title is the address this plugin supplied, the
surface's name, the server's own constant, and the class the server
materialises the title as. Nothing else, and none of the four is a field this
plugin can set afterwards.

The lowercasing in that excerpt is off by default and an operator can turn it
on:

    git grep -n 'EnableCaseSensitiveItemIds' v10.11.11 v12.0-rc4 -- MediaBrowser.Model/Configuration/ServerConfiguration.cs
    v10.11.11:MediaBrowser.Model/Configuration/ServerConfiguration.cs:89:    public bool EnableCaseSensitiveItemIds { get; set; } = true;
    v12.0-rc4:MediaBrowser.Model/Configuration/ServerConfiguration.cs:89:    public bool EnableCaseSensitiveItemIds { get; set; } = true;

Where it is on, two addresses that differ only in the case of their letters are
one item. None of the three bodies below issues identifiers that collide that
way, so this is a bound on what a later body may be given rather than a problem
today.

## The address this plugin supplies

`TitleAddress` in the surface is the one place a title's address is made. The
form is the body that issued the identifier and the identifier as that body
spells it, separated by a colon:

    imdb:tt2543164
    tmdb:329865
    tvdb:121361

Which of a title's identifiers stands for it is the precedence in
`DiscoverTitleIdentity.Precedence`, argued where it is declared: IMDb first
because none of the sources this plugin fetches from issues those identifiers
and all of them carry them, so the address survives the day a shelf is served by
a different source.

What is deliberately not in the address:

- The name. A name is translated, so an address holding one would move the day
  the server's language changed and take every item with it.
- The shelf. A title leaves one shelf and joins another whenever a source's
  list moves, which is most refreshes.
- The position, the page and the time of the fetch, all of which move for
  reasons that are not the title.

`TitleAddressTests` holds those, and reads an address back into the identifier
it was made from, which is what a request for a series folder arrives carrying.

## What a rename of the surface costs

Everything. The surface's name is in the hash of every item under it, and it is
also in the identifier of the surface's own row:

    git show v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs | sed -n '586,591p'
        private Guid GetInternalChannelId(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            return _libraryManager.GetNewItemId("Channel " + name, typeof(Channel));
        }

    git grep -c 'private Guid GetInternalChannelId' v12.0-rc4 -- src/Jellyfin.LiveTv/Channels/ChannelManager.cs
    v12.0-rc4:1

So after a rename every title is a new row, and whatever a user had marked on
the old one, a favourite or a played state, stays on the old one. The old rows
are not cleaned up either. The server does remove items it no longer sees, but
it looks for them under the parent it is refreshing:

    git show v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs | sed -n '738,740p'
                    var existingIds = _libraryManager.GetItemIds(query);
                    var deadIds = existingIds.Except(internalItems)
                        .ToArray();

After a rename that parent is a new row as well, so the previous subtree is not
in any query the refresh makes and nothing goes looking for it.

This is read from the server's source and has not been watched happening on a
running server. There is no surface to rename yet:
[#53](https://github.com/Flowfin/jellyfin-plugin-discover/issues/53) is where
one arrives.

## The surface's name is fixed, and is not a setting

Given the cost above, the name is chosen once in the build and there is no
control that changes it, which is the decision
[#60](https://github.com/Flowfin/jellyfin-plugin-discover/issues/60) asks for.

An operator who could type a new name into a box would be one keystroke from
losing every favourite their users had marked, with nothing on the page telling
them so at the moment they did it, and the loss would not show up until the next
refresh. A setting worth that would have to buy something, and what it buys is a
different word on one library tile.

The same reasoning binds the build. Changing the name in a release is the same
event as an operator changing it, so it is a breaking change rather than a
wording improvement, and a release that makes it says so.

What the name actually is comes from
[#53](https://github.com/Flowfin/jellyfin-plugin-discover/issues/53), and the
plugin's own display name is question 10 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2). This page
decides neither. It decides only that the surface's name is not an operator's to
change.

## What still moves an identity

Two things, both worth knowing before they are met.

**A title gains an identifier from a higher-precedence body.** A response that
carried only a TMDB identifier last week and carries an IMDb one as well this
week is one title throughout, and `DiscoverTitleIdentity.Agrees` says so, but
its primary has moved and so has its address. The item is created again and the
previous one is orphaned exactly as a rename orphans it, for one title rather
than for all of them. `TitleAddressTests` asserts this rather than leaving it to
be discovered.

Removing it means pinning the address the first time a title is seen and reading
the pinned value afterwards, which needs somewhere to keep it. The catalogue is
where that belongs and it does not store records yet:
[#65](https://github.com/Flowfin/jellyfin-plugin-discover/issues/65) settled
where it lives. Until then the derivation is the whole answer, and this
paragraph is the limit that
[#114](https://github.com/Flowfin/jellyfin-plugin-discover/issues/114) collects.

**A title changes kind at the source.** The class the server materialises a
title as follows the kind the adapter mapped it to, a film as a movie and a
series as a folder, and the class is in the hash. A source that reclassifies a
title moves its identity for the same reason a rename does. This is rare enough
that no code here works around it, and it is written down so that a report of it
is recognised rather than investigated from the start.

## What this page does not establish

Nothing here was observed against a running server. Every claim above is read
out of the server's source at the two tags named at the top, with the command
beside it, and the suite that holds this plugin's half of it starts no server:
what `TitleAddressTests` proves is the address, not what the server then does
with it. The end-to-end half is
[#38](https://github.com/Flowfin/jellyfin-plugin-discover/issues/38).
