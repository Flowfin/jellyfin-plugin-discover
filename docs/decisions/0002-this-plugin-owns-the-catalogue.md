# 0002 This plugin owns the catalogue, a requests plugin owns requests

Decided. Raised in
[#69](https://github.com/Flowfin/jellyfin-plugin-discover/issues/69).

## The problem this settles

Two plugins will eventually want the same data. This one holds titles the server
does not have, so that it can show them. A requests plugin holds titles somebody
asked for, so that it can track them. Those two sets overlap. If neither side
says who owns what, both store both, and the two copies disagree the first time
one of them is refreshed and the other is not.

## What was decided

This plugin owns the catalogue. That means the source adapters, the shelf
definitions, the fetched title records, and their expiry.

A requests plugin owns request records and their fulfilment. It receives a copy
of the title identity at the moment of a handover. It does not read this
plugin's storage.

Neither plugin reads or writes the other's files or database rows. That is the
single property that keeps either one installable on its own, which is the
requirement the whole plan is built on.

## Why ownership rather than a shared store

A shared store needs an agreed schema, a migration that both sides run, and an
answer to what happens when one of the two is upgraded and the other is not.
Every one of those is a coupling between two plugins an operator installs and
removes independently, and the failure it produces is on a running server rather
than in a build.

Handing over a copy costs a duplicated field set and nothing else. The copy is
stale from the instant it is made, which is a property to state rather than a
problem to solve, and the section below states it.

## What this plugin promises about what it handed over

That the identifiers were correct at the time the title was fetched. Nothing
more.

Not that the title still exists at the source. Not that the artwork is still
reachable. Not that the overview text has not changed. Not that this plugin
still holds the record at all, because retention removes records and the
retention ceiling is question 8 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2).

A requests plugin that wants a current answer re-resolves the title from the
source by identifier. That is its business and not this plugin's, and it needs
nothing from this plugin to do it, because an identifier plus the name of the
source it came from is the whole input. This plugin will not grow a lookup
endpoint for that purpose.

## What is not settled here

The record that crosses the boundary is not named or versioned in this note. It
is a projection of the catalogue record from
[#64](https://github.com/Flowfin/jellyfin-plugin-discover/issues/64), which does
not exist yet, and the contract that carries it is
[#94](https://github.com/Flowfin/jellyfin-plugin-discover/issues/94), with the
version rule in
[#101](https://github.com/Flowfin/jellyfin-plugin-discover/issues/101). Naming a
field set here would put a second description of the handover in the tree, next
to the one #94 exists to hold.

The boundary above is a position and, since
[#69](https://github.com/Flowfin/jellyfin-plugin-discover/issues/69) added
`tools/invariants/rules/no-other-plugin-storage.rule`, also a mechanism for two
of the ways it can be broken: a path composed under the plugins path or the
plugin configurations path, and a type loaded out of an assembly by path at run
time. That rule was watched refusing both.

It is not the whole of the boundary and the rule's own text says where it stops.
A plugin that reads another's data through an API the server offers is outside
any pattern a text lint can write, and for that part this section is still the
whole of the enforcement.

## What would reverse this

The server growing a store that plugins are meant to share, with the migration
and the version skew handled by the server rather than by either plugin. That
removes the coupling argument above, which is the argument this note rests on.

A requirement that the two plugins ship and upgrade together. That is the
opposite of the requirement today, and it would make a shared store cheaper than
a handover rather than more expensive.

Neither is in sight, and the second one would be a change to what this plugin is
for rather than to how it is built.
