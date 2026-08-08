# Invariants whose subject has not arrived

An invariant belongs here when the thing it would hold does not exist yet, so a
rule for it would have no shape to match and no fixture that meant anything.
Each entry names the issue that brings the subject, which is the issue that also
brings the rule.

Nothing refuses an entry that should be here and is not. This file is prose, and
the run says nothing about it.

## No source library type and no source wire type outside its adapter

From #33, and the subject is #73 and #74. Whether the first adapter depends on a
client library at all is decided in #74, and the wire types are whatever the
response shape turns out to be, so there is no name to match today. A rule
written now would either match nothing or guess at a vocabulary that has not
been chosen.

`no-network-outside-source-adapter` holds the part of it that can be held
already: whatever the adapter is made of, everything that leaves this server for
a third party leaves through it.

## No server type in a test outside the adapter's own tests

From #49, and the subject is #52 and #73. The plugin talks to the server through
interfaces of its own, and the rule refuses a test that reaches past them. Those
interfaces do not exist, and today every test in the suite necessarily names a
server type, because the only thing there is to test is a plugin class the
server defines. #49 adds the rule with the file set it derives from.

## Nothing reads or writes another plugin's storage

From #69, and the subject is #65. The position is already decided and written
out in `docs/decisions/0002-this-plugin-owns-the-catalogue.md`: this plugin owns
the catalogue, a requests plugin owns request records and their fulfilment, and
neither reads the other's files. That is what keeps either one installable
alone. #69's last condition asks for a rule that refuses the code rather than a
note asking for it, and `0002` says in its own words that until such a rule
exists and is proven to fire, the note is the whole of the enforcement.

There is nothing for the rule to match yet. This plugin writes nothing at all:
#65 is where the catalogue's location and form are chosen, and a pattern written
before that choice would have to guess at the calls a store has not been given.
The two spellings it will have to reach are a path built under another plugin's
directory and a type loaded out of another plugin's assembly, and whether either
is even expressible as a line pattern depends on what #65 chooses.

## What the configuration page renders

From #33, partly held and partly not.
`no-unescaped-render-in-config-page` refuses the calls that treat a string as
markup, which is the half that can be written as a pattern. The other half, that
a value read out of configuration reaches the page at all, needs a configuration
with something in it: #103 is where the page gets its settings and #113 is where
they are listed. Until then there is nothing for the second half to be about.
