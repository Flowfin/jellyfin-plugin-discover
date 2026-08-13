# 0004 What crosses the seam to a requests plugin

Decided, apart from the version rule. Raised in
[#94](https://github.com/Flowfin/jellyfin-plugin-discover/issues/94).

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
[#95](https://github.com/Flowfin/jellyfin-plugin-discover/issues/95), and a
receiver written against this note still needs that to receive anything.

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

## What is not settled here

The version rule. The field above says a message carries a contract version. How
that version changes without breaking either side, what a receiver does with a
version it does not know, and whether anything is negotiated at all, is
[#101](https://github.com/Flowfin/jellyfin-plugin-discover/issues/101), and
writing it here would answer that issue from inside this one.

The gesture that produces a want is
[#96](https://github.com/Flowfin/jellyfin-plugin-discover/issues/96) and its
answer is question 2 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2). Who may
produce one at all is
[#98](https://github.com/Flowfin/jellyfin-plugin-discover/issues/98). Neither
changes what crosses, and both change whether anything does.

This note does not close #94. That issue's first condition wants the version
rule in this document, and its last keeps it open until the sibling repository
exists and points at it.

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
