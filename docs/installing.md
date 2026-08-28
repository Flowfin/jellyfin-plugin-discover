# Installing, first run, and what a removal leaves

The operator's page. What to do, in order, what to expect after each step, and
what is still on the server after the plugin has been uninstalled.

Two things about this page before the steps.

It points rather than restates. What a setting means is
[`configuration.md`](configuration.md), what this plugin cannot make good is
[`limits.md`](limits.md), and which server lines carry a commitment is
[`support.md`](support.md). A step that repeats one of those is a second copy
that goes stale while nobody is looking at it.

The names in the removal section are not typed here twice either. They are
derived from the assembly this build produces, and `InstallingPageTests` in the
test project reads this page and refuses a name that is not the one the build
carries. That matters because the assembly is due to be renamed under
[#14](https://github.com/Flowfin/jellyfin-plugin-discover/issues/14), and a
manual removal step an operator follows to the letter is the worst place in the
documentation for a path that has quietly gone stale.

## Before you install

Almost nothing this repository describes is built. The packaging metadata is
where that is said to somebody deciding whether to install, and it is the
sentence the dashboard shows them:

    git grep -n 'What installs today' build.yaml
    build.yaml:16:  Almost none of that exists yet. What installs today is the plugin itself, a configuration page that carries no settings, and an entry called Discover that this plugin offers the server for browsing. Nothing contacts a metadata source and nothing is kept about any title, so there is nothing behind that entry to look at.

The package declares a floor, and a server below it does not load the plugin at
all:

    git grep -n '^targetAbi' build.yaml
    build.yaml:10:targetAbi: "10.11.0.0"

Which lines this project intends to support is a different question from the one
line the tree is built against, and both are on [`support.md`](support.md).

What the plugin costs on disk and in the library database is not on this page,
because it has not been measured. That measurement is
[#71](https://github.com/Flowfin/jellyfin-plugin-discover/issues/71), and until
it lands an operator deciding on numbers has none to decide on. This is the one
thing this page owes and cannot pay.

## Installing

There is no manifest to install from. Publishing one is
[#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120) and has
not happened, so no address exists that a server's plugin catalogue can be
pointed at. What is left is a package built from this tree and copied into place
by hand, and the steps for that are under **Running it against a local server**
in [the README](../README.md) rather than a second time here.

Restart the server afterwards. The plugin is loaded at start-up and nothing
picks it up while the server is running.

## What you get after installing and doing nothing

An entry called Discover, and nothing behind it. That is the whole of it, and it
is worth saying plainly because an empty entry reads as a broken install rather
than an idle one.

Every level of that entry answers with nothing. The top level is a level this
plugin recognises and which holds no shelves; every other address is one it does
not recognise, and a client can tell the two apart by the total:

    git grep -n 'IsRoot ? SurfaceListing.EmptyLevel' -- Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs
    Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs:166:            asked.Parent.IsRoot ? SurfaceListing.EmptyLevel : SurfaceListing.NoSuchLevel);

Nothing is written and nothing is sent. The plugin makes no outbound call and
writes no catalogue on a server nobody has configured, and both halves are held
by tests rather than by there being nothing that could break them:

    git grep -n 'public void AStartWithNothingConfiguredOffersNoSource' -- Jellyfin.Plugin.Template.Tests/AFreshInstallHoldsNoWayOutTests.cs
    Jellyfin.Plugin.Template.Tests/AFreshInstallHoldsNoWayOutTests.cs:52:    public void AStartWithNothingConfiguredOffersNoSource()

    git grep -n 'public void AStartWithNothingConfiguredWritesNothing' -- Jellyfin.Plugin.Template.Tests/AFreshInstallWritesNothingTests.cs
    Jellyfin.Plugin.Template.Tests/AFreshInstallWritesNothingTests.cs:93:    public void AStartWithNothingConfiguredWritesNothing()

The configuration page carries no settings. The one property on the
configuration is the schema version, which is not a control and which an
operator has no reason to set:

    git grep -n 'public .* { get; set; }' -- Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs
    Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:36:    public int SchemaVersion { get; set; }

So there is no key to enter, no bound to choose, no refresh to start and no
per-user decision to take. Each of those is an open issue rather than a step
this page has left out:
[#77](https://github.com/Flowfin/jellyfin-plugin-discover/issues/77) for where a
source key comes from,
[#58](https://github.com/Flowfin/jellyfin-plugin-discover/issues/58) for the
bound on what reaches the library database,
[#88](https://github.com/Flowfin/jellyfin-plugin-discover/issues/88) for a
refresh an operator can trigger, and
[#57](https://github.com/Flowfin/jellyfin-plugin-discover/issues/57) for who
sees the surface at all.

## The waiting, once there is something to wait for

This does not bite yet, and it is written here because it will bite the first
operator who has something to refresh and no reason to expect a gap.

The server keeps what a surface returned for three hours, so a refresh that has
succeeded can leave a client showing the previous answer for that long. Read
from a checkout of `https://github.com/jellyfin/jellyfin.git` with both targeted
lines fetched:

    git rev-parse v10.11.11 v12.0-rc4
    1fbd8739292cce610231be93daf43368733edf63
    b3a06113029585594fe7a44becbfae7d2bdd9974

    git grep -n 'CacheLength =>' v10.11.11 v12.0-rc4 -- src/Jellyfin.LiveTv/Channels/ChannelManager.cs
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:97:        private static TimeSpan CacheLength => TimeSpan.FromHours(3);
    v12.0-rc4:src/Jellyfin.LiveTv/Channels/ChannelManager.cs:98:        private static TimeSpan CacheLength => TimeSpan.FromHours(3);

The refresh working and the change being visible are two events with a gap
between them. That row is on [`limits.md`](limits.md) as well, which is where
the reasoning lives.

## Removing it

Uninstalling from the dashboard deletes one directory, and it is the directory
the package was unpacked into:

    git grep -n 'Directory.Delete(plugin.Path, true);' v10.11.11 v12.0-rc4 -- Emby.Server.Implementations/Plugins/PluginManager.cs
    v10.11.11:Emby.Server.Implementations/Plugins/PluginManager.cs:654:                Directory.Delete(plugin.Path, true);
    v12.0-rc4:Emby.Server.Implementations/Plugins/PluginManager.cs:655:                Directory.Delete(plugin.Path, true);

That is not the directory this plugin writes to. The unpack directory is named
from the manifest name with the version appended; the plugin's own data folder
is named from the file name of the assembly the server loaded, directly under
the plugins path. They are two names side by side, and neither is inside the
other. Which two, and why, is
[#108](https://github.com/Flowfin/jellyfin-plugin-discover/issues/108) and
[0003](decisions/0003-the-catalogue-lives-in-the-plugins-own-data-folder.md).

### The manual steps

Two paths survive the uninstall, both under the server's plugins directory. That
directory is `%LOCALAPPDATA%\jellyfin\plugins\` on Windows and
`~/.local/share/jellyfin/plugins/` on Linux, unless the server was told to keep
its data somewhere else.

With the server stopped, delete both:

| What              | Path, relative to the plugins directory       | What is in it                         |
| ----------------- | --------------------------------------------- | ------------------------------------- |
| The data folder   | `Jellyfin.Plugin.Template`                    | Everything this plugin ever wrote     |
| The configuration | `configurations/Jellyfin.Plugin.Template.xml` | The settings the dashboard page saved |

Both names are derived from the assembly rather than chosen, so both move on the
day [#14](https://github.com/Flowfin/jellyfin-plugin-discover/issues/14) renames
it. `InstallingPageTests` is what stops this table saying the old name
afterwards.

Neither path exists on a server where the plugin was installed and never
configured, because nothing writes either of them until there is something to
write. Deleting what is not there is not an error, and an operator who finds
neither has nothing to do.

### What goes on its own, and when

The rows this plugin put in the library database are removed by the server
rather than by this plugin, on the server's own schedule rather than at the
moment of the uninstall. The task that does it removes the items of every
channel with no loaded provider behind it:

    git grep -n 'A task to remove all non-installed channels from the database.' v10.11.11 v12.0-rc4 -- src/Jellyfin.LiveTv/Channels/ChannelPostScanTask.cs
    v10.11.11:src/Jellyfin.LiveTv/Channels/ChannelPostScanTask.cs:14:    /// A task to remove all non-installed channels from the database.
    v12.0-rc4:src/Jellyfin.LiveTv/Channels/ChannelPostScanTask.cs:14:    /// A task to remove all non-installed channels from the database.

Its default trigger is an interval rather than a start-up, so there is a window
of up to a day in which an operator who has uninstalled the plugin still sees
its items. Running the channel refresh task from the server's scheduled tasks
page closes that window without waiting.

### What no step above removes

What a user marked on one of this plugin's titles. Removing an item does not
remove a favourite or a played mark on it: both server lines move that data to a
placeholder and stamp when the move happened, and the task that would later
delete it offers no trigger of its own. The reading is on
[`title-identity.md`](title-identity.md) and the rows are on
[`limits.md`](limits.md).

It is unreachable from anything a user can browse and it points at no item, and
it is the same treatment every removed item on the server gets. It is not
nothing, and an operator told the server is clean afterwards is being told
something narrower than they will hear.

## What holds this page

`InstallingPageTests` in the test project reads the table under **The manual
steps** and refuses a path that is not derived from the assembly this build
produces, in both directions: a row naming something the build does not carry,
and a build whose assembly name has no row.

It does not read the prose. Whether the steps above are in the right order,
whether the waiting is described the way an operator experiences it, and whether
anything is missing from the list of what is left behind, are judgements no
reading of this tree makes.

## What this page has not done

Nothing here was observed on a running server. Every claim about what the server
does on an install or an uninstall is read from its source at the two tags named
above, and watching one happen is the harness in
[#38](https://github.com/Flowfin/jellyfin-plugin-discover/issues/38).

No numbers. See **Before you install**.

No client was tried. What a client draws from an empty entry is
[#115](https://github.com/Flowfin/jellyfin-plugin-discover/issues/115).
