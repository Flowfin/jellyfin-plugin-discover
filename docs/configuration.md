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

**No setting here has a control on the configuration page yet.** That page
carries no script that reads or writes a configuration at all, so a control on it
would be one an operator could move with no effect, which is worse than an
absent one. Building the page is
[#103](https://github.com/Flowfin/jellyfin-plugin-discover/issues/103). Until it
lands, a setting other than its default is a hand edit of the plugin's
configuration document on disk, and a document the plugin refuses is refused when
the server hands it back rather than when it is typed.

## The settings

| Setting                         | Type  | Default | Introduced by                                                        |
| ------------------------------- | ----- | ------- | -------------------------------------------------------------------- |
| `MaximumTitlesAcrossAllShelves` | `int` | `120`   | [#58](https://github.com/Flowfin/jellyfin-plugin-discover/issues/58) |
| `MaximumTitlesPerShelf`         | `int` | `20`    | [#58](https://github.com/Flowfin/jellyfin-plugin-discover/issues/58) |
| `SchemaVersion`                 | `int` | `1`     | [#17](https://github.com/Flowfin/jellyfin-plugin-discover/issues/17) |

### MaximumTitlesPerShelf

The most titles one shelf may hold. Twenty by default, which is the one source's
own page size rather than a round number:

    git grep -n 'private const int PageSize' -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:66:    private const int PageSize = 20;

A shelf of at most one page costs one request per refresh. Every twenty titles
above that costs another request against a budget this plugin does not own,
which is [#78](https://github.com/Flowfin/jellyfin-plugin-discover/issues/78),
so forty a shelf is twice the calls for a row a television remote has to scroll
to reach.

At the edges. Zero and anything below it are refused rather than read as "hold
nothing": a plugin that holds nothing is one that has been turned off, which is
[#109](https://github.com/Flowfin/jellyfin-plugin-discover/issues/109), and a
bound of zero would leave every shelf drawn and empty with nothing saying why.
There is no maximum of the setting's own. A large number is an operator's
decision about their own server, and what is refused instead is a number that
contradicts the one beside it.

### MaximumTitlesAcrossAllShelves

The most titles this plugin may write into the library database in total. Every
title the surface returns becomes a row there, so this is the number an operator
reads when they want to know what installing this costs them.

A hundred and twenty by default, which is the shipped set at the per-shelf
default rather than a second number chosen on its own. Six shelves ship:

    git grep -c 'Row("' -- Jellyfin.Plugin.Template/Shelves/ShippedShelves.cs
    Jellyfin.Plugin.Template/Shelves/ShippedShelves.cs:6

Six shelves at twenty titles each is a hundred and twenty rows on a first
install that changes nothing. The arithmetic is held rather than stated:
`CatalogueBoundsTests` derives the default from the shipped set's own size, so a
seventh shelf reddens the suite instead of quietly making the default
configuration one the plugin refuses to save.

**A hundred and twenty is a row count and not a size.** What a row costs on disk
and in the library database has not been measured, which is
[#71](https://github.com/Flowfin/jellyfin-plugin-discover/issues/71), so this
default is defended against the request budget above and against nothing else.

At the edges. Zero and below are refused for the same reason as the per-shelf
bound. A total smaller than `MaximumTitlesPerShelf` is refused as well, because
no set of shelves satisfies both, including a set of one; that is the ordinary
typing mistake of lowering the total and forgetting the number beside it. And a
total the shipped shelves do not fit inside is refused when the configuration is
saved, rather than truncated at a refresh nobody is watching: a surface whose
last shelves are short for a reason nothing on the screen explains is
indistinguishable from a source that answered with nothing.

The refusal names all four numbers, the shelf count, the per-shelf bound, the
product and the total, because an operator's next action is to change one of
them and they cannot choose which without seeing the arithmetic.

**What has not been observed is what the dashboard draws.** The refusal happens
in `Plugin.UpdateConfiguration`, so the save does not reach disk. Nothing in this
repository has been run against a server, so how the message reaches the operator
is unknown here; the configuration page is
[#103](https://github.com/Flowfin/jellyfin-plugin-discover/issues/103) and
refusing a bad setting as a general rule is
[#105](https://github.com/Flowfin/jellyfin-plugin-discover/issues/105).

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

The first two settings that cost an operator something are here now, and they are
the bound on what this plugin writes into the library database. Both state their
cost above with the command that produced the number, which is what this section
asked of them before they landed.

**The cost they state is a count of requests and a count of rows, and neither is
a size.** How much disk the catalogue takes and how much the library database
grows per title is
[#71](https://github.com/Flowfin/jellyfin-plugin-discover/issues/71) and has not
been measured, so a hundred and twenty rows is a number an operator can act on
only if they already know what a row costs them. Nothing here tells them, and
saying otherwise would be a defended default this repository does not have.

The other setting that will cost something is the cadence at which a source is
called, which is
[#78](https://github.com/Flowfin/jellyfin-plugin-discover/issues/78). When it
lands, its entry here states the number and the command that produced it.

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
