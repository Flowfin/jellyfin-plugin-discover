# 0003 The catalogue lives in the folder the server gives this plugin

Decided. Raised by
[#65](https://github.com/Flowfin/jellyfin-plugin-discover/issues/65).

## What was decided

The catalogue lives in a `catalogue` directory under the folder the server
derives for this plugin, which the base plugin class computes while it is being
constructed and exposes as its data folder path. Nothing else in this plugin
writes anywhere, and a document name that would resolve outside that directory
is refused where the path is built rather than where it is used.

## What it was decided against

Four paths were on the table because the server offers all four:

    git grep -n 'string DataPath\|string PluginsPath\|string PluginConfigurationsPath\|string CachePath' v10.11.11 v12.0-rc4 -- MediaBrowser.Common/Configuration/IApplicationPaths.cs
    v10.11.11:MediaBrowser.Common/Configuration/IApplicationPaths.cs:32:        string DataPath { get; }
    v10.11.11:MediaBrowser.Common/Configuration/IApplicationPaths.cs:44:        string PluginsPath { get; }
    v10.11.11:MediaBrowser.Common/Configuration/IApplicationPaths.cs:50:        string PluginConfigurationsPath { get; }
    v10.11.11:MediaBrowser.Common/Configuration/IApplicationPaths.cs:74:        string CachePath { get; }
    v12.0-rc4:MediaBrowser.Common/Configuration/IApplicationPaths.cs:32:        string DataPath { get; }
    v12.0-rc4:MediaBrowser.Common/Configuration/IApplicationPaths.cs:44:        string PluginsPath { get; }
    v12.0-rc4:MediaBrowser.Common/Configuration/IApplicationPaths.cs:50:        string PluginConfigurationsPath { get; }
    v12.0-rc4:MediaBrowser.Common/Configuration/IApplicationPaths.cs:74:        string CachePath { get; }

**The cache path**, rejected. The argument for it is that the catalogue is
derived data with a legal expiry on it, and it does not survive contact with
what is already there. The server writes what a channel returned into that path
on its own, without being asked:

    git grep -n -A4 'return Path.Combine(' v10.11.11 -- src/Jellyfin.LiveTv/Channels/ChannelManager.cs
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:915:            return Path.Combine(
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs-916-                _config.ApplicationPaths.CachePath,
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs-917-                "channels",
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs-918-                channelId,
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs-919-                version,

So putting this plugin's copy there too puts the answer and the fallback in the
one directory an operator is told they may clear, and clearing it takes both in
the same stroke. The expiry argument does not survive the move either: nothing
about that path enforces one. The server's own reader judges a file by its age
against a window of its own, and no route deletes anything on a source's terms.
Whatever [#68](https://github.com/Flowfin/jellyfin-plugin-discover/issues/68)
decides is a deletion this plugin performs, wherever the file sits, so choosing
the cache path buys the appearance of an expiry rather than the expiry.

**The data path and the plugin configurations path**, rejected. Both are shared
with the server and with every other plugin, so a name in either has to be
chosen to be unlikely rather than derived. That is the property this decision
exists to avoid needing.

**The plugins path**, rejected for the same reason and one more: it is the
directory the server unpacks installed plugins into, so writing data beside them
puts this plugin's state where an uninstall and an upgrade both operate.

## What the choice buys

The folder is not a name anybody picked. The base class derives it from the file
name of the assembly the server loaded:

    git grep -n -A5 'var dataFolderPath = Path.Combine' v10.11.11 v12.0-rc4 -- MediaBrowser.Common/Plugins/BasePluginOfT.cs
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs:50:            var dataFolderPath = Path.Combine(ApplicationPaths.PluginsPath, Path.GetFileNameWithoutExtension(assemblyFilePath));
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs-51-            if (Version is not null && !Directory.Exists(dataFolderPath))
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs-52-            {
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs-53-                // Try again with the version number appended to the folder name.
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs-54-                dataFolderPath += "_" + Version;
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs-55-            }
    --
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs:50:            var dataFolderPath = Path.Combine(ApplicationPaths.PluginsPath, Path.GetFileNameWithoutExtension(assemblyFilePath));
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs-51-            if (Version is not null && !Directory.Exists(dataFolderPath))
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs-52-            {
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs-53-                // Try again with the version number appended to the folder name.
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs-54-                dataFolderPath += "_" + Version;
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs-55-            }

Same code on both lines. A collision with another plugin's data therefore needs
another plugin shipping an assembly with this plugin's file name, which is a
collision that breaks the install before it reaches any catalogue. That is
asserted rather than argued, in
`Jellyfin.Plugin.Template.Tests/CatalogueDirectoryTests.cs`.

## What an operator clearing it by hand would break

Removing the directory removes the catalogue and nothing else. The
configuration is not in it, so the shelves an operator defined, their bounds and
their source settings survive. What is lost is every fetched title, so the
surface is empty until the next refresh succeeds, and on a server whose source
is unreachable it stays empty for as long as that lasts. That is the cost the
cache path would have imposed on every operator who cleared a cache, and here it
is the cost of removing a directory nobody is told to remove.

## The hazard this decision carries

The branch quoted above can put the data under a second name with the version
appended. This note said whether it is ever taken is not evaluated here and is
not a property this tree can read. The second half is right and the first half
was a gap rather than a limit: it is readable at the two tags every other block
on this page is read at, and the answer is that the branch cannot be taken.

`Version` is a property with a private setter and no `virtual`, so nothing
outside the base class assigns it and no derived plugin overrides it:

    git grep -n 'public Version Version { get; private set; }' v10.11.11 v12.0-rc4 -- MediaBrowser.Common/Plugins/BasePlugin.cs
    v10.11.11:MediaBrowser.Common/Plugins/BasePlugin.cs:37:        public Version Version { get; private set; }
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePlugin.cs:37:        public Version Version { get; private set; }

The one assignment is inside `SetAttributes`:

    git grep -n 'Version = assemblyVersion;' v10.11.11 v12.0-rc4 -- '*.cs'
    v10.11.11:MediaBrowser.Common/Plugins/BasePlugin.cs:85:            Version = assemblyVersion;
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePlugin.cs:85:            Version = assemblyVersion;

and the only call to it in the server tree is line 57 above, seven lines below
the branch that reads the property, in the same constructor:

    git grep -n 'SetAttributes(assemblyFilePath, dataFolderPath, assemblyName.Version)' v10.11.11 v12.0-rc4 -- '*.cs'
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs:57:            SetAttributes(assemblyFilePath, dataFolderPath, assemblyName.Version);
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs:57:            SetAttributes(assemblyFilePath, dataFolderPath, assemblyName.Version);

So on a fresh instance `Version` is null when line 51 tests it, the condition is
false, and the unsuffixed folder is what `SetAttributes` is then given. The
catalogue's directory does not move on an upgrade by that route, on either line.

THE DIRECTORY CAN STILL MOVE, BY THE OTHER HALF OF THE SAME EXPRESSION. The name
is `Path.GetFileNameWithoutExtension(assemblyFilePath)`, so a release that ships
an assembly under a different file name derives a different folder and leaves the
old one full. The rename this repository still owes is
[#14](https://github.com/Flowfin/jellyfin-plugin-discover/issues/14), and it
costs nothing today because nothing has been published, so no server holds a
folder under the old name. It stops being free at the first release, which is the
ordering that matters rather than the hazard itself. Whether that duplication is
handled is
[#107](https://github.com/Flowfin/jellyfin-plugin-discover/issues/107)'s.

The path is not re-derived in this plugin. The base class owns that rule,
including the branch, and a second copy of it here would be a copy that drifts.
What is owed is that the resolved directory is written to the log once at
startup, so a move is visible rather than silent. That is unchanged by the
reading above: it covers the rename route, which is the one that is live, rather
than the version route, which is not. The log line is not in this change and is
owed by whatever first constructs the store.

Both readings are of the server's source at two tags rather than of a running
server, and neither says what a later line does.

## What is not decided here

THE LAYOUT IS DECIDED NOW, IN
[0005](0005-the-catalogue-is-one-document-per-shelf.md), AND THE PARAGRAPH BELOW
IS WHAT STOOD HERE UNTIL IT WAS. It is kept rather than replaced because it names
the reason this note declined, and 0005's argument is that the reason stopped
holding rather than that it was wrong.

The layout inside the directory: one file per shelf, or one file for all. That
is decided against the concurrency the refresh in
[#87](https://github.com/Flowfin/jellyfin-plugin-discover/issues/87) needs, and
#87 does not exist, so deciding now would be deciding by preference. It is the
one condition on #65 that this note does not answer.

What 0005 takes it against instead is three settled things that arrived without
the refresh: partial success is already defined on
[#79](https://github.com/Flowfin/jellyfin-plugin-discover/issues/79) and fixes
the unit a write succeeds at, retention landed under
[#68](https://github.com/Flowfin/jellyfin-plugin-discover/issues/68) and is
enforced per record, and the store states its own bounds on a read's memory and
on two writes of one document. This note's own decision is untouched by that.

## What would reverse this

A server line that stops deriving a per-plugin folder, or derives it from
something an operator can change. The catalogue would then need a name of its
own under one of the shared paths, and the collision argument above would have
to be replaced by something rather than dropped.

A decision under [#68](https://github.com/Flowfin/jellyfin-plugin-discover/issues/68)
that a source's terms require the catalogue to be removable by the operator
through a route the server already offers would also reopen this, because that
is the one thing the cache path gives and this does not.
