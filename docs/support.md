# What is supported, and for how long

Which combinations of this plugin, a Jellyfin server and a metadata source I
will fix a bug in. Every one of the three can end without this project deciding
anything, so an operator planning an upgrade needs the answer in one place
rather than inferred from what happens to still build.

This page states a position. It is not a description of a mechanism, and where a
position has no mechanism behind it the text says so, because a support
statement a reader takes for an enforced guarantee is worse than none.

## The server lines

The lines this project supports are **10.11** and **12.0**. That is the answer
to question 1 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), taken on
2026-08-24, and the argument for it is there rather than repeated here. What
belongs here is what the answer costs an operator, which is the order: the 10.11
artefact ships first, and the 12.0 artefact follows once a stable 12.0 server
exists.

Supported and built are two different words on this page, and today they name
different sets. One line is declared in the tree, and it is the only one
anything is compiled, packaged or installed against:

    git grep -n '<JellyfinDeclaredLines>' -- Directory.Build.props
    Directory.Build.props:46:        <JellyfinDeclaredLines>10.11</JellyfinDeclaredLines>

    git grep -nE '^targetAbi|^version' -- build.yaml
    build.yaml:7:version: "0.1.0.0"
    build.yaml:10:targetAbi: "10.11.0.0"

So an operator running a 12.0 server gets nothing installable from this project
today, and the sentence "12.0 is supported" is a commitment rather than a
report. Compiling against a second line is
[#31](https://github.com/Flowfin/jellyfin-plugin-discover/issues/31), and it
carries a second package and a second published artefact behind it.

Read a line as carried once a release exists for it. No release exists for
either line yet, because this repository has never published one, which is
[#163](https://github.com/Flowfin/jellyfin-plugin-discover/issues/163).

| Server line | What exists for it today                                                   | What is owed before it is carried                                                                             |
| ----------- | -------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| 10.11       | The tree compiles, packages and boots a server against it on every change. | A published release, which is [#163](https://github.com/Flowfin/jellyfin-plugin-discover/issues/163).         |
| 12.0        | Nothing. No compile, no package, no server booted with this plugin on it.  | The build, which is [#31](https://github.com/Flowfin/jellyfin-plugin-discover/issues/31), and then a release. |

## When a server line ends upstream

Support here ends when the line ends upstream, and it ends by this project
stopping rather than by anything being withdrawn.

- No release after that point declares the ended line, and no bug is fixed
  against it.
- The last release that did declare it stays where it was published. The publish
  route does not edit or delete a release it created, and under immutable
  releases it could not, so an operator who already holds that archive keeps
  both a working install and an installable file.
- Whether the plugin manifest goes on serving that version to a dashboard is a
  property of the manifest rather than of the archive, and it is not decided
  here. That is
  [#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120).

Nothing in this tree watches an upstream end-of-life date, so the first of those
three is something I do rather than something that happens.

## When a source ends, changes its terms, or stops answering a key

The plugin has one metadata source and the operator supplies its credential.
That is the answer to question 4 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), and it
decides most of this section: a credential that stops working is one install's
problem rather than every install's, and no revocation reaches past the operator
who registered it.

    git ls-tree -r --name-only origin/master -- docs/sources/
    docs/sources/README.md
    docs/sources/thetvdb.md
    docs/sources/tmdb.md

The three cases and what each one gets:

- **A key the source no longer answers.** That install stops fetching and keeps
  whatever the catalogue already holds until its retention runs out. No release
  is cut for it and nothing is supported back into it; the operator registers a
  key again. What the plugin tells them while it is in that state is
  [#63](https://github.com/Flowfin/jellyfin-plugin-discover/issues/63).
- **Terms that change so that this plugin may no longer use the source.** The
  reading of a source's terms lives on its page under `docs/sources/`, and a
  change there is a change to what the code is allowed to do. I would ship a
  release without that source rather than one that goes on calling it, and the
  shelves naming it stop.
- **An API that ends.** The same ending, reached without anybody's permission
  changing.

A plugin with no working source is not a broken install. It is an install whose
shelves are empty, which is a state this plugin is required to be honest about
rather than one it is required to avoid.

What this project will not do is keep a source alive by shipping a credential of
its own. That was decided in the other direction on question 4, and this page
does not reopen it.

## How long a release is supported

While the first number of the version is `0`, nothing is promised at all.
`CHANGELOG.md` states that and it governs this page rather than the other way
round: settings, stored data and the seam to a sibling plugin may all move in
any release, and an upgrade may leave data behind that has to be removed by
hand.

After that, one release is supported at a time and it is the most recent one.
There are no maintenance branches and nothing is backported. A fix arrives in
the next release or it does not arrive.

Downgrading is unsupported in either era, and what it costs is
[`what-a-downgrade-does.md`](what-a-downgrade-does.md).

## The beta channel

A beta channel has no support. Nothing installed from one is fixed on request,
nothing installed from one is guaranteed to upgrade cleanly into a stable
release, and an operator who installs from one is doing me a favour rather than
receiving a service.

That is stated in advance, because no beta channel exists yet and the position
is cheaper to fix now than to argue about with the first person on it. Nothing
published from here is marked as a pre-release today:

    git grep -n 'prerelease:' -- .github/workflows/publish.yaml
    .github/workflows/publish.yaml:537:          prerelease: false

Building the channel, and making a beta build tellable from a stable one, is
[#121](https://github.com/Flowfin/jellyfin-plugin-discover/issues/121). This
page is the definition of unsupported that issue's first condition points at.

## What is not on this page

Which clients this plugin has been seen working in. Nothing here has been tried
with any client, and the matrix that records what was actually observed is
[#115](https://github.com/Flowfin/jellyfin-plugin-discover/issues/115). A
statement about supported servers is not a statement about clients, and reading
it as one is the mistake this paragraph exists against.

Whether this plugin works beside another plugin. No set of supported siblings is
declared anywhere in this tree, which [`RELEASING.md`](RELEASING.md) says at the
condition that would need one.

What the plugin cannot make good, which is [`limits.md`](limits.md) rather than
this page. A limit and a support position are different claims: one says what
happens, the other says what I will do about it.

Nothing derives any of this. No route compares this page against the tree,
against the tracker or against an upstream calendar, so a line that ends
upstream tomorrow does not appear here by itself and nothing goes red for its
absence.
