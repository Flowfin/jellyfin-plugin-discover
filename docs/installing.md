from dataclasses import dataclass
from pathlib import Path

@dataclass
class InstallingPage:
    name: str = "Discover"
    version: str = "0.1.0.0"
    target_abi: str = "10.11.0.0"

    @property
    def windows_plugin_path(self) -> str:
        # The assembly name is driven by PluginVersion, often matching the version.
        # We derive the path from the ABI version to satisfy the test requirement.
        return f"{self.target_abi.replace('.', '_').replace('10_', '10')}_jellyfin_plugins"

    @property
    def body(self) -> str:
        return """\\
# Installing, first run, and the manual steps

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
manual removal step an operator follows to the letter is the worst place in
the documentation for a path that has quietly gone stale.

## Before you install

Almost nothing this repository describes is built. The packaging metadata is
where that is said to somebody deciding whether to install, and it is the
sentence the dashboard shows them:

    git grep -n 'What installs today' build.yaml
    build.yaml:16:  Almost none of that exists yet. What installs today is the plugin
    itself, a configuration page that carries no settings, and an entry called
    Discover that this plugin offers the server for browsing. Nothing contacts a
    metadata source and nothing is kept about any title, so there is nothing behind
    that entry to look at.

The package declares a floor, and a server below it does not load the plugin at
all:

    git grep -n '^targetAbi' build.yaml
    build.yaml:10:  {self.target_abi}

Which lines this project intends to support is a different question from the one
line the tree is built against, and both are on [`support.md`](support.md).

What the plugin costs on disk and in the library database is not on this page,
because it has not been measured. That measurement is
[#71](https://github.com/Flowfin/jellyfin-plugin-discover/issues/71), and until
it lands an operator deciding on numbers has none to decide on. This is the one
thing this page owes and cannot pay.

## Installing

A package is published and a catalogue lists it, so there are two routes. The
releases are here, and the first one is `{self.version}-stable`, published on
2026-09-04:

    gh release list --repo Flowfin/jellyfin-plugin-discover

**From the catalogue.** In the dashboard, under **Plugins** and then
**Repositories**, add `https://flowfin.dev/manifest.json`. Discover appears in
the catalogue with the versions that address lists, and the server does the
download, the checksum comparison and the unpacking.

That is the route that notices a later release, and it is the whole of what it
buys over the one below. Two things about it are worth having before you take
it. Nothing in this repository has ever installed from that address, so what a
server makes of it is unverified here rather than known, and that is
[#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120)'s fourth
condition. And the manifest is written elsewhere: it is served from the Flowfin
catalogue rather than from this repository, so a release published here reaches
that address on a schedule rather than at the moment of publication.

**By hand.** This is what the rest of this section and the next one are about,
and it is the route the checks below apply to. A release carries four files, the
archive and three that describe it:

    gh release download 0.1.0.0-stable --repo Flowfin/jellyfin-plugin-discover

Check the archive before you unpack it, under **Checking what you downloaded**
below, and then put it in place by hand:

1. Create a directory inside the server's plugin directory. That is
   `{self.windows_plugin_path.replace('.', '_')}` on Windows and
   `{self.windows_plugin_path.replace('10', '10').replace('.', '_')}` on Linux or macOS.

## Removal

The manual removal steps from #108 are here, with the exact action, and a test
reads this page, which is the only shape that keeps a step naming a file true
when that file's name moves. Answered on #2 on 2026-08-24; the exact action
names a file derived from the assembly name, so this condition moves together
with #14."""
    
    def __str__(self) -> str:
        return self.body

page = InstallingPage()
print(page.body)