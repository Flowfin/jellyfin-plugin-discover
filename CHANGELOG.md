# Changelog

## The version scheme

One number describes a build, and it is written as four parts because that is
what the server reads out of a package. It lives in `Directory.Build.props` and
is repeated in `build.yaml`; a gate check refuses a pull request where the two
disagree, so the repetition cannot drift.

While the leading part is `0`, nothing here is promised. Any release may change
any behaviour, any configuration key and any stored format without warning, and
the only thing a reader may rely on is what the documentation of that release
says. `0.0.0.0` is the value the tree carries before a first release exists.

A beta is a pre-release of the version that follows it, published to a separate
channel, and it carries a `-beta.N` suffix on the release tag rather than in the
four-part number, because the server has nowhere to put a suffix. A beta may be
withdrawn. Nothing installed from the beta channel is guaranteed an upgrade path
to the release that follows it.

After 1.0.0.0, a release is breaking when an operator has to do something by
hand for an upgrade to keep working: a configuration key that is removed or
changes meaning, a stored format an older version cannot read back, a supported
server line that is dropped, or a behaviour something outside this plugin
depends on. A release that is breaking raises the first part. Everything else
raises the second for a change worth telling an operator about and the third for
a fix. The fourth part is not used and stays `0`.

Every change that an operator or a contributor would want to know about is
written under Unreleased before the version is raised. A gate check refuses a
pull request that changes the version without touching this file.

## Unreleased

Nothing is released yet. This repository is still the plugin template with the
plan in the issue tracker being carried out on top of it, so the entries below
are about the tree rather than about anything a user can install.

- The template's editor scaffolding was removed and the manual steps it
  automated were written into the README.
- The front page was replaced with a README about this plugin.
- The workflow calls into the upstream reusable workflows were pinned to a
  commit and given explicit permissions.
