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

| Setting                         | Type             | Default | Introduced by                                                          |
| ------------------------------- | ---------------- | ------- | ---------------------------------------------------------------------- |
| `Enabled`                       | `bool`           | `true`  | [#109](https://github.com/Flowfin/jellyfin-plugin-discover/issues/109) |
| `IncludeAdultTitles`            | `bool`           | `false` | [#93](https://github.com/Flowfin/jellyfin-plugin-discover/issues/93)   |
| `MaximumTitlesAcrossAllShelves` | `int`            | `120`   | [#58](https://github.com/Flowfin/jellyfin-plugin-discover/issues/58)   |
| `MaximumTitlesPerShelf`         | `int`            | `20`    | [#58](https://github.com/Flowfin/jellyfin-plugin-discover/issues/58)   |
| `SchemaVersion`                 | `int`            | `1`     | [#17](https://github.com/Flowfin/jellyfin-plugin-discover/issues/17)   |
| `UsersRefusedTheAsk`            | `list of string` | `empty` | [#98](https://github.com/Flowfin/jellyfin-plugin-discover/issues/98)   |

### Enabled

Whether this plugin does its work at all. True by default, and true for a
configuration document written before the setting existed, because an absent
element reads as the initialiser's value.

Set to false, it stops every request to a source and every fetch the scheduled
refresh would make, and keeps the configuration and the catalogue. It is for an
operator diagnosing something, sitting on a source's rate limit, or going away
for a month, and it is what to use instead of uninstalling, which takes the
catalogue with it. The one place it is read is where a run is handed its
shelves:

    git grep -n 'configuration.Enabled' -- Jellyfin.Plugin.Template/Refresh/DiscoverRefreshTask.cs
    Jellyfin.Plugin.Template/Refresh/DiscoverRefreshTask.cs:270:        if (!configuration.Enabled)

A run while it is off is handed no shelves, so it asks nothing and writes
nothing, and a test counts the questions a fake source was asked across such a
run and compares the catalogue byte for byte before and after it.

It is read before the two bounds below are judged, and a document with the
plugin off therefore carries a pair of them that does not fit onto disk and past
that reading, meeting its refusal when somebody turns the plugin back on. What
that ordering buys is the paragraph immediately below: a run under a turned-off
plugin goes on taking what the retention takes, which no number in that pair has
any bearing on. Whether the trade is the right one is a question about what a
load judges, which is
[#106](https://github.com/Flowfin/jellyfin-plugin-discover/issues/106), and a
test holds the ordering so that changing it is a decision rather than an edit.

**The scheduled task still runs while the plugin is off, and that is a decision
rather than an oversight.** What a run with no shelves does is take what the
documents on disk hold past the retention, and the retention is a source's
terms, which do not stop applying because an operator turned the plugin off.
What stops is what spends: the requests and the writes.

**The surface stays, with what the catalogue holds.** Off stops what costs a
source and a database, and a surface reading what is already on disk costs
neither; an operator who wants users to stop seeing it has the per-user control
of [#57](https://github.com/Flowfin/jellyfin-plugin-discover/issues/57) or the
uninstall. Nothing in the surface reads a catalogue document yet, so today the
surface answers empty either way, and the sentence is the decision rather than
an observation. The configuration page cannot tell an operator this yet, because
it carries no controls; this page is where it is told until
[#103](https://github.com/Flowfin/jellyfin-plugin-discover/issues/103) lands.

Turning it back on resumes the schedule. Nothing is refetched because it was
off: the documents were never taken, so the next run is the ordinary one, and
the catalogue is as it was up to what the retention took in the meantime.

At the edges. It is a boolean, so there is no range to refuse. A document with
the element spelled wrongly reads as one with the element absent, and what a
load does with a stored document this build cannot accept is
[#106](https://github.com/Flowfin/jellyfin-plugin-discover/issues/106)'s rather
than this setting's.

### IncludeAdultTitles

Whether a title the source flags as adult may be kept. False by default, and
false for a configuration document written before the setting existed, so the
exclusion holds on a server nobody has configured, which is what
[#93](https://github.com/Flowfin/jellyfin-plugin-discover/issues/93) asks for.

Left false, a title the source flagged is dropped before it becomes a record, so
nothing a shelf could draw, a catalogue could store or the seam could hand over
is ever built from one. Set to true, such a title is kept like any other. The
one place it is read is where a page the source answered with is turned into
records:

    git grep -n 'includeAdultTitles && TheSourceFlagsThisAsAdult' -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:529:            if (!includeAdultTitles && TheSourceFlagsThisAsAdult(entry))

**It changes what is kept and not what is asked for.** No address this plugin
asks accepts a parameter that would leave adult titles out of the request, which
is a row on [`docs/limits.md`](limits.md), so the exclusion is made on the
answer. What this server sends is the same in either position, and turning the
setting on does not spare a request.

**It says nothing about the two shelves the source gives no adult flag for.**
Two of the six addresses document no such field on a result, so on those shelves
the exclusion has nothing to read in either position. They ship and are
unchanged by this setting, and what they should do instead is
[#93](https://github.com/Flowfin/jellyfin-plugin-discover/issues/93)'s own open
half. That is the sentence to read before setting this to false and treating the
surface as filtered.

**It is not a per-user control.** What a given user may see out of what the
plugin holds is
[#57](https://github.com/Flowfin/jellyfin-plugin-discover/issues/57); this
decides what the catalogue may hold at all, so it binds every user on the
server.

At the edges. It is a boolean, so there is no range to refuse. Changing it does
not revisit what is already stored: a title kept while it was true stays in the
catalogue until a refresh replaces that shelf's document or the retention takes
it, and a rule that invalidated the catalogue on a configuration change is
[#93](https://github.com/Flowfin/jellyfin-plugin-discover/issues/93)'s second
condition rather than this setting's.

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

It is refused where a run reads it as well, and not only where a page saves it.
A configuration is written by a page, by a restored backup and by an operator
editing the file, and only the first of those goes through the save, so the same
call is made where the shelves for a run are derived. The rule is the same rule
and the words are the same words; nothing rewrites the file and nothing
substitutes a default.

The refusal names all four numbers, the shelf count, the per-shelf bound, the
product and the total, because an operator's next action is to change one of
them and they cannot choose which without seeing the arithmetic.

**What has not been observed is what the dashboard draws.** The refusal happens
in `Plugin.UpdateConfiguration`, so the save does not reach disk, and on the read
path it is the scheduled task that fails, so what the server shows is that task's
failure. Nothing in this
repository has been run against a server, so how either message reaches the operator
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

### UsersRefusedTheAsk

The server's own identifiers of the users this plugin will not pass a want on
for. Empty by default, and an empty list is not a permission that has been left
unset: it is a server where whoever may browse the discover surface may also ask
for a title.

That default is the answer to question 2 of the permission on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) from
2026-08-24, and it is why this setting names who may **not** rather than who
may. A list of who may would leave a fresh install one where nobody can ask,
which reads to an operator as a plugin that is broken rather than as one that is
careful.

**Seeing the surface is not this setting, and it is not this plugin's at all.**
Whether a user sees the surface is a server permission, and a user nobody has
configured holds it. This setting only takes the ask away from somebody who
already has it, so a user who is not listed here has exactly the ability to ask
that they have to browse.

**Administrator status is not used as a proxy for it**, which is a decision
rather than an oversight. On a household server there is one administrator, so a
permission keyed on that flag would admit one person, and admitting one person
is the opposite of what an operator installs this for.

At the edges, and this one fails closed rather than open. Every entry is a user
identifier as the server spells it; an entry that is not one, and the empty
identifier, are refused when the configuration is saved, with the entry quoted
back. A document that reached disk another way is met at the moment a want is
offered, and there the whole list is treated as unreadable and **every** want is
refused, including for users the list does not name. Honouring wants under a
list this build cannot read would be the silent half of what
[#98](https://github.com/Flowfin/jellyfin-plugin-discover/issues/98) exists
against, so the unreadable case costs an operator every gesture on the server
until they correct it, and the log line names which entry.

A refusal is written to the log at warning level, naming the want, the user and
which of the two reasons it was. That is where an operator sees it today, and it
is the whole of "visibly to the operator" this build offers: there is no page
that lists refused gestures, and building one is
[#92](https://github.com/Flowfin/jellyfin-plugin-discover/issues/92) and
[#103](https://github.com/Flowfin/jellyfin-plugin-discover/issues/103).

**Nothing in this repository produces a want yet.** The gesture that makes one is
[#96](https://github.com/Flowfin/jellyfin-plugin-discover/issues/96), so this
setting is read by the handover and by the suite and by nothing else on a running
server. It is stated here now rather than later because the identifier is
permanent and an operator reading this page should not find the permission
missing from it.

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
