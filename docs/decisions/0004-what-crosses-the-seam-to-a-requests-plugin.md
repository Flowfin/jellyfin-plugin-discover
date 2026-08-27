# 0004 What crosses the seam to a requests plugin

Decided, version rule included. Raised in
[#94](https://github.com/Flowfin/jellyfin-plugin-discover/issues/94); the version
rule under
[#101](https://github.com/Flowfin/jellyfin-plugin-discover/issues/101).

## The problem this settles

A sibling plugin, jellyfin-plugin-requests, will handle what happens after a
user says they want a title. It does not exist yet. Everything about the
handover therefore has to be written from one side, before there is anybody to
agree it with, and the thing that goes wrong when it is not written down is not
a disagreement. It is each side growing its own idea of what the other needs,
discovered when both are installed on somebody's server.

[0002](0002-this-plugin-owns-the-catalogue.md) settled who owns what and said in
as many words that the field set is not named there, so that there is one
description of the handover rather than two. This note is that description.

## The division, stated once

1. This plugin owns the catalogue. The source adapters, the shelf definitions,
   the fetched title records, their expiry, and the surface every client
   browses.
2. A requests plugin owns requests. Who asked for what, when, what state it is
   in, what fulfils it, and anything that talks to whatever acquires media.
3. The boundary is a handover, one way, at one moment. A user expressed a want.
   This plugin says what was wanted and who wanted it. It learns nothing back
   except, optionally, that the handover was accepted.
4. Neither plugin reads the other's files, database or configuration. Neither
   references the other's assembly. A server may have either alone.

Three of those four are not proposals here. Point 1 is
[0002](0002-this-plugin-owns-the-catalogue.md). The two halves of point 4 are
refused as tracked text by `tools/invariants/rules/no-other-plugin-storage.rule`
and `tools/invariants/rules/no-sibling-plugin-reference.rule`, and the assembly
half again by `AssemblyReferencesTests`, which reads the references out of the
built assembly rather than out of the project file. What this note decides on
its own account is point 3 and the field set below.

## What crosses

One message, in one direction, per want. The fields are fixed here. How they are
encoded and how the message travels is the extension point, which is
[#95](https://github.com/Flowfin/jellyfin-plugin-discover/issues/95), and it is
built: the message is the `Want` record, the route is `IWantReceiver` resolved
from the server's container, and `WantHandover` is what offers one to the other.

    git grep -n 'public interface IWantReceiver\|public sealed record Want$' -- 'Jellyfin.Plugin.Template/Seam/*.cs'

The list below stays the authority for what the message carries. A field on that
record and no row here, or the reverse, is a drift in this note rather than a
second contract.

| Field                | What it is                                                                                                                      | Why it crosses                                                                                                   |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Provider identifiers | One or more pairs of a source and that source's identifier for the title, from `DiscoverTitleIdentity` in the catalogue record. | A receiver resolves the title itself from these. They are the whole input it needs to do that.                   |
| Kind                 | Film or series, from `DiscoverTitleKind`, which has those two members and no others.                                            | A receiver acts differently on the two, and inferring it from whether a year is present is a guess.              |
| Name                 | The title as the source gave it.                                                                                                | So a person reading a request list recognises what they asked for without a lookup.                              |
| Release year         | The year, or absent where the source gave none.                                                                                 | The same reason as the name. Two films share a name often enough that the name alone is not recognition.         |
| Asking user          | The server's identifier for the user who made the gesture.                                                                      | A request belongs to somebody. A receiver cannot ask this plugin later, because there is no route back.          |
| Want identifier      | This plugin's own identifier for this want.                                                                                     | So a receiver can tell a repeat of one want from two wants, and so the same want handed over twice is one thing. |
| Contract version     | Which version of this contract the message is written to.                                                                       | So a receiver can refuse a message it does not understand rather than read it as though the two versions agreed. |

Absence is absence. A field the source gave nothing for is sent as absent rather
than as an empty string or a zero, which is the rule the catalogue record itself
holds and the reason it holds it: a source that returned no year is a different
thing from a source that returned the year zero.

## How the want identifier is computed

Written under
[#99](https://github.com/Flowfin/jellyfin-plugin-discover/issues/99). It is here
rather than left to the code because the rule below says that changing how an
existing field's value is computed is what raises the contract version, and a
rule about a computation nobody wrote down cannot be broken visibly.

`<source>:<user>:<identifier>`, in that order, with `:` between the three. The
source is the name of the body that issued the identifier. The user is the
server's identifier for the asking user, thirty-two hexadecimal digits and no
separators. The identifier is that source's identifier for the title, exactly as
the source spelled it: this plugin normalises nothing, so it is everything after
the second `:` and a value carrying a `:` of its own crosses whole.

Which of a title's identifiers is used is the highest-precedence one the title
has, which is IMDb, then TMDB, then TheTVDB, and is the same one the server's own
item identity is built from.

Two consequences a receiver may rely on. Two users wanting one title are two
wants, because the user is inside the value rather than in a field beside it.
One user wanting one title is one want however many times the gesture is seen,
because nothing that varies between runs reaches the computation.

One residual, stated rather than removed. A title whose identifiers later gain
one the precedence puts first has a different value here, so a receiver sees a
second want. That is the same moment the server's own item identity moves, which
is [#60](https://github.com/Flowfin/jellyfin-plugin-discover/issues/60), so what
a user was looking at is a different item by then and a second want is what a
second item means. A value computed over every identifier instead would move
whenever a response carried one identifier more, which is far more often and for
no such reason.

## What does not cross, and why not

The catalogue record carries more than the list above. The rest stays here.

The summary and the artwork location do not cross. Both are the source's
content, and the terms rule in
[`docs/sources/tmdb.md`](../sources/tmdb.md) allows the handover on the ground
that what moves is a reference plus the little a person needs to recognise what
they asked for. A description is neither.

The original-language name does not cross. A receiver that wants it re-resolves
the title from the identifiers, which is the same route it already takes for
anything current.

The catalogue record's own schema version does not cross. It is the version of
this plugin's storage, it changes for reasons that have nothing to do with the
seam, and a receiver reading it would be reading a number about a shape it never
sees.

## What this plugin promises about what it handed over

That the identifiers were correct at the time the title was fetched. Nothing
more. The argument for that, and the list of the things it is deliberately not,
is [0002](0002-this-plugin-owns-the-catalogue.md). The sentence is repeated here
rather than only pointed at because a contract a stranger implements from has to
carry its own guarantee, and the pointer says which of the two is the argument.

## What a receiver may not assume

No ordering. Two wants handed over in one sequence may arrive in the other, and
nothing here numbers them.

No delivery guarantee. A handover that fails is this plugin's problem and it is
recorded locally, which is
[#97](https://github.com/Flowfin/jellyfin-plugin-discover/issues/97). A receiver
never learns that a message it did not get existed.

No transaction. There is nothing to roll back on either side, and an accepted
handover is not a commitment by either party to anything afterwards.

No callback. This plugin does not ask what happened to a want and offers nothing
for a receiver to call. What it does instead is watch its own library, which is
[#100](https://github.com/Flowfin/jellyfin-plugin-discover/issues/100).

## Where the field list is authoritative

Here. The same list appears in one other place, as a row in the table of every
feature that moves source data on
[`docs/sources/tmdb.md`](../sources/tmdb.md), and it has to appear there because
that table exists to be checkable against every such feature rather than to
point elsewhere. That row now names this note, so a field added in one place has
one place it is added and one place that cites it.

## How this contract changes

Written under [#101](https://github.com/Flowfin/jellyfin-plugin-discover/issues/101),
which is where this rule is argued. It settles that issue's first condition and
none of its other three.

**One integer, and it counts breaking changes only.** The contract version is a
whole number starting at 1. It is not a version of this plugin, of the catalogue
record, or of anything else, and it does not carry a second part. A receiver has
exactly one question - can I read this message - and one number answers it. Two
parts would invite a receiver to reason about the second one, which is the
negotiation this seam does not have.

**Adding a field a receiver may ignore does not raise it.** A new field that an
existing receiver can leave unread changes nothing for that receiver, and raising
the number for it would make every older receiver refuse a message it could have
read. Absence is absence, so a receiver meets a field it does not know by not
looking for it, and one it does know by its presence.

**These may never change meaning at one version, and changing them is what raises
it.** Removing a field. Making a field that could be absent always present, or
the reverse. Changing what an existing field means, what it is scoped to, or how
it is computed. Changing the alphabet or the shape of an existing field's value.

**Three fields are the ones a receiver keys on, and they carry the strongest form
of that rule.** The provider identifiers, because resolving the title from them
is the whole of what a receiver does. The kind, because a receiver acts
differently on the two members. And the want identifier, because a receiver that
stores it has made it part of the contract whether or not anybody said so: two
handovers carrying one want identifier are one want, at any version. A release
that recomputes it is breaking, and that is
[#99](https://github.com/Flowfin/jellyfin-plugin-discover/issues/99)'s identifier
named here as contract rather than as an implementation detail.

**What forces something other than a version.** A message travelling the other
way, or anything a receiver returns beyond the optional acknowledgement in point
3 above, is not a version of this contract. It is a second contract and a
different interface, and it reopens the argument in
[0002](0002-this-plugin-owns-the-catalogue.md) about which side owns what rather
than following from it.

**What each side does with a number it does not recognise.**

This plugin writes the version it was built for and reads nothing back except the
acknowledgement. It therefore never sees a receiver's version and never adapts to
one: there is no negotiation here, and a reader looking for one should stop.

A receiver reads the number first. A number it does not know is higher than any
it knows, because the number only ever grows, so the message was written to a
contract that changed in a way it cannot see. It refuses the message rather than
reading the fields it recognises, because the reason the number moved is that one
of those fields no longer means what it did.

A refusal is not an error on this side. The want is already recorded locally, by
[#97](https://github.com/Flowfin/jellyfin-plugin-discover/issues/97), and a
receiver that refuses is behind rather than broken. Retrying the same message
produces the same refusal, so it is not retried for that reason.

**Version 1 is not frozen yet, and this says where that stops being true.**
Nothing has been published from this repository and no sibling exists, so the
field set above is version 1 and a change to it before the first release edits
version 1 rather than minting version 2. From the first release that ships this
seam, every rule above applies as written. That is the same window `CHANGELOG.md`
describes for the leading zero in this plugin's own version: cheap now, and
expensive from the moment somebody has installed something.

## What is not settled here

The rest of #101, and this paragraph said the place those behaviours live did not
exist. It exists. The rule above says what a version change may do. That this
plugin tolerates a receiver built against an older version, and that a newer one
is handled by a stated rule, are behaviours at the point implementations are
resolved, which is now `WantHandover`, and neither is written there: what it does
with a receiver's answer is take it or not take it, and it never reads a version
back because there is none to read. Whether the interface is published for a
sibling to compile against, or copied, is #101's fourth condition and is a
packaging decision that needs a release path, which is
[#119](https://github.com/Flowfin/jellyfin-plugin-discover/issues/119). A copied
interface is two types in one process that do not satisfy each other, so that
condition is not a formality.

The gesture that produces a want is
[#96](https://github.com/Flowfin/jellyfin-plugin-discover/issues/96) and its
answer is question 2 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2). Who may
produce one at all is
[#98](https://github.com/Flowfin/jellyfin-plugin-discover/issues/98). Neither
changes what crosses, and both change whether anything does.

This note does not close #94, and both of the reasons it used to give have gone.
That issue's first condition wanted the version rule in this document and has it,
in the section above. Its last kept the issue open until the sibling repository
existed and pointed at this note; `jellyfin-plugin-requests` exists and
`docs/seam.md` there names #94 as the contract, says it is the only one, and
writes no second field list.

What is left is that issue's fourth condition, that a reader who has never seen
this repository can implement the other side from the note. The field set, the
meanings and the version rule are here, and the encoding and the route are now in
the tree rather than owed: a receiver implements `IWantReceiver` and registers it
under that interface in its own registrator. What a reader of this note alone
still cannot do is compile against the type, because nothing publishes it yet,
which is #101's fourth condition. That is a bound on this note rather than a gap
in it, and it is a narrower one than the bound this paragraph used to state.

## What would reverse this

A receiver that genuinely cannot resolve a title from identifiers, because the
sources this plugin carries and the sources it carries do not overlap. Then
either more of the record crosses, against the terms rule above, or the two do
not connect. That is a reason to re-argue this note rather than to widen it
quietly.

A second consumer with different needs. This contract is shaped by there being
one receiver whose job is known. A second one wanting a different field set is
the moment to ask whether the seam is a contract or an interface, and those are
different things with different costs.
