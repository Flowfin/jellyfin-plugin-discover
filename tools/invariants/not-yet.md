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

## No server type in a test outside the adapter's own tests

From #49, and the subject arrived. The plugin talks to the server through
interfaces of its own, and the rule refuses a test that reaches past them. Those
interfaces are in the tree, landed by #52 and #73, and the seam interface #95
declared has since joined them without giving this entry a fifth reason to move:

    git grep -n 'public interface' -- 'Jellyfin.Plugin.Template/*.cs'
    Jellyfin.Plugin.Template/Randomness/IRandomSource.cs:21:public interface IRandomSource
    Jellyfin.Plugin.Template/Seam/IWantReceiver.cs:31:public interface IWantReceiver
    Jellyfin.Plugin.Template/Sources/IMetadataSource.cs:36:public interface IMetadataSource
    Jellyfin.Plugin.Template/Surface/IDiscoverSurface.cs:26:public interface IDiscoverSurface
    Jellyfin.Plugin.Template/Time/IClock.cs:22:public interface IClock

and the suite no longer names a server type everywhere. Seven files of
fifty-four do, and each is a fake standing in for a server interface, the
adapter's own tests, or a test of what the plugin declares to the server:

    git grep -lE '^using (MediaBrowser|Jellyfin\.Data|Jellyfin\.Database)' -- 'Jellyfin.Plugin.Template.Tests/*.cs' | wc -l
    7

    git ls-files -- 'Jellyfin.Plugin.Template.Tests/*.cs' | wc -l
    54

This entry said the interfaces do not exist and that every test necessarily
names a server type. Both stopped being true when #52 and #73 landed, and the
entry went on giving a reason that had been overtaken while the conclusion it
supports, that the rule is not written, stayed correct.

WHAT KEEPS IT HERE IS NOT A MISSING SUBJECT ANY MORE, and that is why the entry
is rewritten rather than removed. The runner's second leg applies a rule's
pattern to the whole fixture tree without the rule's own `Subject`, so a rule
that discriminates by where a line is rather than by what it says cannot be
expressed: this rule's fixture has to be a server import in a test file, which
is the exact shape `no-channel-type-outside-surface` already refuses, and that
neighbour fires on it. Written as the residue instead, the namespaces no other
rule owns, it would be a rule a reader counts for more than it covers.

THE NEIGHBOUR IS NOT ONE NEIGHBOUR, AND THIS PARAGRAPH SAID IT WAS. Three
fixtures name a server namespace, belonging to three rules:

    git grep -nIE 'using MediaBrowser\.' -- tools/invariants/fixtures
    tools/invariants/fixtures/no-channel-type-outside-surface/AlsoBreaksTheRule.cs:6:using MediaBrowser.Controller.Entities;
    tools/invariants/fixtures/no-other-plugin-storage/BreaksTheRule.cs:10:using MediaBrowser.Common.Configuration;
    tools/invariants/fixtures/no-server-provider-key/BreaksTheRule.cs:6:using MediaBrowser.Providers.Plugins.Tmdb;

That matters to the choice rather than to the arithmetic. The set a residue
pattern has to stay disjoint from is not an obstacle to route around once: it
grows whenever a rule lands whose fixture names a server namespace, and each such
landing widens the gap between what the rule's prose would claim and what its
pattern reaches, with nothing red. It has already grown once unnoticed. And the
namespace the third one holds, `MediaBrowser.Common.Configuration`, is named by
two of the seven test files this rule is about, so the residue would be barred
from the part of the server the tests actually reach for.

WHAT THE NEIGHBOUR ALREADY DOES IS THE OTHER HALF A READER SHOULD KNOW BEFORE
CHOOSING. `no-channel-type-outside-surface` has `Subject: *.cs`, so it reaches
the test project, and one of its three exceptions is the adapter's own tests -
the same carve-out #49's condition writes. Its pattern is silent on every other
test file, which the runner watches on every invocation. So the channel and
entity half of this property is refused today, by a rule written for #52 and for
a different reason, and what is missing is the complement. That is a property
held by the side effect of another rule's subject, which moves when that rule's
exceptions move and which nothing here connects to this entry, so it is a reason
to write the rule rather than a reason not to.

Those are three different endings and choosing between them is #49's. THE ONE
THAT CHANGES THE RUNNER IS HELD BY NO OPEN ISSUE, and this entry named one until
now. #33 seeded this lint and closed as completed, so a reader following that
pointer arrives at finished work rather than at somewhere the change can be
argued:

    gh issue view 33 --repo Flowfin/jellyfin-plugin-discover --json state,stateReason
    {"state":"CLOSED","stateReason":"COMPLETED"}

Taking that ending therefore means opening an issue for the runner before the
rule can be written, and the cost of the ending is that issue rather than the
pattern. The measurements behind this paragraph, including which namespaces a
test may import today with nothing firing, are on #49 rather than repeated here.

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
    Jellyfin.Plugin.Template.Tests/ConfigurationReferenceTests.cs:27:    public static void EverySettingOnTheTypeHasAnEntry()

What is left is the page carrying no control for a stored value to reach, so
there is still nothing for the second half to be about. The conclusion is
unchanged and one of the two reasons under it is gone.
