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

Nothing in that description is built. There is no catalogue, nothing contacts a
metadata source, and no new browsing appears anywhere in a client. Read every
sentence in the section above as a plan and not as behaviour you can install.

What has been built is underneath it rather than in front of it: the gate this
repository runs on every change, the test project and the rules the suite lives
under, the plugin's own identity, and a configuration that carries a schema
version and no settings. None of that is visible to a user, which is why the
paragraph above says what it says.

The plan is the issue tracker. It is organised into milestones, each with an
issue that says what the milestone ends with, and the first one is
[M1](https://github.com/Flowfin/jellyfin-plugin-discover/milestone/1). Ten
questions in it are open decisions rather than work, and they are collected in
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2).

## Which server this builds against

No set of supported server lines has been chosen yet. That choice is question 1
in [#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), and it is
a decision about which lines this project will fix a bug in, which is a
different question from which line the tree builds against today.

The tree no longer disagrees with itself about the second question.
`Directory.Build.props` is the one place a server line is stated and everything
else derives from it:

    git grep -nE '<Jellyfin(PackageVersion|TargetFramework|DeclaredLines)>' -- Directory.Build.props
    Directory.Build.props:35:        <JellyfinPackageVersion>10.11.11</JellyfinPackageVersion>
    Directory.Build.props:36:        <JellyfinTargetFramework>net9.0</JellyfinTargetFramework>
    Directory.Build.props:41:        <JellyfinDeclaredLines>10.11</JellyfinDeclaredLines>

The project file reads both rather than repeating either, and the packaging
metadata is compared against the same source by the build, which fails on a
difference:

    git grep -n 'TargetFramework\|Jellyfin.Controller' -- Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj
    Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj:5:    <TargetFramework>$(JellyfinTargetFramework)</TargetFramework>
    Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj:15:    <PackageReference Include="Jellyfin.Controller" Version="$(JellyfinPackageVersion)" >

    git grep -nE '^targetAbi|^framework' -- build.yaml
    build.yaml:10:targetAbi: "10.11.0.0"
    build.yaml:11:framework: "net9.0"

So one line is declared, 10.11, and it is the only one anything here is built or
checked against. Read that as the line the tree carries and not as a support
commitment, which is still question 1's to make.

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
