> [!NOTE]
>
> **Part of [Flowfin](https://github.com/Flowfin).** It works with any Jellyfin
> server, and with the Flowfin clients.

# jellyfin-plugin-discover

On a television this is one more tile in the library list, called Discover.
Opening it leads to shelves of films and series the server does not have: what
is trending, what is popular and what is highly rated, asked separately for
films and for series. They are drawn the way every other library on that screen
is drawn, by every existing client and with no client change, because a shelf
here is an ordinary browsable page rather than a new kind of one. A title inside
opens to its artwork, its year and its description, and nothing on any of those
shelves plays. What a user does with one instead is say they want it, and where
a companion requests plugin is installed beside this one, that is handed over to
it.

The name on that tile and the sentence a client draws under it are fixed in the
tree rather than described here:

    git grep -h 'Name = "Discover"' -- Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs
            Name = "Discover",

    git grep -h 'Summary = "Films' -- Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs
            Summary = "Films and series this server does not have, so you can see what is out there. Nothing here is playable. " + SourceNotice.Tmdb,

The sentence about every existing client is the one this repository cannot check
for itself. What a client draws from what the server sent needs the client, a
screen and somebody looking at one, which no run here can be. So which clients
were tried, on which version, and what each of them drew is
[#115](https://github.com/Flowfin/jellyfin-plugin-discover/issues/115), and that
issue is what this page points at instead of naming a client. No client has been
tried yet, so it carries no rows and this page claims nothing about any of them.

## What this plugin does not do

It does not acquire anything. The shelves are a catalogue of titles this server
has not got rather than a queue of titles it is getting, and pressing play on
one gets an answer no client can draw as a message. That is a limit rather than
a feature and it is on [`docs/limits.md`](docs/limits.md) with the reading
behind it.

It does not ask anybody for a title either, on a server where it is the only
thing installed. Where a want goes is a handover to a sibling plugin, one way
and at one moment, and the whole of what crosses is
[0004](docs/decisions/0004-what-crosses-the-seam-to-a-requests-plugin.md), which
is [#94](https://github.com/Flowfin/jellyfin-plugin-discover/issues/94). That
sibling exists, at
[Flowfin/jellyfin-plugin-requests](https://github.com/Flowfin/jellyfin-plugin-requests),
it has published releases, and its own seam page names #94 as the contract and
writes no second one.

What this side offers it is an extension point rather than a route it goes
looking for. This plugin declares the interface and takes whatever the server's
container holds for it; it puts nothing there itself, and nothing at all is a
complete state rather than a degraded one:

    git grep -n 'IWantReceiver' -- Jellyfin.Plugin.Template/PluginServiceRegistrator.cs
    Jellyfin.Plugin.Template/PluginServiceRegistrator.cs:72:        // Nothing registers an IWantReceiver here, and that is the point rather

So on a server where nothing implements it a want has nowhere to go but a list
this plugin keeps for the operator to read, and that list is
[#97](https://github.com/Flowfin/jellyfin-plugin-discover/issues/97) and is not
built either. Whether the released sibling implements this interface has not
been read here, and this page claims nothing either way. Somebody installing
this and expecting the other half meets a seam rather than a bug, and that is
the paragraph on this page most worth reading twice.

It classifies nobody and nothing, and that part is not a plan. The surface
declares one audience for the whole of itself and answers yes to every user it
is asked about:

    git grep -h 'Audience = SurfaceAudience' -- Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs
            Audience = SurfaceAudience.General

    git grep -h 'public bool IsAvailableTo' -- Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs
        public bool IsAvailableTo(Guid userId) => true;

Who may see the surface at all is
[#57](https://github.com/Flowfin/jellyfin-plugin-discover/issues/57) and what
may be fetched into it is
[#93](https://github.com/Flowfin/jellyfin-plugin-discover/issues/93). What makes
the permissive answer above cost nothing today is that there is nothing behind
the surface, which the status section is about, and it stops costing nothing on
the day the catalogue holds a title.

## Status

Nothing the opening description promises works yet, and one part of it now
appears without working. A discover page shows up in a client, because the surface is registered
with the server:

    git grep -n 'AddSingleton<IChannel' -- Jellyfin.Plugin.Template/PluginServiceRegistrator.cs
    Jellyfin.Plugin.Template/PluginServiceRegistrator.cs:64:        serviceCollection.AddSingleton<IChannel, DiscoverSurfaceAdapter>();

Every level of that page is empty, the top of it included. What the top level and
any other address answer is no longer the same thing: the top is a level the
surface recognises and which holds nothing, and every other address is one it does
not recognise, which a client can tell apart by the total. Tests hold both rather
than the sentence resting on a reading of the source:

    git grep -n 'IsRoot ? SurfaceListing.EmptyLevel' -- Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs
    Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs:166:            asked.Parent.IsRoot ? SurfaceListing.EmptyLevel : SurfaceListing.NoSuchLevel);

    git grep -n 'TheTopLevelIsRecognisedAndHoldsNothingUntilTheShelvesExist' -- Jellyfin.Plugin.Template.Tests/DiscoverSurfaceTests.cs
    Jellyfin.Plugin.Template.Tests/DiscoverSurfaceTests.cs:160:    public async Task TheTopLevelIsRecognisedAndHoldsNothingUntilTheShelvesExist()

    git grep -n 'AnAddressThisSurfaceDoesNotRecogniseIsAnsweredWithNoTotalRatherThanZero' -- Jellyfin.Plugin.Template.Tests/DiscoverSurfaceTests.cs
    Jellyfin.Plugin.Template.Tests/DiscoverSurfaceTests.cs:187:    public async Task AnAddressThisSurfaceDoesNotRecogniseIsAnsweredWithNoTotalRatherThanZero(string folder)

So what installing this gets you today is a page with nothing on it and nothing
saying why.

Nothing leaves the server for a metadata source. The types that would ask one
are in the tree, and the two files in the plugin outside the source directory
that name the interface take one as a parameter rather than keeping one, so
nothing on a running server holds one. The only callers that build the adapter
are in the test project:

    git grep -n 'IMetadataSource' -- 'Jellyfin.Plugin.Template/*.cs' ':!Jellyfin.Plugin.Template/Sources/'
    Jellyfin.Plugin.Template/Catalogue/CatalogueRetention.cs:13:/// six months, which <see cref="IMetadataSource.RetentionCeiling"/> carries as a
    Jellyfin.Plugin.Template/Catalogue/CatalogueRetention.cs:62:    public static CatalogueRetention Of(TimeSpan duration, IReadOnlyCollection<IMetadataSource> activeSources)
    Jellyfin.Plugin.Template/Shelves/Shelf.cs:194:    /// <see cref="IMetadataSource"/> asks that, and finding out means issuing a
    Jellyfin.Plugin.Template/Shelves/Shelf.cs:253:    public Shelf ValidatedAgainst(IReadOnlyCollection<IMetadataSource> activeSources)

    git grep -rln 'new TmdbSourceAdapter' -- '*.cs'
    Jellyfin.Plugin.Template.Tests/CatalogueRetentionTests.cs
    Jellyfin.Plugin.Template.Tests/ShelfTests.cs
    Jellyfin.Plugin.Template.Tests/SourceResponseFuzzTests.cs
    Jellyfin.Plugin.Template.Tests/TmdbSourceAdapterTests.cs

The catalogue is the same shape. A record, a directory and a document store
exist, the only place one is constructed is the suite, and installing this
plugin therefore puts no catalogue on a server's disk:

    git grep -rln 'new CatalogueDirectory\|new CatalogueDocumentStore' -- '*.cs'
    Jellyfin.Plugin.Template.Tests/AFreshInstallWritesNothingTests.cs
    Jellyfin.Plugin.Template.Tests/CatalogueDirectoryTests.cs
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentBodyTests.cs
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentStoreTests.cs
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs
    Jellyfin.Plugin.Template.Tests/CatalogueLayoutTests.cs
    Jellyfin.Plugin.Template.Tests/WantListStoreTests.cs

Read every sentence above this section as a plan and not as behaviour you can
install. That covers what a television shows and what this plugin does not do,
both of which describe a configured plugin rather than an installed one, and the
second of the two is the half that is true early: a thing not built does none of
what it will not do. The note at the top of this page is the same kind of
sentence and needs the same reading, and the claim it makes about clients is
answered where the description's is rather than a second time here. "any Jellyfin server" is
narrower than it sounds: the package declares a floor, and a server below it does
not load the plugin at all.

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

## The source this plugin uses

Titles and artwork are meant to come from TMDB, and that source's API terms
require an application using it to display a notice. This is the notice, quoted
from the place the plugin holds it rather than typed again here:

    git grep -h 'This application uses TMDB' -- Jellyfin.Plugin.Template/Surface/SourceNotice.cs
            "This application uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.";

One home rather than a copy per rendering is the rule, and it holds for the
surface, which takes the constant. It does not hold for the configuration page,
which is a static asset with no substitution step and therefore carries a second
copy of the same bytes; what stands between the two is a test rather than a
construction, and that is written at the constant. This page is a third copy for
the same reason, and the command above is what keeps it the same bytes.

What the terms oblige and what was read to establish it is
[`docs/sources/tmdb.md`](docs/sources/tmdb.md), which is
[#76](https://github.com/Flowfin/jellyfin-plugin-discover/issues/76). What that
source's API offers, as opposed to what it requires, is a separate page for a
separate reason, [`docs/source-api/tmdb.md`](docs/source-api/tmdb.md).

Nothing in this tree has ever asked TMDB anything. The notice is displayed
because the terms ask it of an application built against the API, and the status
section above carries the command showing that nothing on a running server holds
a source at all.

## Licence

This repository ships the GNU General Public License, version 3, in
[LICENSE](LICENSE), and that is the licence the plugin is under.

A Jellyfin plugin links against the Jellyfin binary NuGet packages, which are
themselves under the GPLv3. A compiled plugin is therefore GPLv3 whatever the
source licence says, which is worth knowing before anybody reuses this code
under something more permissive.

See [NOTICE.md](NOTICE.md) for the intended-use notice.
