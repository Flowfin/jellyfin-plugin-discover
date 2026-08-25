# Adopt or decline, for every workflow on the target gate

The target is the gate on `iderex/jellyfin-plugin-sso`, read at commit
`eae9feb2edcf94be926b0204162da98f51337c9c`, which was the head of that
repository's default branch when this table was written. It is not the head any
more, so what fixes the pin is the commit itself rather than whatever `main`
points at:

    gh api repos/iderex/jellyfin-plugin-sso/commits/eae9feb2edcf94be926b0204162da98f51337c9c --jq .sha
    eae9feb2edcf94be926b0204162da98f51337c9c

Where `main` stands today is a moving answer, so it is handed to the reader
rather than pasted. A value written under this one is wrong again the next time
the target takes a commit, and this page carried one:

    gh api repos/iderex/jellyfin-plugin-sso/commits/main --jq .sha

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

| Target workflow             | Verdict | Why, in one line                                                                                                                                                                                                                                                                                                                                              | Where it lands here                                                                                                                                                                                                                                                                                                                                    |
| --------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `build.yml`                 | adopt   | Packaging has to happen inside the gate or the first time a package is built is the release.                                                                                                                                                                                                                                                                  | present as `build.yaml`, calling this repository's own `build-run.yml`, from [#27](https://github.com/Flowfin/jellyfin-plugin-discover/issues/27); [#35](https://github.com/Flowfin/jellyfin-plugin-discover/issues/35) adds the bill of materials                                                                                                     |
| `codeql.yml`                | adopted | A scanner that reads this tree rather than reporting on another repository's name.                                                                                                                                                                                                                                                                            | present as `scan-codeql.yaml`, from [#29](https://github.com/Flowfin/jellyfin-plugin-discover/issues/29)                                                                                                                                                                                                                                               |
| `dco.yml`                   | adopted | Already here, refusing a commit with no sign-off matching its author.                                                                                                                                                                                                                                                                                         | present as `dco.yml`                                                                                                                                                                                                                                                                                                                                   |
| `dependency-review.yml`     | adopted | Already here, reading the pull request's dependency diff against the advisory database, which is a second reader on what `NuGetAudit` in `Directory.Build.props` already refuses at restore time.                                                                                                                                                             | present as `dependency-review.yml`                                                                                                                                                                                                                                                                                                                     |
| `dotnet.yml`                | adopt   | Build, test and coverage owned here rather than called from another organisation, plus its ABI floor job.                                                                                                                                                                                                                                                     | the test half is present as `test.yaml` calling `test-run.yml` and the build half as `build.yaml` calling `build-run.yml`, both from [#27](https://github.com/Flowfin/jellyfin-plugin-discover/issues/27); the ABI floor is present as `abi-matches-the-line.yml`, from [#30](https://github.com/Flowfin/jellyfin-plugin-discover/issues/30)           |
| `e2e-login.yml`             | decline | It drives a login round trip, and this plugin has nothing to log into: it adds no authentication surface at all.                                                                                                                                                                                                                                              | replaced by [#38](https://github.com/Flowfin/jellyfin-plugin-discover/issues/38), which proves the discover surface reaches a client instead; present as `discover-surface-appears.yml`, which asks a signed-in user's views for it                                                                                                                    |
| `fuzz.yml`                  | adopt   | This plugin parses responses from a third party, which is the one place here where a hostile input arrives from outside.                                                                                                                                                                                                                                      | [#37](https://github.com/Flowfin/jellyfin-plugin-discover/issues/37)                                                                                                                                                                                                                                                                                   |
| `manifest-freshness.yml`    | adopt   | A manifest that stops listing the newest release fails silently and only a user notices.                                                                                                                                                                                                                                                                      | [#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120)                                                                                                                                                                                                                                                                                 |
| `nightly-betas.yml`         | adopt   | A beta channel is worth nothing if nothing builds into it.                                                                                                                                                                                                                                                                                                    | [#121](https://github.com/Flowfin/jellyfin-plugin-discover/issues/121)                                                                                                                                                                                                                                                                                 |
| `opengrep.yml`              | adopted | The mechanism is the same and the invariants worth grepping for here are this plugin's own.                                                                                                                                                                                                                                                                   | present as `invariants.yml`, from [#33](https://github.com/Flowfin/jellyfin-plugin-discover/issues/33)                                                                                                                                                                                                                                                 |
| `perf-baseline.yml`         | decline | Its own header says it times a login round trip in-process, weekly and never on a pull request, and there is no login here to time. The cost that matters here is what a refresh does to a large library, which is one measurement with the library size beside it rather than a number taken every week.                                                     | nothing; [#194](https://github.com/Flowfin/jellyfin-plugin-discover/issues/194) holds that measurement and it is a document rather than a workflow. Added to the target after the pinned commit, which the note at the top of this page shows.                                                                                                         |
| `pr-hygiene.yml`            | adopted | It is the defect class no code scanner covers, and it is cheapest before the tenth pull request rather than after.                                                                                                                                                                                                                                            | present as `pr-hygiene.yml`, from [#32](https://github.com/Flowfin/jellyfin-plugin-discover/issues/32), publishing the check-run name `Deterministic pull-request hygiene`; the changelog leg the target keeps inside its own version stays in `changelog-entry.yml` below                                                                             |
| `prettier.yml`              | adopted | The analyzers read the C# and nothing read the rest.                                                                                                                                                                                                                                                                                                          | present as `format.yml`, from [#34](https://github.com/Flowfin/jellyfin-plugin-discover/issues/34)                                                                                                                                                                                                                                                     |
| `publish.yml`               | adopt   | A release path that is not the gate's build path is a package nobody checked.                                                                                                                                                                                                                                                                                 | present as `publish.yaml`, which is a call into the upstream project's reusable workflow; [#119](https://github.com/Flowfin/jellyfin-plugin-discover/issues/119) decides whether the release path keeps that shape and [#124](https://github.com/Flowfin/jellyfin-plugin-discover/issues/124) checks the published package against what the gate built |
| `publish-beta.yml`          | adopt   | Same path as the stable one with a different channel, and it is what makes a beta channel real.                                                                                                                                                                                                                                                               | [#121](https://github.com/Flowfin/jellyfin-plugin-discover/issues/121)                                                                                                                                                                                                                                                                                 |
| `publish-failure-alert.yml` | adopt   | A publish that fails and tells nobody is the failure mode a release process is for.                                                                                                                                                                                                                                                                           | [#121](https://github.com/Flowfin/jellyfin-plugin-discover/issues/121)                                                                                                                                                                                                                                                                                 |
| `publish-jf12-beta.yml`     | defer   | It exists because that gate carries two server lines. This one carries two as well: question 1 on [#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) was answered on 2026-08-24 with 10.11 and 12.0, the 10.11 artefact first. What defers this row now is the artefact rather than the question, because nothing here is built for 12.0 yet. | [#31](https://github.com/Flowfin/jellyfin-plugin-discover/issues/31) carries the second line in the build, and the channel it would publish to is [#119](https://github.com/Flowfin/jellyfin-plugin-discover/issues/119)                                                                                                                               |
| `publish-jf12-stable.yml`   | defer   | Same workflow for the stable channel, deferred by the same missing artefact rather than by a question.                                                                                                                                                                                                                                                        | as above                                                                                                                                                                                                                                                                                                                                               |
| `regenerate-manifest.yml`   | adopt   | The manifest has to be rebuildable on demand or a bad one can only be fixed by hand.                                                                                                                                                                                                                                                                          | [#120](https://github.com/Flowfin/jellyfin-plugin-discover/issues/120)                                                                                                                                                                                                                                                                                 |
| `scorecard.yml`             | adopted | Already here, and its push trigger names a branch this repository does not have, so that half of it never runs.                                                                                                                                                                                                                                               | present as `scorecard.yml`, with the trigger in [#28](https://github.com/Flowfin/jellyfin-plugin-discover/issues/28)                                                                                                                                                                                                                                   |
| `stryker-mutation.yml`      | adopt   | Coverage says a line ran; this says a test would have failed had the line been wrong, and the code worth asking that of is the catalogue and the shelves.                                                                                                                                                                                                     | [#36](https://github.com/Flowfin/jellyfin-plugin-discover/issues/36), which waits on that code existing                                                                                                                                                                                                                                                |
| `unicode-guard.yml`         | adopted | Already here, refusing the bidirectional control characters behind Trojan Source in every tracked text file; whether it blocks a merge is the required set, printed by the command at the top.                                                                                                                                                                | present as `unicode-guard.yml`                                                                                                                                                                                                                                                                                                                         |
| `wiki-lint.yml`             | decline | It lints a repository wiki, and the documentation for this plugin lives in `docs/` and the README instead, which is what M10 writes.                                                                                                                                                                                                                          | nothing; [#117](https://github.com/Flowfin/jellyfin-plugin-discover/issues/117) and [#118](https://github.com/Flowfin/jellyfin-plugin-discover/issues/118) carry where documentation goes                                                                                                                                                              |
| `zizmor.yml`                | adopted | Already here, auditing the workflows this table is about, and its push trigger names the same absent branch as `scorecard.yml`.                                                                                                                                                                                                                               | present as `zizmor.yml`, with the trigger in [#28](https://github.com/Flowfin/jellyfin-plugin-discover/issues/28)                                                                                                                                                                                                                                      |

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

## Which of these are wanted behind a merge

The two tables above answer whether a check is wanted at all. Whether a check
that landed stands behind a merge is a different question and this page did not
answer it, so a name outside the required set read the same whether it was left
out on purpose or never considered.

Nothing here is a reading of the required set. That set is a repository setting,
printed by the command at the top of this page rather than repeated below, and
raising it is typed at the settings page rather than landed from a branch. What
this section carries is the decision that typing needs, which is what
[#40](https://github.com/Flowfin/jellyfin-plugin-discover/issues/40) ends in a
hand-off for.

The rows below are every name this gate can publish on a pull request, which is
not the same thing as the names one pull request published. Three workflows
filter their `pull_request` trigger by path:

    git grep -n '^    paths:\|^    paths-ignore:' -- .github/workflows/
    .github/workflows/discover-surface-appears.yml:43:    paths:
    .github/workflows/plugin-loads.yml:29:    paths-ignore:
    .github/workflows/plugin-loads.yml:34:    paths-ignore:
    .github/workflows/scan-codeql.yaml:18:    paths-ignore:
    .github/workflows/scan-codeql.yaml:22:    paths-ignore:

so which names report moves with what a change touched, and a count off one
merged pull request is a floor rather than the set. Two merged pull requests
each publish twenty-four names and it is not the same twenty-four:

    gh pr checks 281 --repo Flowfin/jellyfin-plugin-discover --json name --jq '[.[].name] | unique | length'
    24

The same command on pull request 246 also answers 24, and the two lists differ by
one name in each direction:

    gh pr checks 246 --repo Flowfin/jellyfin-plugin-discover --json name --jq '[.[].name] | unique | length'
    24

`Reaches a signed-in user on 10.11` is on the second list and not on the first,
because 281 touched neither of the two paths its workflow runs for.
`Documented commands still print what is pasted` is on the first and not the
second, because that check landed after 246 was merged. The rows below are the
union, so a name that a particular change never asks for still carries a
decision rather than reading as one nobody took.

### Four of them cannot be required as they stand

A required entry matches a check-run name literally, so what the name is made of
decides whether the entry keeps meaning what it meant.

A name carrying a matrix value moves with the list that value comes from:

    git grep -n '^    name: .*matrix\.' -- .github/workflows/
    .github/workflows/abi-matches-the-line.yml:96:    name: Package for ${{ matrix.line }}
    .github/workflows/discover-surface-appears.yml:110:    name: Reaches a signed-in user on ${{ matrix.line }}
    .github/workflows/plugin-loads.yml:100:    name: Loads on ${{ matrix.line }}
    .github/workflows/scan-codeql.yaml:37:    name: Analyze ${{ matrix.language }}

An entry naming one of those four goes on passing the day a second line or a
second language is declared, and covers less than it did with nothing saying so.

A name no job in this tree spells is posted by the action rather than by a job.
`CodeQL` and `zizmor` are both published on a pull request and neither is
written down here:

    git grep -n 'name: CodeQL$\|name: zizmor$' -- .github/workflows/ ; echo "exit=$?"
    exit=1

`zizmor.yml` has a job beside its aggregate whose name is written down, so that
workflow has a name that can be required. `scan-codeql.yaml` does not: its only
job name carries the language matrix value, so nothing it publishes can be
required while the file is shaped that way.

One name is published by two workflows:

    git grep -n '^    name: Read the targeted server lines' -- .github/workflows/
    .github/workflows/discover-surface-appears.yml:57:    name: Read the targeted server lines
    .github/workflows/plugin-loads.yml:47:    name: Read the targeted server lines

A required entry of that name is satisfied by whichever of the two reports, and
which one it means is not decidable from the name. The two are not
interchangeable, and the second of them runs on a pull request only when one of
two paths changed:

    git grep -n -A6 '^  pull_request:' -- .github/workflows/discover-surface-appears.yml
    .github/workflows/discover-surface-appears.yml:40:  pull_request:
    .github/workflows/discover-surface-appears.yml-41-    branches:
    .github/workflows/discover-surface-appears.yml-42-      - master
    .github/workflows/discover-surface-appears.yml-43-    paths:
    .github/workflows/discover-surface-appears.yml-44-      - ".github/workflows/discover-surface-appears.yml"
    .github/workflows/discover-surface-appears.yml-45-      - "tools/discover-surface-appears.sh"
    .github/workflows/discover-surface-appears.yml-46-  workflow_dispatch:

So on a change touching neither path one of the two reports under that name, and
on a change touching them both do. That is also the failure recorded on
[#154](https://github.com/Flowfin/jellyfin-plugin-discover/issues/154) seen from
the allow-list side rather than the ignore-list side: a required check that
cannot report on a class of change refuses that change rather than passing it.

### One thing this section does not answer

Seven jobs here declare that they follow another:

    git grep -n '^    needs:' -- .github/workflows/
    .github/workflows/abi-matches-the-line.yml:97:    needs: lines
    .github/workflows/abi-matches-the-line.yml:184:    needs: [lines, package]
    .github/workflows/discover-surface-appears.yml:111:    needs: lines
    .github/workflows/plugin-loads.yml:101:    needs: lines
    .github/workflows/publish.yaml:240:    needs: gate
    .github/workflows/publish.yaml:384:    needs: build
    .github/workflows/publish.yaml:410:    needs: [build, attest]

What a check run of that kind reports when the job it follows has failed, and
whether a required entry naming it is then satisfied, has not been measured
here. Two rows below turn on that answer and say so rather than guessing it.

### The decision, one line each

| Published name                                             | Wanted behind a merge | Why, in one line                                                                                                                                  |
| ---------------------------------------------------------- | --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Analyze actions`                                          | no                    | The name carries the language matrix value, so the entry stops covering what it named the day that list moves.                                    |
| `Analyze csharp`                                           | no                    | The same, and `scan-codeql.yaml` publishes no job name without a matrix value in it.                                                              |
| `Audit workflows (zizmor)`                                 | yes                   | A job name written down in the tree, one deterministic answer, over the files every other check on this list runs from.                           |
| `call / build`                                             | yes                   | The job id is `call` and does not move with what it calls, and nothing else here is worth judging on a package that did not build.                |
| `call / test`                                              | yes                   | The same name shape, over the suite the rest of this gate is written around.                                                                      |
| `CodeQL`                                                   | no                    | No job here spells it, so it is the aggregate the action posts rather than a name a job publishes.                                                |
| `DCO sign-off`                                             | yes                   | It reads a property of the commits rather than making a judgement about them.                                                                     |
| `dependency-review`                                        | yes                   | It judges what a change adds, so its answer moves with the change rather than with the day it runs.                                               |
| `Deterministic pull-request hygiene`                       | yes                   | Both failing legs have one answer and no judgement, and it reports success without judging for a bot or an outside author, which it prints.       |
| `Documented commands still print what is pasted`           | yes                   | It reads the tree deterministically, with the bound that on a pull request it defers the blocks reading `origin/master` and says so.              |
| `Every package of this build declares its own ABI`         | not decided           | The only job of `abi-matches-the-line` whose name carries no declared line, and it follows the other two, which is the unanswered question above. |
| `Every source carries its terms page`                      | yes                   | It reads the tree deterministically, and the reason the check is here at all is in the `source-terms.yml` row above.                              |
| `Formatting outside the C#`                                | yes                   | One deterministic answer, and a formatting pass made afterwards rewrites files nobody reviewed.                                                   |
| `Loads on 10.11`                                           | no                    | The name carries the declared line, so the entry stays green and covers one line the day a second is declared.                                    |
| `No workflow names a branch this repository does not have` | yes                   | It reads the tree deterministically, and the failure it refuses is in the `branch-name-exists.yml` row above.                                     |
| `No workflow names another repository`                     | yes                   | The same, and the failure it refuses is in the `own-repository-name.yml` row above.                                                               |
| `Package for 10.11`                                        | no                    | The declared line again, and the last job of that workflow carries the same failure without it in the name.                                       |
| `Reaches a signed-in user on 10.11`                        | no                    | The declared line in the name again, and it reports only on a change touching one of the two paths its workflow runs for.                         |
| `Read the lines a package is built for`                    | not decided           | The last job of that workflow follows it, so whether requiring the later name covers this failure is the unanswered question above.               |
| `Read the targeted server lines`                           | no                    | Two workflows publish this exact name and the entry cannot say which of them it means.                                                            |
| `Reject Trojan Source Unicode`                             | yes                   | It reads tracked text deterministically, over a change that reads as something other than what it does.                                           |
| `The parity tables name every workflow`                    | yes                   | It reads the tree deterministically, over the page this section is written on.                                                                    |
| `This plugin's invariants hold`                            | yes                   | Every rule it runs is proven to fire on its own fixture and on no other.                                                                          |
| `Version bump carries a changelog entry`                   | yes                   | It reads the change deterministically, and the reason it is kept apart is in the `changelog-entry.yml` row above.                                 |
| `zizmor`                                                   | no                    | The aggregate beside the job named above, which is the name that can be required.                                                                 |

### What this section is not

It is a decision about which names are wanted, not a reading of which are
configured. The command at the top of this page is what says which of the rows
above are already typed at the settings page, and the rows marked `yes` are
meant to be a superset of that rather than a copy of it.

Nothing reads this section. `gate-parity.yml` holds the two tables above against
the workflow directory, and the first column it reads is a file name rather than
a check-run name, so a job renamed in this tree leaves a row here naming a check
nobody publishes and every run stays green.

## What this table is not

It is a decision about which checks this repository wants, not a measurement of
what it runs. Whether an adopted row has actually landed is the state of the
issue it names, and whether a landed check blocks a merge is the required set,
which is printed by the command at the top rather than repeated here.

Nothing in it says an adopted check bites. That is
[#39](https://github.com/Flowfin/jellyfin-plugin-discover/issues/39), one guard
at a time, and a row here saying `adopted` means the workflow exists rather than
that it was proven to refuse anything.
