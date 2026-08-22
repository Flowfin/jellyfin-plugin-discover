# Changelog

## How a version works here

A version is four numbers, because that is the shape the plugin manifest and the
assembly attributes both take. It is stated once, as `PluginVersion` in
`Directory.Build.props`, and the three assembly properties derive from it.
`build.yaml` has to repeat it, because the plugin repository manager reads that
file and cannot read an MSBuild property, so the build compares the two and
fails when they differ. A server shows the version it read from the package, so
two sources that disagree means the dashboard names a build a bug report cannot
be tied to.

While the first number is 0 nothing is promised. Settings, whatever the plugin
stores, and the seam to a requests plugin may all change in any release, and an
upgrade may leave data behind that has to be removed by hand.

A release is breaking when a working install needs somebody to do something by
hand to keep working. A setting that is read differently, stored data an older
or newer build refuses, and a change to the contract a sibling plugin talks to
are all breaking. New behaviour that an existing install ignores is not.

A release that moves the identity of the items this plugin created is breaking
as well. That widens the sentence above rather than following from it, and the
widening is deliberate: such a release asks nobody to do anything, and the
titles come back at the next refresh on their own. What changes is the rows they
come back as. A favourite or a played mark a user set sits on the row that was
there before, and when that row goes the mark is detached rather than deleted,
with nothing scheduled to delete it afterwards on a default install. Whether it
reattaches to the new row is not established. So what such a release costs an
operator is stated as the marks surviving the removal and their destination
being an open question, and no further than that. It is named here because it is
the one thing in this plugin that an operator cannot act on before taking the
upgrade and cannot undo after it.

Which changes move an identity is not written out here, because a second list
drifts against the first and the version a reader meets would be the stale one.
[docs/title-identity.md](docs/title-identity.md) is the page that decides it, and
it carries what an identity is made of, what a rename of the surface costs, and
what moves one without anybody choosing to. A release doing any of them says so
under its own heading.

There is no beta suffix, because a four-number version has nowhere to put one,
and nothing published from here is marked as a pre-release either.
`.github/workflows/publish.yaml` runs on a tag ending in `-stable` and on no
other, and it creates every release with that flag off:

    git grep -nE '^      - "\[0-9\]|prerelease:' -- .github/workflows/publish.yaml
    .github/workflows/publish.yaml:17:      - "[0-9]+.[0-9]+.[0-9]+-stable"
    .github/workflows/publish.yaml:18:      - "[0-9]+.[0-9]+.[0-9]+.[0-9]+-stable"
    .github/workflows/publish.yaml:492:          prerelease: false

So a beta build is not told apart from a stable one by its version, and it is not
told apart by the release either. Which channel a pre-release would reach, what
would distinguish it, and what happens when publishing fails, is
[#121](https://github.com/Flowfin/jellyfin-plugin-discover/issues/121).

This paragraph said a pre-release is a GitHub release marked as a prerelease
which `.github/workflows/publish.yaml` passes on as `is-unstable`. That input is
declared by the workflow in `jellyfin/jellyfin-meta-plugins` which this file
called until
[#163](https://github.com/Flowfin/jellyfin-plugin-discover/issues/163) replaced
the publish path with one of this repository's own:

    gh api "repos/jellyfin/jellyfin-meta-plugins/contents/.github/workflows/publish.yaml?ref=eb99033a7ff644881b014bc0b4169916c854a68b" --jq .content | base64 -d | grep -n 'is-unstable'
    7:      is-unstable:

    git grep -n 'is-unstable' -- .github/ ; echo "exit=$?"
    exit=1

The name outlived the call, so the sentence described a route that had been
removed while naming the file that replaced it.

Every change that bumps the version adds its line under Unreleased first. The
`changelog-entry` check refuses a pull request that moves the version and leaves
this file alone.

At release time that line moves again, out of Unreleased and under a heading
naming the version, as `## 1.4.0` or `## 1.4.0.0`. The publish run refuses a tag
whose version this file has no such heading for, and refuses a heading with
nothing under it, so a version somebody installed is described here before it is
published. Whether the description is right is not judged by anything.

## Unreleased

- Nothing has been released. What exists so far is repository setup rather than
  plugin behaviour, and the plan is on the milestones.
- One place now states which server line the build targets, and the build
  refuses a package whose manifest disagrees with it
  ([#15](https://github.com/Flowfin/jellyfin-plugin-discover/issues/15)).
