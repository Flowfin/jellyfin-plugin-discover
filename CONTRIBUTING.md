# Contributing

Read [the status section of the README](README.md#status) first. Almost nothing
described in this repository is built yet, and the plan is the issue tracker
rather than a roadmap document.

## Sign off every commit

Every commit in a pull request has to carry a `Signed-off-by:` line naming the
same person as the commit's author. `git commit -s` writes it, and
`git rebase --signoff <base>` adds it to commits that already exist.

    Signed-off-by: Your Name <you@example.com>

What you are certifying by adding it is in [DCO](DCO), which is the Developer
Certificate of Origin 1.1 and is the same text the Linux kernel and Jellyfin
itself use. In short, that you wrote the change or have the right to submit it,
and that you understand the contribution and the name and address in the sign-off
are public and stay in the history.

This is checked, not requested. A commit without a matching sign-off reds the
pull request, and the check names the exact line it wanted:

    FAIL  <sha> is missing: Signed-off-by: Your Name <you@example.com>

The name and address have to match the commit's author exactly, so a sign-off
with a different address than the one git is configured with fails even though a
human reads it as the same person.

The bots this repository lets in are exempt by an explicit list rather than by a
pattern that matches any address shaped like a bot's, which means you cannot get
yourself exempted by choosing a clever address.

## Branches and commits

Branch off `master` and name the branch `<area>/<what-it-does>`, lowercase, with
hyphens. What the areas look like in practice is on the remote rather than in a
list here:

    git ls-remote --heads origin | sed 's#.*refs/heads/##' | sed -n 's#^\([a-z]*\)/.*#\1#p' | sort -u

One topic per pull request. A change carrying two unrelated things has a
description that fits one of them, and the other is what a reviewer misses.

A commit message says what changed and what failure it prevents. Where it
corrects something, it says what was wrong and how that was found. The first line
is a sentence rather than a category prefix, and it carries the issue number, as
`#123`, anywhere in the line. `git log`, `git blame` and a bisect all show a
subject without the pull request that carried it, so a subject with no reference
puts the reason for a change one hop away from every tool that will read it.

Every change starts from an issue. Say which one the pull request closes, in the
body, on its own line, so the tracker closes it on merge.

Where a sentence is not meant to close an issue, name the reference with no verb
in front of it at all: `see #123`, or the bare `#123`, never "does not close
#123". The tracker matches a closing keyword followed by a reference and does
not read the words between them, so a sentence written to say a change leaves an
issue open closes it. That is the documented matching rule and there is no
exception in it for a negation:

    https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue

It is worth the paragraph because of the direction the mistake runs. A change
that fails to close an issue leaves an issue open, and somebody meets it again.
A change that closes an issue whose conditions are unmet takes it out of every
count and out of its milestone, with nothing anywhere saying a condition was
skipped, so what a later session reads as finished work is a body somebody wrote
to say the opposite. This has happened on this board in a pull-request body and
in a commit message, and it is caught by a person re-reading the issue or not at
all. Nothing here refuses the shape: `pr-hygiene` asks whether a body carries a
reference and judges no wording, which is prose rather than enforcement, like
the rest of the conventions in this file.

Both of those are checked rather than requested, by
`.github/workflows/pr-hygiene.yml`, which fails on those two and on nothing
else. Two conventions in the same file annotate instead of failing: a change of
about 400 lines or more, and plugin source moving with no change in the test
project beside it. The workflow's own header says which tier each is in and why.

## What the gate refuses

The gate is the set of workflows in `.github/workflows/`, and it is not listed
here because a list in a document drifts against the directory that decides it:

    ls .github/workflows

Which of those stand between a pull request and the merge button is a property
of the repository rather than of this tree, so that is not listed here either:

    gh api repos/Flowfin/jellyfin-plugin-discover/rules/branches/master \
      --jq '.[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context'

A check that is red without being in that set is still a check that found
something. Raising the required set to the checks worth blocking on is
[#40](https://github.com/Flowfin/jellyfin-plugin-discover/issues/40).

Four of them are worth knowing before you push, because they refuse things that
are easy to do by accident.

**Formatting outside the C#.** Prettier reads everything the analyzers do not:
the markdown, the workflows, the packaging metadata and the configuration page.
The C# is held by the analyzers and by `jellyfin.ruleset` instead. Run it the way
the gate does, from the root of a checkout:

    npx --yes prettier@3.6.2 --check .

**The invariants.** `tools/invariants/` holds rules that are this plugin's own,
each a pattern for a shape this codebase has decided not to have, each with a
fixture that breaks it. Nothing is downloaded and the runner needs only what a
checkout already has:

    tools/invariants/run.sh

Adding a rule means adding a rule file and a fixture. `tools/invariants/README.md`
says what the runner refuses and, at the end, what it cannot do.

**A version bump with no changelog line.** `CHANGELOG.md` explains how a version
works here. If your change moves `PluginVersion` in `Directory.Build.props` or
the version in `build.yaml`, it also touches `CHANGELOG.md`, or the check reds.

**Invisible and reordering Unicode.** Every tracked text file is grepped for the
bidirectional control characters behind Trojan Source, and a match fails the run
rather than warning. This is one of the checks that blocks a merge today.

## The headless rule

Tests here run without a display, without elevation, and without touching a
machine trust store.
[`Jellyfin.Plugin.Template.Tests/HEADLESS.md`](Jellyfin.Plugin.Template.Tests/HEADLESS.md)
is the one place that rule is written down, with each refusal, what replaces it,
and what none of it covers. A test that needs any of the three is refused rather
than skipped, and the replacement is named before the refusal is complete.

The gate restores, builds and then tests, in that order and in Release:

    dotnet restore
    dotnet build --configuration Release --no-restore
    dotnet test --configuration Release --no-build

One difference between that and your working tree is deliberate.
`RestoreLockedMode` is on only where `CI` is set, so a restore here accepts a
graph that differs from the committed `packages.lock.json` and the same restore
on the gate refuses it. If you add or move a package, restore with
`--force-evaluate` and commit the lock file the restore writes.

The second difference is the runner's rather than this tree's, and it is the one
that reads as the suite being unrunnable. The gate's machine has the runtime the
tree targets and yours may not. The test host asks for that exact major version
and starts nothing without it, so no test is reported and the run ends on a
message about the machine instead of about the code. One sentence of it is the
host's own and is the one to recognise; the rest is the machine's locale and a
list of the runtimes it did find:

    You must install or update .NET to run this application.

That is an install instruction rather than a verdict, and taking it is not the
only way out. Tell the host a later runtime will do:

    DOTNET_ROLL_FORWARD=Major dotnet test --configuration Release --no-build

Which version it asks for is not written here. It follows the target framework,
and `Directory.Build.props` is the one place that is stated, so the lines beside
the one above name whatever that file says on the day it is read.

This paragraph exists because the other conclusion has been drawn in writing on
this board more than once, that the suite cannot run on a machine without that
runtime, each time from recollection rather than from the line above and each
time corrected afterwards. Installing a runtime is not a step anybody has to
take to run this suite.

## Which server line

`Directory.Build.props` is the one place a server line is stated. The project
file, the packaging metadata and the workflows derive from it rather than
repeating it, and the build fails when the packaging metadata disagrees. Do not
add a second statement of a version anywhere; change that file.

Which lines this project intends to support is a different question from which
one the tree builds against today, and the two currently have different answers.
[`docs/support.md`](docs/support.md) is the first one and states which lines
carry a support commitment; this section is the second one only.

## Reporting something rather than fixing it

Bugs and requests go in the issue tracker. A security problem does not: read
[SECURITY.md](SECURITY.md) first.

## Licence

The plugin is under the GPLv3, in [LICENSE](LICENSE). A Jellyfin plugin links
against the Jellyfin binary packages, which are themselves GPLv3, so a compiled
plugin is GPLv3 whatever a source licence says. Contributions are accepted under
that licence and under the certificate above.
