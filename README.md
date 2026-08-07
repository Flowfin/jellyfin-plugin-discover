> [!NOTE]
>
> **Part of [Flowfin](https://github.com/Flowfin).** It works with any Jellyfin
> server, and with the Flowfin clients.

# jellyfin-plugin-discover

A Jellyfin server plugin that adds a place to browse titles the server does not
have. Shelves are built from third party metadata sources, they appear as
ordinary browsable pages so that every existing client can show them with no
client change, and when a user asks for one of those titles a companion
requests plugin can pick the request up. The plugin is meant to be useful on a
server with no companion plugin installed.

## Status

Nothing in that description is built. This repository is the official Jellyfin
plugin template with a handful of security workflows added on top, and the work
that turns it into the plugin above has not started. Read every sentence in the
section above as a plan and not as behaviour you can install.

The plan is the issue tracker. It is organised into milestones, each with an
issue that says what the milestone ends with, and the first one is
[M1](https://github.com/Flowfin/jellyfin-plugin-discover/milestone/1). Ten
questions in it are open decisions rather than work, and they are collected in
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2).

## Which server this builds against

No set of supported server lines has been chosen yet. That choice is question 1
in [#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), and
[#15](https://github.com/Flowfin/jellyfin-plugin-discover/issues/15) is where it
lands as one place the rest of the tree derives from.

What the tree says today is three different things, all inherited from the
template:

    git grep -n 'targetAbi\|framework' -- build.yaml
    build.yaml:5:targetAbi: "10.9.0.0"
    build.yaml:6:framework: "net8.0"

    git grep -n 'TargetFramework\|Jellyfin.Controller' -- Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj
    Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj:4:    <TargetFramework>net9.0</TargetFramework>
    Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj:14:    <PackageReference Include="Jellyfin.Controller" Version="10.9.11" >

So it compiles against the 10.9 package set on net9.0 while declaring a net8.0
framework and a 10.9.0.0 ABI floor in the packaging metadata. Do not read any of
those three numbers as a supported line. They are the state #15 removes.

## Building

    dotnet build Jellyfin.Plugin.Template.sln -c Release

The assembly lands under `Jellyfin.Plugin.Template/bin/Release/net9.0/`.

## Running it against a local server

There is no editor scaffolding in this repository. Every path in the template's
`.vscode` configuration was derived from one setting naming the template's
project, and this plugin's own name and identifier are not minted yet
([#14](https://github.com/Flowfin/jellyfin-plugin-discover/issues/14)), so those
tasks would have had to be rewritten the moment they were. The directory was
removed rather than left building a solution that will not exist. The steps it
automated are these, and they are short enough to run by hand:

1. Build, as above.
2. Create a directory named after the plugin inside the server's plugin
   directory. That is `%LOCALAPPDATA%\jellyfin\plugins\` on Windows and
   `~/.local/share/jellyfin/plugins/` on Linux, unless the server was told to
   keep its data somewhere else.
3. Copy everything from the build output directory into it.
4. Restart the server, and read the server log for the plugin being loaded.

Whether a package built from this tree actually loads on a server is
[#19](https://github.com/Flowfin/jellyfin-plugin-discover/issues/19), and until
that issue closes nobody has checked it.

## Licence

This repository ships the GNU General Public License, version 3, in
[LICENSE](LICENSE), and that is the licence the plugin is under.

A Jellyfin plugin links against the Jellyfin binary NuGet packages, which are
themselves under the GPLv3. A compiled plugin is therefore GPLv3 whatever the
source licence says, which is worth knowing before anybody reuses this code
under something more permissive.
