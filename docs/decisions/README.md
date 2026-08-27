# Decision record

Four decisions in this plan shape everything built on top of them: how a
discover page is represented, who owns the catalogue, where that catalogue
lives, and what crosses the seam to a requests plugin. Each one has an argument
behind it, and each argument will be re-opened by whoever arrives next. This
directory is so that the re-opening starts from the reasoning rather than from a
reconstruction of it.

## The notes

| Note                                                               | Decision                                                        | State   |
| ------------------------------------------------------------------ | --------------------------------------------------------------- | ------- |
| [0001](0001-a-discover-page-is-a-server-channel.md)                | A discover page is a server channel                             | Decided |
| [0002](0002-this-plugin-owns-the-catalogue.md)                     | This plugin owns the catalogue, a requests plugin owns requests | Decided |
| [0003](0003-the-catalogue-lives-in-the-plugins-own-data-folder.md) | Where the catalogue lives on disk                               | Decided |
| [0004](0004-what-crosses-the-seam-to-a-requests-plugin.md)         | What crosses the seam to a requests plugin                      | Decided |

Every row is decided. The last one was not until the version rule arrived under
[#101](https://github.com/Flowfin/jellyfin-plugin-discover/issues/101), which is
the paragraph this one replaces; the note now fixes the field set, the direction,
what a receiver may not assume, and what a version change may do.

A decided note is not a closed issue. Both issues behind that row stay open for
reasons the note names at its end: #101 for the behaviour at the point
implementations are resolved and for whether the interface is published or
copied, and
[#94](https://github.com/Flowfin/jellyfin-plugin-discover/issues/94) until a
reader who has never seen this repository can implement the other side from the
note.

This paragraph gave a different reason for #94 until now, that the sibling
repository does not yet exist and point at the note. It exists and it points,
which the note's own last section has said since that reason retired, and an
index sending a reader to finished work is worse than one saying nothing.

## What a note holds

What was decided, what was rejected and why, the evidence, and what would
reverse it. The last of those is the part that makes a note worth keeping: a
reader who wants to re-argue a decision can check whether the ground moved
instead of re-running the argument from the start.

A note carries no limit of its own. What this plugin cannot do belongs in
[#114](https://github.com/Flowfin/jellyfin-plugin-discover/issues/114), and a
note points there rather than repeating it, so there is one place a limit can be
wrong.

## Decisions that are open

The decisions this plan still needs are collected in
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), and this
index does not restate them. Two of that issue's ten touch the notes here
without reopening them: which server lines are carried, question 1, and how long
a fetched catalogue may be kept, question 8.

## Reversing a note

A note that is overtaken is superseded in place. The old text stays where it is,
readable, with a line at the top saying what replaced it and why. It is not
edited into the new answer and it is not deleted.

The reason is that a record showing only the current answer teaches nobody why
the current answer is the current one. Somebody arriving later with the rejected
option in mind needs to find the rejection, not a document that reads as though
the question was never asked.
