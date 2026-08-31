# Invariants whose subject has not arrived

An invariant belongs here when the thing it would hold does not exist yet, so a
rule for it would have no shape to match and no fixture that meant anything.
Each entry names the issue that brings the subject, which is the issue that also
brings the rule.

Nothing refuses an entry that should be here and is not. This file is prose, and
the run says nothing about it.

## No source library type outside its adapter

From #33, and the subject was #73 and #74. The wire half of this entry has left
it: #74 chose the response shape, so the names exist, and
`no-source-wire-name-outside-its-adapter` is the rule over them.

The library half stays here and the reason changed. It is no longer that the
decision is open. #74 took it, and the adapter speaks to the source directly, so
there is no client library and therefore no type name for a pattern to match. A
rule written now would match nothing, and a rule matching nothing is one a reader
counts.

What holds it meanwhile is not a pattern.
`Jellyfin.Plugin.Template.Tests/allowed-assembly-references.txt` is a closed list
read by `AssemblyReferencesTests`, so a client library cannot arrive without an
edit to that file made on purpose. The day one does, this entry is what says a
rule is owed over where its types may be named.

`no-network-outside-source-adapter` holds the part of it that was always
holdable: whatever the adapter is made of, everything that leaves this server for
a third party leaves through it.

## What the configuration page renders

From #33, partly held and partly not.
`no-unescaped-render-in-config-page` refuses the calls that treat a string as
markup, which is the half that can be written as a pattern. The other half, that
a value read out of configuration reaches the page at all, needs a configuration
with something in it, and that is #103 alone now. THIS ENTRY NAMED #113 BESIDE
IT AND THAT HALF HAS ARRIVED. The reference exists and is derived from the
configuration type rather than kept by hand, so a setting cannot exist on that
type with nothing describing it:

    git grep -n 'EverySettingOnTheTypeHasAnEntry' -- Jellyfin.Plugin.Template.Tests/ConfigurationReferenceTests.cs
    Jellyfin.Plugin.Template.Tests/ConfigurationReferenceTests.cs:28:    public static void EverySettingOnTheTypeHasAnEntry()

What is left is the page carrying no control for a stored value to reach, so
there is still nothing for the second half to be about. The conclusion is
unchanged and one of the two reasons under it is gone.
