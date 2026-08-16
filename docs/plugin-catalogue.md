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
is under `Flowfin`, which is a decision already recorded, and whether it moves is
question 9 on [#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2).

The consequence is larger than a listing. The publishing route is a reusable
workflow, and three of its steps, including the whole publishing job, are guarded
on the owner:

    grep -n "contains(github.repository, 'jellyfin/')" .github/workflows/publish.yaml
    82:        if: ${{ contains(github.repository, 'jellyfin/') }}
    93:        if: ${{ contains(github.repository, 'jellyfin/') }}
    106:    if: ${{ contains(github.repository, 'jellyfin/') }}

What those guard is an upload to a host and a manifest edit over ssh:

    sed -n '107,109p' .github/workflows/publish.yaml
        env:
          JELLYFIN_REPO: "/srv/repository/main/plugin/manifest.json"
          JELLYFIN_REPO_URL: "https://repo.jellyfin.org/files/plugin/"

with credentials the calling repository has to supply:

    sed -n '21,27p' .github/workflows/publish.yaml
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

## What is not decided here

Whether to be in the catalogue at all. Under the reading above that question is
not about sending anything: it is about whether this repository moves under
another owner, which changes who holds the publishing credentials, who reviews
the code, and what the install path in
[#111](https://github.com/Flowfin/jellyfin-plugin-discover/issues/111) points at.
It is question 9 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), and
[#119](https://github.com/Flowfin/jellyfin-plugin-discover/issues/119) and
[#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120) sit on the
same answer.

## Bounds

Everything above is read from files in that repository at the commit named, and
from this repository and the forge. I ran nothing in the catalogue's tooling,
submitted nothing, and observed no publish. Whether the tooling described is the
whole of the route that project actually uses is not something the files can say,
and no page there states the requirements in prose: its README describes the
build scripts and nothing else.

Nothing in this tree reads this page, so it goes stale silently against a
repository that moves on its own schedule. What catches that is somebody running
the commands on it.
