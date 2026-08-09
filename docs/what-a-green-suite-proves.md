# What a green suite proves

A green `dotnet test` here answers a narrow question and is easy to read as an
answer to a wider one. This page says what the run establishes, what it does
not, and where each thing it does not cover is held instead.

It is the neighbour of
[`HEADLESS.md`](../Jellyfin.Plugin.Template.Tests/HEADLESS.md), which is about
the tests that were refused and what replaces them. This page is about the tests
that are here.

## What is measured

The gate restores, builds and tests, in that order and in Release, and a working
tree runs the same three commands:

    dotnet restore
    dotnet build --configuration Release --no-restore
    dotnet test --configuration Release --no-build

Coverage is collected by that third command rather than by a command of its own.
A figure produced only by a step nobody runs is a figure nobody has, so the table
is printed by the ordinary run, on the gate and on a working tree alike, and the
number a reader quotes is the number a run printed.

One module is measured, and which one is stated in the test project rather than
here, because a module named in two places is two names the day one is renamed:

    git grep -n '<Include>' -- Jellyfin.Plugin.Template.Tests/Jellyfin.Plugin.Template.Tests.csproj
    Jellyfin.Plugin.Template.Tests/Jellyfin.Plugin.Template.Tests.csproj:62:    <Include>[Jellyfin.Plugin.Template]*</Include>

The test assembly is not in that set on purpose. Coverage of the tests is a
statement about the tests, and a figure averaging the two moves whenever a test
is added and says nothing about the code.

No figure is written on this page. Coverage moves with every commit, and a
percentage in a document is a claim about a tree that has since changed. Run the
command and read the table it prints.

## No threshold, and why that is a decision

Nothing here fails a run for a low coverage figure. That is deliberate rather
than unfinished.

A threshold is met by tests that execute a line and assert nothing about it. It
buys the number and not the property the number is a proxy for, and once it is
set, the cheapest way past a red run is the test that asserts nothing. So the
figure is reported and left to a reader.

The signal a threshold is a bad proxy for is whether a test would have failed had
the line been wrong, and that is measured by mutation testing rather than by
coverage. It is
[#36](https://github.com/Flowfin/jellyfin-plugin-discover/issues/36), it is
scoped to the code where a wrong answer is invisible, and it is reported rather
than enforced for the same reason this is.

    git grep -n 'Threshold' -- Jellyfin.Plugin.Template.Tests/Jellyfin.Plugin.Template.Tests.csproj
    Jellyfin.Plugin.Template.Tests/Jellyfin.Plugin.Template.Tests.csproj:64:      No Threshold is set, and that is a decision rather than an omission. A

## What a green run establishes

Coverage says which lines ran. It does not say that a test would have failed had
one of them been wrong, and it says nothing at all about a line nobody wrote.
What the run does establish is narrower than the figure suggests:

- That the tracked source compiles under this repository's analyzer settings,
  with warnings treated as errors, against the server line
  `Directory.Build.props` declares.
- That the assertions in the test project held for the inputs those tests
  supplied. Where an input is a fixture, that is a statement about the fixture.
- That the tests ran with no display, no elevation, no machine trust store and no
  container. `HEADLESS.md` is where that is argued and where the commands
  showing it are.

Everything else a reader might take from a green run is in the next two sections.

## What it does not establish, and where each is held

Three risks in this plan are outside any automated test here by construction.
None of them is a gap in the suite; each is real, and each is held somewhere that
is not a test.

**What a given client draws.** The claim the plan rests on is that every client
browses a discover page with no client change. The server side of that is
checkable over HTTP and is
[#38](https://github.com/Flowfin/jellyfin-plugin-discover/issues/38). What a
client then draws needs the client, a screen and a person looking at it. It is
held by the matrix in
[#115](https://github.com/Flowfin/jellyfin-plugin-discover/issues/115), which
lists a client as untried rather than inferring it.

**Whether a source's terms are met in spirit as well as in code.** Each source's
terms are turned into obligations on behaviour in `docs/sources/`, one row per
clause. The `source-terms` check holds that a page exists, that every hostname in
the tracked C# is declared, and that a page says when its terms were read. Its
own header says what it cannot do, and the first thing on that list is that no
leg reads the prose of a page. Whether the reading is right is a judgement a
person makes against the terms themselves, which every page links. That judgement
is a review, and this suite is not it.

**How the plugin behaves on a large library.** A refresh that does a library
lookup per title, an item count added to a library database, and a shelf
difference over a catalogue are all cheap on the libraries a test builds and
none of them has been run against a large one. What the catalogue costs on disk
and in the database is
[#71](https://github.com/Flowfin/jellyfin-plugin-discover/issues/71). How long
the work takes and what it does to a server while it runs is
[#194](https://github.com/Flowfin/jellyfin-plugin-discover/issues/194), which
was opened out of this page because nothing in the plan held it.

## What is not measured at all

Coverage is measured over the plugin assembly only, so the following are outside
the figure rather than at zero in it, which is a difference a percentage cannot
show.

- The workflows. What the gate refuses is held by the checks themselves and by
  the fixtures under `tools/invariants/`, not by this suite.
- The packaging metadata, except where a test reads it. `build.yaml` is data,
  and the comparisons against it live in the MSBuild files and in the test
  project rather than in a coverage figure.
- The configuration page as a page. Its bytes are read out of the built assembly
  and asserted; whether it is usable is
  [#115](https://github.com/Flowfin/jellyfin-plugin-discover/issues/115).
- Anything the plugin does not yet do. Most of this plan is unbuilt, and a high
  figure over a small assembly is a statement about a small assembly.
