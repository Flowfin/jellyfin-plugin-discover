# Configuration reference

Every setting this build carries, what it is, what it defaults to, and what
happens at its edges. The configuration page in the dashboard carries a
description per control; this page is for the operator who needs more than the
description.

The list below is not maintained by reading the code and remembering to come
back. `ConfigurationReferenceTests` in the test project reads the settings off
`PluginConfiguration` by reflection and refuses a page that names a different
set, a different type or a different default, so a setting added without an
entry here fails the gate rather than shipping undocumented.

## The settings

| Setting         | Type  | Default | Introduced by                                                        |
| --------------- | ----- | ------- | -------------------------------------------------------------------- |
| `SchemaVersion` | `int` | `1`     | [#17](https://github.com/Flowfin/jellyfin-plugin-discover/issues/17) |

### SchemaVersion

The version of the configuration document, written by the build that wrote the
document and read by the build that finds it. It is not a control on the
configuration page and there is no reason for an operator to set it.

The only value this build accepts is the one it writes. Anything else is refused
before it reaches disk, by `ConfigurationSchema.ThrowIfUnknown`, and the refusal
names both the version found and the version expected.

At the edges, in both directions. A document declaring a higher version is a
document a later build wrote, and it is refused rather than read as though its
fields meant what this build thinks they mean. A document declaring a lower
version is refused too, which is a deliberate choice rather than an oversight:
there is no earlier version to migrate from yet, and reading one lands in
[#106](https://github.com/Flowfin/jellyfin-plugin-discover/issues/106). A
refused document is not deleted or rewritten, so an operator who has landed on
one can move the file aside and start again with nothing lost that this plugin
was holding.

## What a setting costs

Nothing on this page costs an operator anything measurable today, because the
settings that will are not here yet. The two that will are the bound on what the
catalogue writes into the library database and the cadence at which a source is
called, and both come with a number rather than an adjective:
[#71](https://github.com/Flowfin/jellyfin-plugin-discover/issues/71) measures
what the catalogue costs on disk and in the database, and
[#78](https://github.com/Flowfin/jellyfin-plugin-discover/issues/78) is where a
source's own limits are respected. When either lands, its entry here states the
number and the command that produced it.

This section exists now rather than later so that the first expensive setting
arrives into a page that already has a place for its cost.

## What the check holds, and what it does not

It holds the set, the type and the default, and it holds them in both
directions: a setting with no entry fails, and an entry naming a setting that no
longer exists fails. It also refuses an entry that names no issue.

It does not read the prose. Whether the paragraph under a setting describes what
that setting actually does, whether the extremes named are the real extremes,
and whether the issue named is the issue that introduced it, are judgements
nothing in the tree makes. A review is where a wrong one is caught.

It reads this file from the repository rather than from the package, so it says
nothing about a copy of this page published anywhere else.
