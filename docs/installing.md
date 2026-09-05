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

A package is published and no catalogue entry is, and both halves decide what an
operator does. The releases are here, and the first one is `0.1.0.0-stable`,
published on 2026-09-04:

    gh release list --repo Flowfin/jellyfin-plugin-discover

Publishing a manifest is
[#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120) and has
not happened, so there is still no address a server's plugin catalogue can be
pointed at. The dashboard will not find this plugin, and nothing on the server
notices a later release: an operator installing this way is the thing that has
to watch for one.

A release carries four files, the archive and three that describe it:

    gh release download 0.1.0.0-stable --repo Flowfin/jellyfin-plugin-discover

Check the archive before you unpack it, under **Checking what you downloaded**
below, and then put it in place by hand:

1. Create a directory inside the server's plugin directory. That is
   `%LOCALAPPDATA%\jellyfin\plugins\` on Windows and
   `~/.local/share/jellyfin/plugins/` on Linux, unless the server was told to
   keep its data somewhere else.
2. Unpack the archive into it.

Building a package out of this tree instead is under **Running it against a
local server** in [the README](../README.md). That is the developer's route and
it produces no attestation, so nothing below applies to it.

Restart the server afterwards. The plugin is loaded at start-up and nothing
picks it up while the server is running.

## Checking what you downloaded

Two questions, and an answer to one is not an answer to the other.

**Did this come from this repository, and from the workflow that publishes it.**
The publish run signs a provenance statement for the archive, and `gh` compares a
downloaded file against it:

    gh attestation verify discover_0.1.0.0.zip \
      --repo Flowfin/jellyfin-plugin-discover \
      --signer-workflow Flowfin/jellyfin-plugin-discover/.github/workflows/publish.yaml

`--repo` alone answers the first half only. `gh attestation verify --help` calls
it the minimum and names `--signer-workflow` as what validates the signer
workflow's path, so a statement minted by some other workflow of this repository
satisfies `--repo` and is what the second flag is against. Its value is the path
of the publish workflow rather than a name somebody chose, and it is read off the
published attestation rather than typed from expectation.

**A PASS IS SILENT AND THE VERDICT IS THE EXIT STATUS.** Run against the first
published package on 2026-09-05, with both streams captured, the command printed
nothing whatever and ended 0:

    gh attestation verify discover_0.1.0.0.zip --repo Flowfin/jellyfin-plugin-discover --signer-workflow Flowfin/jellyfin-plugin-discover/.github/workflows/publish.yaml ; echo "exit=$?"
    exit=0

Naming a workflow that did not sign it ends 1 and says so:

    gh attestation verify discover_0.1.0.0.zip --repo Flowfin/jellyfin-plugin-discover --signer-workflow Flowfin/jellyfin-plugin-discover/.github/workflows/build-run.yml ; echo "exit=$?"

    Error: verifying with issuer "sigstore.dev"
    exit=1

So no output is the pass rather than the check having done nothing, which is the
way round an operator gets wrong, and a run whose status nobody read has not been
verified. Both runs are on `gh version 2.98.0 (2026-08-20)`; a later `gh` may
print where this one is quiet, and the exit status is the part to keep reading.

**Is this the file the release lists.** That is the other question, and it is the
checksum beside the archive rather than the attestation:

    sha256sum -c discover_0.1.0.0.sha256
    discover_0.1.0.0.zip: OK

The `.md5` is the same comparison in the value a Jellyfin catalogue serves as a
plugin checksum, for the day there is a catalogue to serve it.

**What neither answers.** Both bind the archive to a build; neither says what is
inside it. The list of what ships is the bill of materials, and it is written on
the build rather than on the publish, so it is not among the four files a release
carries and an operator following this section gets provenance rather than
contents.

And nothing re-checks any of this afterwards. The comparison an operator would
most want repeated is a catalogue's published checksum against the file it serves,
and there is no catalogue and no manifest to hold one, which is
[#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120) again. The
scheduled half of
[#124](https://github.com/Flowfin/jellyfin-plugin-discover/issues/124) waits on
that and on nothing in this page.

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

The disk half is held twice, because installing is not the only thing that
happens to an install nobody configured. The server also runs this plugin's
scheduled refresh once a day, and that is the one route here that reaches a
write:

    git grep -n "public void AStartWithNothingConfiguredWritesNothing\|public async Task ARunWithNothingConfiguredWritesNothing" -- Jellyfin.Plugin.Template.Tests/AFreshInstallWritesNothingTests.cs
    Jellyfin.Plugin.Template.Tests/AFreshInstallWritesNothingTests.cs:113:    public void AStartWithNothingConfiguredWritesNothing()
    Jellyfin.Plugin.Template.Tests/AFreshInstallWritesNothingTests.cs:188:    public async Task ARunWithNothingConfiguredWritesNothing()

The configuration page carries no controls. The configuration itself has five
properties with a setter: the schema version, the two bounds on how many titles
this plugin may write into the library database, the switch that turns the
plugin off without removing it, and the switch that lifts the exclusion of the
titles a source flags as adult:

    git grep -n 'public .* { get; set; }' -- Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs
    Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:46:    public int SchemaVersion { get; set; }
    Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:58:    public int MaximumTitlesPerShelf { get; set; }
    Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:69:    public int MaximumTitlesAcrossAllShelves { get; set; }
    Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:143:    public bool Enabled { get; set; } = true;
    Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:185:    public bool IncludeAdultTitles { get; set; }

A property is not a control. The page has no script that reads or writes a
configuration, so the bounds and the two switches are reachable today only by editing
the plugin's configuration document on disk, and their defaults are what an
operator who does nothing gets. What each one means and what it costs is
`docs/configuration.md`, and the page that would carry them as controls is
[#103](https://github.com/Flowfin/jellyfin-plugin-discover/issues/103).

So there is no key to enter, no refresh to start and no per-user decision to
take, and the bounds are set by hand or not at all. Each of those is an open
issue rather than a step this page has left out:
[#77](https://github.com/Flowfin/jellyfin-plugin-discover/issues/77) for where a
source key comes from,
[#58](https://github.com/Flowfin/jellyfin-plugin-discover/issues/58) for the
rest of the bound on what reaches the library database, which still owes a
measurement on a real server,
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

Before any of that the server invites the plugin to clean up after itself, and
this plugin takes the invitation: the data folder and everything under it goes at
that moment, on the plugin's own account rather than the server's.

    git grep -n 'plugin.Instance?.OnUninstalling();' v10.11.11 v12.0-rc4 -- Emby.Server.Implementations/Updates/InstallationManager.cs
    v10.11.11:Emby.Server.Implementations/Updates/InstallationManager.cs:395:            plugin.Instance?.OnUninstalling();
    v12.0-rc4:Emby.Server.Implementations/Updates/InstallationManager.cs:398:            plugin.Instance?.OnUninstalling();

That invitation is on the dashboard's route and the API's, and it is the only
route on which anything of this plugin runs. An operator who removes the plugin
by deleting its folder runs none of it, so the steps below are what to do after
a removal made that way, and what to check for after any removal.

### The manual steps

One path survives an uninstall taken from the dashboard and two survive a removal
made by hand. Both are under the server's plugins directory, which is
`%LOCALAPPDATA%\jellyfin\plugins\` on Windows and
`~/.local/share/jellyfin/plugins/` on Linux, unless the server was told to keep
its data somewhere else.

With the server stopped, delete whichever of these is still there:

| What              | Path, relative to the plugins directory       | What is in it                         |
| ----------------- | --------------------------------------------- | ------------------------------------- |
| The data folder   | `Jellyfin.Plugin.Template`                    | Everything this plugin ever wrote     |
| The configuration | `configurations/Jellyfin.Plugin.Template.xml` | The settings the dashboard page saved |

The data folder is in the table because a removal by hand leaves it, not because
an uninstall from the dashboard does. The configuration is the one that survives
either way: it sits in a directory this plugin may not compose a path into at
all, so nothing it can do reaches the file.

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

No installation of the published archive was watched. The commands under
**Checking what you downloaded** were run against the real published package on
2026-09-05 and what is pasted under them is what they printed, but nothing
unpacked that archive into a server, started one, or read a plugin list
afterwards.
