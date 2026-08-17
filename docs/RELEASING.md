# Releasing

A release is published by pushing a tag. Nothing is created by hand.

## The tag

The tag has the form `X.Y.Z-stable` or `X.Y.Z.W-stable`, for example `1.4.0-stable`
or `0.1.0.0-stable`. The numeric part is the plugin version that Jellyfin installs,
and it must be exactly the `version` in `build.yaml`, written the same way, with the
same number of parts. The `-stable` suffix lives only in the tag and in the release
name.

## Cutting a release

1. Update `version` in `build.yaml` on the release branch, move the lines
   describing that version out of `## Unreleased` and under a `## 1.4.0` heading
   of its own in `CHANGELOG.md`, and merge both together.
2. Check that the commit you want to release is on that branch.
3. Check that the changelog names the version. The run checks this again and
   fails before anything is published, so this is the cheap place to find out:

   ```
   tools/changelog-names-the-version.sh 1.4.0 CHANGELOG.md
   ```

4. Push the tag for that commit:

   ```
   git tag 1.4.0-stable <commit>
   git push origin 1.4.0-stable
   ```

The `Publish Release` workflow takes it from there.

Push one tag at a time and wait for its run to finish. GitHub keeps at most one
queued run per concurrency group, and although the group here is keyed on the tag,
serialising them by hand is what keeps the release order readable.

## Interoperability, before the tag is pushed

A release is not cut while the interoperability matrix is red. The matrix is
[#126](https://github.com/Flowfin/jellyfin-plugin-discover/issues/126): a server of
each declared line booted twice, once with this plugin alone and once with the full
set of supported siblings installed together, both coming up without startup errors,
answering their routes, and passing a scan for collisions over routes, scheduled task
names and configuration keys.

A red matrix leaves two ways forward and neither of them is a tag. Either the
collision is fixed, or the incompatibility is written down as a known limitation with
the reason it was accepted, where somebody deciding whether to install reads it. The
page for that is
[`limits.md`](limits.md), owed by
[#114](https://github.com/Flowfin/jellyfin-plugin-discover/issues/114), and it is
written now, so a known limitation goes there as a row with a pointer to what
established it. `CHANGELOG.md` is the other half rather than the substitute it was
while that page was missing: it states what changed against a version, and the row
states what a reader deciding about a server meets. Pushing the tag with neither
ending is what this condition exists against, because the operator who then meets the
collision has no way to tell it was already known.

Nothing produces that verdict here today, and this condition must not be recorded as
met until something does. No set of supported siblings is declared anywhere in the
tree, so a matrix has nothing to install beside this plugin:

```
git grep -in 'supported plugin\|supported sibling\|plugin set' -- docs/ tools/ .github/ README.md
```

That prints three lines and exits 0, and all three are in this page: the two
sentences above and the command itself. The search matches its own text and nothing
else, so the absence it is quoted for still holds, and a reader who runs it should
read the three matches as this paragraph rather than as a declared set.

The sentence that stood here said it printed nothing and exited 1. That was already
untrue when it landed, because the change that wrote the sentence is the change that
created the three matches, and what it described is the tree one commit earlier.

What does run is narrower and is a different claim.
`plugin-loads.yml` unpacks the package a release would ship into a server of each
declared line and reads the server's own log, with nothing else in the plugin
directory. That is the alone half of the rule, on every push and every pull request,
and it says nothing about the together half.

## What the run produces

The workflow builds the plugin from the tagged commit, creates the GitHub release
for the tag, and attaches four files:

- the plugin archive
- the packaging metadata written beside it, `<archive>.zip.meta.json`
- one `.md5` file, the checksum of the archive
- one `.sha256` file for the same archive

The release notes are not written by hand and are not taken from `CHANGELOG.md`.
The run asks GitHub to compose them, which it does from the commits and merged
pull requests between the previous release and this tag, so what a reader sees
under a release is the merge history of that range. `CHANGELOG.md` stays the
place a version's changes are stated in this project's own words.

The two are compared, in one direction and by name only. The run refuses a
version `CHANGELOG.md` does not carry a heading for, and it refuses a heading
with nothing under it. What those lines say is not judged by anything, so a tag
whose changelog entry describes the wrong change still publishes.

That matters more than it looks, because a release the workflow created is not
edited by this route afterwards, and under immutable releases it cannot be edited
at all. Notes that have to read a particular way have to come out of the commit
subjects in the range.

The `.md5` is the value a Jellyfin catalog serves as the plugin checksum. There is
exactly one per release so that no generator can pair a checksum with the wrong
file. Both the archive and the metadata are checked for existence by name before the
release job runs, so a release with three of the four files is not a state this route
can reach.

The run also signs a build provenance statement for the archive, in a separate job
that downloads the archive and runs no build tooling. A downloaded archive can be
checked against it:

```
gh attestation verify <archive>.zip --repo <owner>/<repository>
```

Nothing here writes a plugin catalog. A GitHub release is the whole output. If this
repository previously published through the Jellyfin meta plugins workflow, that path
is gone and no catalog is fed until a manifest generator is added.

## What fails the run

- The tag does not end in `-stable`, or the workflow was started from something
  other than a tag.
- The numeric part of the tag differs from `version` in `build.yaml`.
- `build.yaml` is missing a required field, or `version`, `targetAbi`, `framework`
  or `guid` has the wrong shape.
- `framework` in `build.yaml` names a target the plugin project is not built for.
- A packaging manifest that shadows `build.yaml` is present, such as `jprm.yaml` or
  `meta.yaml`.
- `build.yaml` declares an `image` file that is not in the repository.
- The tagged commit is not contained in a release branch, or the tag was moved after
  the run started.
- There is no `packages.lock.json` next to the plugin project, so the release build
  cannot restore against a reviewed dependency graph. Create one with
  `dotnet restore <project> -p:RestorePackagesWithLockFile=true` and commit it.
- `CHANGELOG.md` carries no heading naming the version being released, or the
  heading is there with nothing under it.
- The version stamped into the assembly is not the version in `build.yaml`.
- The build produced no archive, or more than one, or no packaging metadata.
- A release already exists for the tag.

All of these fail before anything is published.

## What the run notes without failing

The packaging tool warns when `build.yaml` declares neither `image` nor `imageUrl`.
The plugin then shows without a logo in a catalog. That is a warning on every run
until a logo exists, and it is not a reason to hold a release.

## Re-running

A release that exists is not touched again. The release job asks whether a release
exists for the tag before it writes anything and stops if one does, and the upload
step is configured not to replace an asset of the same name. Replacing the bytes of a
version people have already installed is the failure this prevents, and it is worth
more than the convenience of a re-run.

So: if a release went out with the wrong contents, fix the problem, raise the version
in `build.yaml`, and push a new tag.

If a run failed **before** the release was created, the tag is still clean. Fix the
cause and re-run the workflow from the Actions page, or delete and re-push the tag.

If a run failed **after** the release was created but before every asset was attached,
the release is incomplete and a re-run will refuse it. What is possible then depends
on the repository settings below. Without immutable releases you can delete the
incomplete release, delete the tag, and push it again. With immutable releases you
cannot, and the version has to be raised.

## Repository settings this expects

- Default workflow permissions set to read only.
- A rule that restricts who may push `*-stable` tags.
- The `ABI floor build` check required on the release branches.
- Immutable releases, if the repository wants the guarantee that a published release
  can never be edited or deleted at all. The workflow does not depend on it: the
  refusal to touch an existing release is enforced in the release job. Turning it on
  removes the only recovery path for an incomplete release, so try it on one
  repository and cut a release there before turning it on everywhere.
