# Adopt or decline, for every workflow on the target gate

The target is the gate on `iderex/jellyfin-plugin-sso`, read at commit
`eae9feb2edcf94be926b0204162da98f51337c9c`, which was the head of that
repository's default branch when this table was written:

    gh api repos/iderex/jellyfin-plugin-sso/commits/main --jq .sha
    eae9feb2edcf94be926b0204162da98f51337c9c

Naming the commit is what lets a reader tell whether this table has drifted. The
target moves, and a table that did not say which state it was written against
would quietly stop describing it.

It has moved once since, by one file and in one direction:

    diff <(gh api "repos/Flowfin/jellyfin-plugin-sso/contents/.github/workflows?ref=eae9feb2edcf94be926b0204162da98f51337c9c" --jq '.[].name' | sort) \
         <(gh api repos/Flowfin/jellyfin-plugin-sso/contents/.github/workflows --jq '.[].name' | sort)
    10a11
    > perf-baseline.yml

That file has a row below like every other. The pin stays where it is rather
than moving to the head that added it, because the pin is what a reader compares
against, and moving it without re-reading all twenty-four rows would say the
whole table had been checked again when one row had.

Both workflow sets are printed rather than restated:

    gh api "repos/iderex/jellyfin-plugin-sso/contents/.github/workflows?ref=eae9feb2edcf94be926b0204162da98f51337c9c" --jq '.[].name' | sort
    ls .github/workflows | sort

Which checks stand behind a merge here is a property of the repository rather
than of this tree, so it is not written down either:

    gh api repos/Flowfin/jellyfin-plugin-discover/rules/branches/master \
      --jq '.[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context'

Raising that set is [#40](https://github.com/Flowfin/jellyfin-plugin-discover/issues/40)
and it is the last thing in the milestone.

## Why the two gates differ

This plugin is not the SSO plugin. It has no authentication surface, so nothing
here has the blast radius that justifies the target's login harness. It fetches
from a third party over the network, which the target does not, so supply-chain
and response-parsing checks matter more here than there. And it writes items
into the library database, which nothing on the target does, so what it puts in
front of a user is a correctness question rather than a security one.

Both directions appear below. A decline is a decision and says what makes it
one; a row that could not say why would be a defect rather than an entry.

## One branch name

`master`, which is what this repository's default branch already is:

    gh repo view Flowfin/jellyfin-plugin-discover --json defaultBranchRef --jq .defaultBranchRef.name
    master

Every trigger, the ruleset and every documented command name that one. Renaming
the default branch is a change to the repository's settings rather than to this
tree, and it would invalidate the ruleset and every reference at once, so it is
not a thing this table takes on its own.
[#28](https://github.com/Flowfin/jellyfin-plugin-discover/issues/28) is where the
triggers that still name the other one are corrected.

## Every workflow on the target gate

`adopted` means the workflow exists here. `adopt` means it is wanted and the
issue named beside it carries the work. `decline` means it is not wanted and the
row says what makes it so. `defer` means an open question decides it.

| Target workflow             | Verdict | Why, in one line                                                                                                                                                                                                                                                                                          | Where it lands here                                                                                                                                                                                                                                                                                                                                    |
| --------------------------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `build.yml`                 | adopt   | Packaging has to happen inside the gate or the first time a package is built is the release.                                                                                                                                                                                                              | present as `build.yaml`, calling this repository's own `build-run.yml`, from [#27](https://github.com/Flowfin/jellyfin-plugin-discover/issues/27); [#35](https://github.com/Flowfin/jellyfin-plugin-discover/issues/35) adds the bill of materials                                                                                                     |
| `codeql.yml`                | adopted | A scanner that reads this tree rather than reporting on another repository's name.                                                                                                                                                                                                                        | present as `scan-codeql.yaml`, from [#29](https://github.com/Flowfin/jellyfin-plugin-discover/issues/29)                                                                                                                                                                                                                                               |
| `dco.yml`                   | adopted | Already here, refusing a commit with no sign-off matching its author.                                                                                                                                                                                                                                     | present as `dco.yml`                                                                                                                                                                                                                                                                                                                                   |
| `dependency-review.yml`     | adopted | Already here, reading the pull request's dependency diff against the advisory database, which is a second reader on what `NuGetAudit` in `Directory.Build.props` already refuses at restore time.                                                                                                         | present as `dependency-review.yml`                                                                                                                                                                                                                                                                                                                     |
| `dotnet.yml`                | adopt   | Build, test and coverage owned here rather than called from another organisation, plus its ABI floor job.                                                                                                                                                                                                 | the test half is present as `test.yaml` calling `test-run.yml` and the build half as `build.yaml` calling `build-run.yml`, both from [#27](https://github.com/Flowfin/jellyfin-plugin-discover/issues/27); the ABI floor is present as `abi-matches-the-line.yml`, from [#30](https://github.com/Flowfin/jellyfin-plugin-discover/issues/30)           |
| `e2e-login.yml`             | decline | It drives a login round trip, and this plugin has nothing to log into: it adds no authentication surface at all.                                                                                                                                                                                          | replaced by [#38](https://github.com/Flowfin/jellyfin-plugin-discover/issues/38), which proves the discover surface reaches a client instead; present as `discover-surface-appears.yml`, which asks a signed-in user's views for it                                                                                                                    |
| `fuzz.yml`                  | adopt   | This plugin parses responses from a third party, which is the one place here where a hostile input arrives from outside.                                                                                                                                                                                  | [#37](https://github.com/Flowfin/jellyfin-plugin-discover/issues/37)                                                                                                                                                                                                                                                                                   |
| `manifest-freshness.yml`    | adopt   | A manifest that stops listing the newest release fails silently and only a user notices.                                                                                                                                                                                                                  | [#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120)                                                                                                                                                                                                                                                                                 |
| `nightly-betas.yml`         | adopt   | A beta channel is worth nothing if nothing builds into it.                                                                                                                                                                                                                                                | [#121](https://github.com/Flowfin/jellyfin-plugin-discover/issues/121)                                                                                                                                                                                                                                                                                 |
| `opengrep.yml`              | adopted | The mechanism is the same and the invariants worth grepping for here are this plugin's own.                                                                                                                                                                                                               | present as `invariants.yml`, from [#33](https://github.com/Flowfin/jellyfin-plugin-discover/issues/33)                                                                                                                                                                                                                                                 |
| `perf-baseline.yml`         | decline | Its own header says it times a login round trip in-process, weekly and never on a pull request, and there is no login here to time. The cost that matters here is what a refresh does to a large library, which is one measurement with the library size beside it rather than a number taken every week. | nothing; [#194](https://github.com/Flowfin/jellyfin-plugin-discover/issues/194) holds that measurement and it is a document rather than a workflow. Added to the target after the pinned commit, which the note at the top of this page shows.                                                                                                         |
| `pr-hygiene.yml`            | adopted | It is the defect class no code scanner covers, and it is cheapest before the tenth pull request rather than after.                                                                                                                                                                                        | present as `pr-hygiene.yml`, from [#32](https://github.com/Flowfin/jellyfin-plugin-discover/issues/32), publishing the check-run name `Deterministic pull-request hygiene`; the changelog leg the target keeps inside its own version stays in `changelog-entry.yml` below                                                                             |
| `prettier.yml`              | adopted | The analyzers read the C# and nothing read the rest.                                                                                                                                                                                                                                                      | present as `format.yml`, from [#34](https://github.com/Flowfin/jellyfin-plugin-discover/issues/34)                                                                                                                                                                                                                                                     |
| `publish.yml`               | adopt   | A release path that is not the gate's build path is a package nobody checked.                                                                                                                                                                                                                             | present as `publish.yaml`, which is a call into the upstream project's reusable workflow; [#119](https://github.com/Flowfin/jellyfin-plugin-discover/issues/119) decides whether the release path keeps that shape and [#124](https://github.com/Flowfin/jellyfin-plugin-discover/issues/124) checks the published package against what the gate built |
| `publish-beta.yml`          | adopt   | Same path as the stable one with a different channel, and it is what makes a beta channel real.                                                                                                                                                                                                           | [#121](https://github.com/Flowfin/jellyfin-plugin-discover/issues/121)                                                                                                                                                                                                                                                                                 |
| `publish-failure-alert.yml` | adopt   | A publish that fails and tells nobody is the failure mode a release process is for.                                                                                                                                                                                                                       | [#121](https://github.com/Flowfin/jellyfin-plugin-discover/issues/121)                                                                                                                                                                                                                                                                                 |
| `publish-jf12-beta.yml`     | defer   | It exists because that gate carries two server lines; whether this one does is question 1 on [#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) and is unanswered.                                                                                                                        | [#31](https://github.com/Flowfin/jellyfin-plugin-discover/issues/31) carries the second line in the build, and the channel it would publish to is [#119](https://github.com/Flowfin/jellyfin-plugin-discover/issues/119)                                                                                                                               |
| `publish-jf12-stable.yml`   | defer   | Same workflow for the stable channel, and the same unanswered question decides it.                                                                                                                                                                                                                        | as above                                                                                                                                                                                                                                                                                                                                               |
| `regenerate-manifest.yml`   | adopt   | The manifest has to be rebuildable on demand or a bad one can only be fixed by hand.                                                                                                                                                                                                                      | [#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120)                                                                                                                                                                                                                                                                                 |
| `scorecard.yml`             | adopted | Already here, and its push trigger names a branch this repository does not have, so that half of it never runs.                                                                                                                                                                                           | present as `scorecard.yml`, with the trigger in [#28](https://github.com/Flowfin/jellyfin-plugin-discover/issues/28)                                                                                                                                                                                                                                   |
| `stryker-mutation.yml`      | adopt   | Coverage says a line ran; this says a test would have failed had the line been wrong, and the code worth asking that of is the catalogue and the shelves.                                                                                                                                                 | [#36](https://github.com/Flowfin/jellyfin-plugin-discover/issues/36), which waits on that code existing                                                                                                                                                                                                                                                |
| `unicode-guard.yml`         | adopted | Already here, and one of the three checks that stand behind a merge today.                                                                                                                                                                                                                                | present as `unicode-guard.yml`                                                                                                                                                                                                                                                                                                                         |
| `wiki-lint.yml`             | decline | It lints a repository wiki, and the documentation for this plugin lives in `docs/` and the README instead, which is what M10 writes.                                                                                                                                                                      | nothing; [#117](https://github.com/Flowfin/jellyfin-plugin-discover/issues/117) and [#118](https://github.com/Flowfin/jellyfin-plugin-discover/issues/118) carry where documentation goes                                                                                                                                                              |
| `zizmor.yml`                | adopted | Already here, auditing the workflows this table is about, and its push trigger names the same absent branch as `scorecard.yml`.                                                                                                                                                                           | present as `zizmor.yml`, with the trigger in [#28](https://github.com/Flowfin/jellyfin-plugin-discover/issues/28)                                                                                                                                                                                                                                      |

## Checks here that the target does not run

| Here                         | Verdict         | Why, in one line                                                                                                                                                                                                                                                                                                                      |
| ---------------------------- | --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `branch-name-exists.yml`     | keep            | Two triggers and one upload condition here named a branch this repository does not have, so two audits never fired on a push and one sent its findings nowhere; the target has no such shape to refuse.                                                                                                                               |
| `changelog.yaml`             | keep, and unrun | Inherited from the plugin template, manual dispatch only since its push trigger was removed, and [#119](https://github.com/Flowfin/jellyfin-plugin-discover/issues/119) is where the release process it belongs to is decided.                                                                                                        |
| `changelog-entry.yml`        | keep, separate  | A version bump with no line saying what changed is a build nobody can tie a report to. The target reaches the same end inside its own pr-hygiene workflow; kept apart here so that one mistake reds one check, and `pr-hygiene.yml` says so at the leg that is not in it.                                                             |
| `command-dispatch.yaml`      | keep            | Inherited from the template; it turns a comment into a workflow dispatch and is the front half of `command-rebase.yaml`.                                                                                                                                                                                                              |
| `command-rebase.yaml`        | keep            | Inherited from the template; it rebases a pull request on request and costs nothing when nobody asks.                                                                                                                                                                                                                                 |
| `documented-commands.yml`    | keep            | A page here pastes the command behind every fact it states, and nothing re-ran those commands, so a page could go on printing an answer its own block had stopped giving; the target states its facts in prose and has no blocks to re-run.                                                                                           |
| `fuzz-the-source-reader.yml` | keep, separate  | The target fuzzes its authentication parsers because that is where untrusted bytes enter it; the same technique against a different door here, the source response reader whose output is written into an operator's library, on a cadence no merge waits for ([#37](https://github.com/Flowfin/jellyfin-plugin-discover/issues/37)). |
| `gate-parity.yml`            | keep            | This page is the only place the difference between the two gates is argued, and nothing read it, so it drifted against the directory it describes; the target has one gate and nothing to hold a table against.                                                                                                                       |
| `own-repository-name.yml`    | keep            | Two workflows here declared they were running on the template's repository and their jobs silently never ran; nothing on the target has that shape to refuse.                                                                                                                                                                         |
| `plugin-loads.yml`           | keep            | The target proves a login works; this proves the packaged plugin loads at all on each targeted line, which is the claim that matters when there is no login.                                                                                                                                                                          |
| `source-terms.yml`           | keep            | This plugin takes data from third parties under terms, and the target takes none, so nothing there has a reason to refuse an adapter whose terms were never written down.                                                                                                                                                             |
| `sync-labels.yaml`           | keep            | Inherited from the template; it keeps the label set in step and touches nothing a merge depends on.                                                                                                                                                                                                                                   |

The two tables account for every workflow file in this repository. Sixteen are
named in the first table's last column as the counterpart of a target workflow,
and the twelve above are the rest:

    ls .github/workflows | wc -l
    28

It read fourteen and twenty-three, and then fifteen and nine.
`abi-matches-the-line.yml` landed after the tables were written and was named on
neither of them, so the sentence claiming they accounted for everything was
false while the command under it printed a number nobody had re-run. It is named
in the `dotnet.yml` row rather than in the second table, because the ABI floor is
a job of that workflow on the target rather than a check the target does not run.

Those two corrections were both made by hand, months apart, by somebody who
happened to run the command. `gate-parity.yml` is what reads the claim now: a
workflow file this page never names reds the gate, a row in the second table
naming a file that is not there reds it, and the number above is compared with
the directory rather than trusted. What it holds is this repository's half. The
first table's other end is the target gate, which is another repository over the
network, so whether those rows still describe it stays the command at the top of
this page and a person to run it.

## What this table is not

It is a decision about which checks this repository wants, not a measurement of
what it runs. Whether an adopted row has actually landed is the state of the
issue it names, and whether a landed check blocks a merge is the required set,
which is printed by the command at the top rather than repeated here.

Nothing in it says an adopted check bites. That is
[#39](https://github.com/Flowfin/jellyfin-plugin-discover/issues/39), one guard
at a time, and a row here saying `adopted` means the workflow exists rather than
that it was proven to refuse anything.
