# The plugin catalogue, and what it asks of this repository

The official catalogue is the list a stock Jellyfin server offers under
Dashboard, Plugins, Catalogue. Getting into it is usually described as a
submission. It is not one, and that is the fact every row below turns on.

Everything here is read from the catalogue's own tooling rather than from
anybody's memory of it. Read from a clone of
<https://github.com/jellyfin/jellyfin-meta-plugins> at
`eb99033a7ff644881b014bc0b4169916c854a68b`, committed 2026-07-29 and fetched
2026-08-16. Line numbers are at that commit:

    git clone https://github.com/jellyfin/jellyfin-meta-plugins.git
    cd jellyfin-meta-plugins && git rev-parse HEAD
    eb99033a7ff644881b014bc0b4169916c854a68b

Two of the paths quoted below exist in that clone and in this repository, under
the same name and with different contents: `.github/workflows/publish.yaml` and
`build.yaml`. A bare path is therefore not enough to say which file a reading is
about, and the two readings fail differently in the wrong checkout. A `grep` for
something only the catalogue's file has comes back empty here and exits 1, which
reads as the guard being absent rather than as the wrong file. A `sed` line range
prints whatever this repository's file holds at those numbers and exits 0, which
announces nothing at all.

So every reading of the catalogue's copy is written against the commit id above
rather than against a bare path. That is not decoration. This repository does not
have that object, so the same command run in the wrong checkout stops rather than
answering, and it says which of the two mistakes was made:

    git show eb99033a7ff644881b014bc0b4169916c854a68b:.github/workflows/publish.yaml ; echo "exit=$?"
    fatal: path '.github/workflows/publish.yaml' exists on disk, but not in 'eb99033a7ff644881b014bc0b4169916c854a68b'
    exit=128

That output is from this repository rather than from the clone, and it is the one
reading on this page taken here on purpose.

The two readings that are deliberately about this repository's own files carry
`origin/master` instead, and they are the only two on this page that do.

## The catalogue enumerates an organisation

The tool that maintains the plugin list walks one page of the forge API and takes
what it finds there:

    grep -n 'PAGINATION_URL = ' update_submodules.py
    43:PAGINATION_URL = "https://api.github.com/orgs/jellyfin/repos?sort=created&per_page={per}&page={page}"

    grep -n 'startswith("jellyfin-plugin-")' update_submodules.py
    59:        if _name.startswith("jellyfin-plugin-"):
    75:    if not repo.startswith("jellyfin-plugin-"):

Everything in the `jellyfin` organisation whose name begins with
`jellyfin-plugin-` is in, and nothing else can be, because nothing else appears
on that page. The second of those two lines is the other direction: a directory
that is no longer in the enumeration is removed.

So there is no list to be added to and no gate to pass. There is an organisation
to be inside, and being inside it is a question about who owns this repository
rather than about anything in this tree.

## The requirements, one at a time

Each is a tick, a refusal with its reason, or a pointer to the issue that decided
it.

### The repository name begins with `jellyfin-plugin-`

Met. The name is `jellyfin-plugin-discover`, from line 59 above.

### The repository carries a branch named `master`

Met. The submodule is added on a fixed branch:

    grep -n 'submodule", "add"' update_submodules.py
    24:        subprocess.run(["git", "submodule", "add", "--force", "-b", "master", url, _name], check=True)

and this repository's default branch is that one:

    gh repo view Flowfin/jellyfin-plugin-discover --json defaultBranchRef --jq '.defaultBranchRef.name'
    master

### The repository carries a licence the catalogue can read

Met. The forge reports one for this repository:

    gh api repos/Flowfin/jellyfin-plugin-discover --jq '.license.spdx_id'
    GPL-3.0

which is the same identifier the plugins already in the catalogue carry, read
from one of them rather than assumed:

    gh api repos/jellyfin/jellyfin-plugin-tvdb --jq '.license.spdx_id'
    GPL-3.0

### The repository carries packaging metadata the build tool can read

Met, as far as this can be checked without running that tool. The build the
catalogue runs reads the version out of `build.yaml`:

    grep -n 'meta_version=' build_plugin.sh
    53:meta_version=$(grep -Po '^ *version: * "*\K[^"$]+' "${PLUGIN}/build.yaml")

and this repository declares that field, with the rest of what a package needs:

    git grep -n '^name:\|^guid:\|^version:\|^targetAbi:\|^framework:\|^artifacts:' origin/master -- build.yaml
    origin/master:build.yaml:2:name: "Discover"
    origin/master:build.yaml:3:guid: "8227de33-0101-48a3-951d-2bf921709e48"
    origin/master:build.yaml:7:version: "0.0.0.0"
    origin/master:build.yaml:10:targetAbi: "10.11.0.0"
    origin/master:build.yaml:11:framework: "net9.0"
    origin/master:build.yaml:21:artifacts:

### The repository is inside the `jellyfin` organisation

Not met, and not a thing that can be met by changing a file here. This repository
is under `Flowfin`, which is a decision already recorded, and whether it moves was
question 9 on [#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2).
That was answered on 2026-08-24 and the answer is that it does not move for the
first release, so this row is refused rather than open. What the answer puts in
its place is under [What question 9 settled](#what-question-9-settled-and-what-it-left)
below.

The consequence is larger than a listing. The publishing route is a reusable
workflow, and three of its steps, including the whole publishing job, are guarded
on the owner:

    git show eb99033a7ff644881b014bc0b4169916c854a68b:.github/workflows/publish.yaml | grep -n "contains(github.repository, 'jellyfin/')"
    82:        if: ${{ contains(github.repository, 'jellyfin/') }}
    93:        if: ${{ contains(github.repository, 'jellyfin/') }}
    106:    if: ${{ contains(github.repository, 'jellyfin/') }}

What those guard is an upload to a host and a manifest edit over ssh:

    git show eb99033a7ff644881b014bc0b4169916c854a68b:.github/workflows/publish.yaml | sed -n '107,109p'
        env:
          JELLYFIN_REPO: "/srv/repository/main/plugin/manifest.json"
          JELLYFIN_REPO_URL: "https://repo.jellyfin.org/files/plugin/"

with credentials the calling repository has to supply:

    git show eb99033a7ff644881b014bc0b4169916c854a68b:.github/workflows/publish.yaml | sed -n '21,27p'
        secrets:
          deploy-host:
            required: true
          deploy-user:
            required: true
          deploy-key:
            required: true

A repository outside the organisation that called that workflow today would build
a package, attach it to its own release, and publish nothing to the catalogue,
with every job green. That is the failure
[#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120) is
designed against, arriving from a route nobody here wrote, and it is worth
knowing before anybody reaches for the workflow because it is convenient.

### The published version is the one `build.yaml` declares

Refused, and the refusal is a collision rather than a preference. The catalogue's
build script keeps the first segment of the declared version and replaces the
other three with a stamp of when the build ran:

    sed -n '51p;54p' build_plugin.sh
    VERSION_SUFFIX=${VERSION_SUFFIX:-$(date -u +%y%m.%d%H.%M%S)}
    VERSION=${VERSION:-$(echo $meta_version | sed 's/\.[0-9]*\.[0-9]*\.[0-9]*$/.'"$VERSION_SUFFIX"'/')}

This repository's own release route asserts the opposite, that what is published
is exactly what `build.yaml` declares, and refuses the tag otherwise:

    git grep -n 'carries version ${numeric} but build.yaml declares' origin/master -- .github/workflows/publish.yaml
    origin/master:.github/workflows/publish.yaml:179:            echo "::error::Tag ${tag} carries version ${numeric} but build.yaml declares ${version}. Bump build.yaml, or tag the version that is in it."

Both are defensible and they are not the same scheme. If this repository is ever
inside the catalogue, the same bytes carry one version string installed from the
catalogue and a different one installed from a release here, and an operator
comparing the two has no way to tell they are the same build. Whichever way that
is settled, it is settled where the release process is decided, which is
[#119](https://github.com/Flowfin/jellyfin-plugin-discover/issues/119), rather
than by whoever notices the mismatch first.

## What question 9 settled, and what it left

This heading said the question was not decided here and left it at that. It is
decided now, and where it is decided is still not here: question 9 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) was answered
on 2026-08-24.

What was being asked. Under the reading above, whether to be in the catalogue at
all is not about sending anything: it is about whether this repository moves
under another owner, which changes who holds the publishing credentials, who
reviews the code, and what the install path in
[#111](https://github.com/Flowfin/jellyfin-plugin-discover/issues/111) points at.

What was answered. It does not move for the first release. The manifest is
self-hosted under this repository's control and enters the Flowfin catalogue at
`flowfin.dev/manifest.json`, which is
[#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120), and
submission to the official Jellyfin catalogue is a later and additive step, which
is [#122](https://github.com/Flowfin/jellyfin-plugin-discover/issues/122).

What the answer does not touch is every other refusal on this page. Each one is a
property of this repository against the catalogue's own requirements, and a
decision about where the manifest is hosted moves none of them. The
version-string collision under the heading before this one is the one to read
carefully: it is not settled by the answer either, both schemes stay defensible
and stay different, and the release process that picks one is
[#119](https://github.com/Flowfin/jellyfin-plugin-discover/issues/119) rather
than this page.

What the answer does not make true is that this repository is listed anywhere. It
names the address a manifest will be published at. Publishing one is
[#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120) and has
not happened, and a reader who takes the address for an install path is reading a
decision as an artefact.

## Bounds

Everything above is read from files in that repository at the commit named, and
from this repository and the forge. I ran nothing in the catalogue's tooling,
submitted nothing, and observed no publish. Whether the tooling described is the
whole of the route that project actually uses is not something the files can say,
and no page there states the requirements in prose: its README describes the
build scripts and nothing else.

A route in this tree reads part of this page, and this paragraph said none of it
was read. `documented-commands` re-runs every command a tracked page pastes and
compares the answer against the output pasted under it, on every push and every
pull request, so every block above is one of its subjects.

The half this page exists to track is not among the ones it can compare, and the
reader prints its refusals rather than passing over them. Every block quoting the
catalogue's own tooling cites a commit this checkout does not carry, and nothing
here fetches it, so each is refused by name:

    git grep -n 'names an object this repository does not carry' -- tools/documented-commands/run.sh
    tools/documented-commands/run.sh:173:    echo "names an object this repository does not carry"

So those go stale silently against a repository that moves on its own schedule,
exactly as this paragraph said, and what catches them is still somebody running
them against a checkout of that repository.

The blocks that do read this repository are the end of the comparison that moves
when a change here edits the packaging metadata or the publish route. They quote
`origin/master`, so they are compared on the mainline and on the push that merges
to it, and on a pull request the reader reports that it judged none of them rather
than saying they passed:

    git grep -n 'reads origin/master and this checkout is not standing on it' -- tools/documented-commands/run.sh
    tools/documented-commands/run.sh:156:        echo "reads origin/master and this checkout is not standing on it"

Nothing reads the prose on either half. What a block holds is that the command
still prints what is pasted under it, never that the sentence over it is the right
thing to conclude from those bytes.
