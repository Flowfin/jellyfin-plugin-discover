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

Nothing in that description works yet, and one part of it now appears without
working. A discover page shows up in a client, because the surface is registered
with the server:

    git grep -n 'AddSingleton<IChannel' -- Jellyfin.Plugin.Template/PluginServiceRegistrator.cs
    Jellyfin.Plugin.Template/PluginServiceRegistrator.cs:63:        serviceCollection.AddSingleton<IChannel, DiscoverSurfaceAdapter>();

Every level of that page is empty, the top of it included. What the top level and
any other address answer is no longer the same thing: the top is a level the
surface recognises and which holds nothing, and every other address is one it does
not recognise, which a client can tell apart by the total. Tests hold both rather
than the sentence resting on a reading of the source:

    git grep -n 'IsRoot ? SurfaceListing.EmptyLevel' -- Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs
    Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs:166:            asked.Parent.IsRoot ? SurfaceListing.EmptyLevel : SurfaceListing.NoSuchLevel);

    git grep -n 'TheTopLevelIsRecognisedAndHoldsNothingUntilTheShelvesExist' -- Jellyfin.Plugin.Template.Tests/DiscoverSurfaceTests.cs
    Jellyfin.Plugin.Template.Tests/DiscoverSurfaceTests.cs:135:    public async Task TheTopLevelIsRecognisedAndHoldsNothingUntilTheShelvesExist()

    git grep -n 'AnAddressThisSurfaceDoesNotRecogniseIsAnsweredWithNoTotalRatherThanZero' -- Jellyfin.Plugin.Template.Tests/DiscoverSurfaceTests.cs
    Jellyfin.Plugin.Template.Tests/DiscoverSurfaceTests.cs:162:    public async Task AnAddressThisSurfaceDoesNotRecogniseIsAnsweredWithNoTotalRatherThanZero(string folder)

So what installing this gets you today is a page with nothing on it and nothing
saying why.

Nothing leaves the server for a metadata source. The types that would ask one
are in the tree, and the one file in the plugin outside the source directory that
names the interface takes one as a parameter rather than keeping one, so nothing
on a running server holds one. The only callers that build the adapter are in the
test project:

    git grep -n 'IMetadataSource' -- 'Jellyfin.Plugin.Template/*.cs' ':!Jellyfin.Plugin.Template/Sources/'
    Jellyfin.Plugin.Template/Catalogue/CatalogueRetention.cs:13:/// six months, which <see cref="IMetadataSource.RetentionCeiling"/> carries as a
    Jellyfin.Plugin.Template/Catalogue/CatalogueRetention.cs:62:    public static CatalogueRetention Of(TimeSpan duration, IReadOnlyCollection<IMetadataSource> activeSources)

    git grep -rln 'new TmdbSourceAdapter' -- '*.cs'
    Jellyfin.Plugin.Template.Tests/CatalogueRetentionTests.cs
    Jellyfin.Plugin.Template.Tests/SourceResponseFuzzTests.cs
    Jellyfin.Plugin.Template.Tests/TmdbSourceAdapterTests.cs

The catalogue is the same shape. A record, a directory and a document store
exist, the only place one is constructed is the suite, and installing this
plugin therefore puts no catalogue on a server's disk:

    git grep -rln 'new CatalogueDirectory\|new CatalogueDocumentStore' -- '*.cs'
    Jellyfin.Plugin.Template.Tests/AFreshInstallWritesNothingTests.cs
    Jellyfin.Plugin.Template.Tests/CatalogueDirectoryTests.cs
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentStoreTests.cs
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs

Read every sentence in the description above as a plan and not as behaviour you
can install. The note at the top of this page is the same kind of sentence and
needs the same reading. Nothing here has been tried with any client, and "any
Jellyfin server" is narrower than it sounds: the package declares a floor, and a
server below it does not load the plugin at all.

    git grep -n '^targetAbi' -- build.yaml
    build.yaml:10:targetAbi: "10.11.0.0"

The rest of what has been built is underneath the description rather than in
front of it: the gate this repository runs, the test project and the rules the
suite lives under, the plugin's own identity, the source adapter and the
catalogue types named above, and a configuration that carries a schema version
and no settings. The empty page is the only part of any of it a user meets.

The gate does not run on quite every change, and the exception is the one a
reader of this page is most likely to run into. Two workflows skip a change that
touches only Markdown, so a documentation change is judged by less than the whole
set:

    git grep -n -A1 'paths-ignore:' -- .github/workflows/
    .github/workflows/plugin-loads.yml:29:    paths-ignore:
    .github/workflows/plugin-loads.yml-30-      - "**/*.md"
    --
    .github/workflows/plugin-loads.yml:34:    paths-ignore:
    .github/workflows/plugin-loads.yml-35-      - "**/*.md"
    --
    .github/workflows/scan-codeql.yaml:18:    paths-ignore:
    .github/workflows/scan-codeql.yaml-19-      - "**/*.md"
    --
    .github/workflows/scan-codeql.yaml:22:    paths-ignore:
    .github/workflows/scan-codeql.yaml-23-      - "**/*.md"

The plan is the issue tracker. It is organised into milestones, each with an
issue that says what the milestone ends with, and the first one is
[M1](https://github.com/Flowfin/jellyfin-plugin-discover/milestone/1). Ten
questions in it were decisions rather than work; they are collected in
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) and were
answered there on 2026-08-24.

## Which server this builds against

Which lines this project will fix a bug in is a different question from which
line the tree builds against today, and the two have different answers right
now. The first is
[`docs/support.md`](docs/support.md), which states the supported lines, what
happens when one of them ends upstream, what happens when a metadata source ends
or stops answering a key, and how long a release is supported. Read that page
before planning an upgrade around this plugin. The rest of this section is the
second question only.

The tree no longer disagrees with itself about the second question.
`Directory.Build.props` is the one place a server line is stated and everything
else derives from it:

    git grep -nE '<Jellyfin(PackageVersion|TargetFramework|DeclaredLines)>' -- Directory.Build.props
    Directory.Build.props:40:        <JellyfinPackageVersion>10.11.11</JellyfinPackageVersion>
    Directory.Build.props:41:        <JellyfinTargetFramework>net9.0</JellyfinTargetFramework>
    Directory.Build.props:46:        <JellyfinDeclaredLines>10.11</JellyfinDeclaredLines>

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
checked against. Read that as the line the tree carries and not as the set of
lines that are supported: two are supported, and the difference between the two
sets is a row on [`docs/support.md`](docs/support.md).

## Building

    dotnet build Jellyfin.Plugin.Template.sln -c Release

The assembly lands under `Jellyfin.Plugin.Template/bin/Release/net9.0/`.

## Running it against a local server

There is no editor scaffolding in this repository. Every path in the template's
`.vscode` configuration was derived from one setting naming the template's
project, and the project directories and the solution still carry the template's
name until
[#14](https://github.com/Flowfin/jellyfin-plugin-discover/issues/14) renames
them, so those tasks would have had to be rewritten the moment they were. The
directory was removed rather than left building a solution that will not exist.
The steps it automated are these, and they are short enough to run by hand:

1. Build, as above.
2. Create a directory named after the plugin inside the server's plugin
   directory. That is `%LOCALAPPDATA%\jellyfin\plugins\` on Windows and
   `~/.local/share/jellyfin/plugins/` on Linux, unless the server was told to
   keep its data somewhere else.
3. Copy everything from the build output directory into it.
4. Restart the server, and read the server log for the plugin being loaded.

You do not have to take this on trust. A package built from this tree is
installed into a server container and the server's own log is read for the
plugin loading, by `.github/workflows/plugin-loads.yml`, which is
[#19](https://github.com/Flowfin/jellyfin-plugin-discover/issues/19) and is
closed. What that covers is the one line the tree declares, 10.11, because that
is the only one declared, and it does not run on a change that is only Markdown.

## Licence

This repository ships the GNU General Public License, version 3, in
[LICENSE](LICENSE), and that is the licence the plugin is under.

A Jellyfin plugin links against the Jellyfin binary NuGet packages, which are
themselves under the GPLv3. A compiled plugin is therefore GPLv3 whatever the
source licence says, which is worth knowing before anybody reuses this code
under something more permissive.

See [NOTICE.md](NOTICE.md) for the intended-use notice.
