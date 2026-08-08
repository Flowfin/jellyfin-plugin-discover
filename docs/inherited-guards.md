# The guards that arrived with the repository

Five workflows are here because this repository was made from a template with
them already on it, rather than because somebody found a reason for them in this
tree: the workflow audit, the Trojan Source guard, the supply-chain self-audit,
the dependency review and the sign-off gate.

A guard nobody has watched refuse anything is a claim about behaviour. This page
is the run in which each one refused something it names, so that the silence of a
green gate afterwards means the guard is quiet rather than absent.

Every run below is on this repository. Three were produced by one commit written
to trip them and removed again in the commit after it; two were not, for
different reasons, and those are the last two sections.

## The Trojan Source guard refused a right-to-left override

What it was given: one line of a markdown file carrying U+202E, the character
that reorders what a reader sees without changing what a compiler reads.

    https://github.com/iderex/jellyfin-plugin-discover/actions/runs/31111270257/job/92649385372
    tools/guard-proof/near-miss.md:8:    the bytes after this arrow are overridden: <U+202E>gnitset si sihT
    ##[error]Dangerous bidirectional/invisible Unicode found (Trojan Source, CVE-2021-42574). Remove these control characters.

The character itself is written as `<U+202E>` in the line above and nowhere on
this page as itself. That elision is not tidiness: the guard greps every tracked
text file, so a page pasting the literal would refuse the tree it is documenting.
The run id is what a reader follows to see the byte.

The guard names the file and the line rather than only the tree, which is the
part worth having: the same character in a file nobody edited that week would
otherwise cost an afternoon to find.

What this run does not show. The workflow distinguishes three exit codes from
`git grep` and fails closed on the third, a scanner error, rather than reading it
as a clean tree. Nothing here exercised that branch. Making `git grep` exit 2 or
above needs the scanner or the tree to be broken rather than the input to be
hostile, and no way to do that from a commit was found. That branch is therefore
read but not run, and this sentence is the whole of what is known about it.

## The workflow audit refused an action pinned to a tag

What it was given: a workflow file, triggered manually and by nothing else, whose
one step referenced `actions/checkout` by tag rather than by commit.

    https://github.com/iderex/jellyfin-plugin-discover/actions/runs/31111290945/job/92649459556
    INFO audit: zizmor: 🌈 completed ./.github/workflows/guard-proof.yml
    error[unpinned-uses]: unpinned action reference
      --> ./.github/workflows/guard-proof.yml:25:15
       = help: audit documentation → https://docs.zizmor.sh/audits/#unpinned-uses

This is the finding `zizmor.yml`'s own comment lists first, and the audit reads a
workflow that never runs, which is the property that matters: a file is judged
for what it says, not for whether anything triggered it.

## The dependency review refused a package with a published advisory

What it was given: a direct reference to `System.Security.Cryptography.Xml`
4.5.0 in the test project, with the lock file regenerated so the dependency graph
carried the same package the project declared.

    https://github.com/iderex/jellyfin-plugin-discover/actions/runs/31111290460/job/92649458344
    Jellyfin.Plugin.Template.Tests/Jellyfin.Plugin.Template.Tests.csproj » System.Security.Cryptography.Xml@4.5.0 – .NET Information Disclosure Vulnerability (moderate severity)
      ↪ https://github.com/advisories/GHSA-vh55-786g-wjwj
    ##[error]Dependency review detected vulnerable packages.

The workflow is set to `fail-on-severity: low`, so a moderate finding is well
inside what it refuses, and the run refused it.

One thing surfaced that was not being looked for. The restore refuses the same
package before any of this, because `NuGetAudit` is on at level `low` and the
audit warnings are errors in this tree:

    DOTNET_CLI_UI_LANGUAGE=en dotnet restore Jellyfin.Plugin.Template.Tests/Jellyfin.Plugin.Template.Tests.csproj --force-evaluate
    error NU1902: Warning As Error: Package 'System.Security.Cryptography.Xml' 4.5.0 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-vh55-786g-wjwj

The language is forced on the command rather than translated afterwards, because
the SDK on the machine this ran on prints in the locale it finds and a rendered
output is no longer the output. The advisory it names is the one the dependency
review named, reached by a different reader.

That is a different control from the dependency review and it is not one of the
five. It is recorded here because it is the reason the lock file for the
near-miss had to be written with `-p:NuGetAudit=false`, and because it means the
build and test legs were red on that commit for a reason that is not the one this
page is about.

## The sign-off gate refused a real commit, not a fixture

This one was not staged. A commit landed on a branch with a `Signed-off-by`
trailer naming an address that is not its author's, and the gate refused it:

    gh api repos/iderex/jellyfin-plugin-discover/commits/6815c59f353d01c805ccd118a8b81fcfe9badd51/check-runs --jq '.check_runs[] | select(.name=="DCO sign-off") | .conclusion'
    failure

    https://github.com/iderex/jellyfin-plugin-discover/actions/runs/31109878855/job/92644560252
    FAIL  6815c59f353d01c805ccd118a8b81fcfe9badd51 is missing: Signed-off-by: Nils Lehnen <30603423+iderex@users.noreply.github.com>
    ok    94308ed98a30e6349268ba784c0a6f3e66206bee

The same two changes, cherry-picked with the trailer corrected and pushed as
different commits, pass:

    gh api repos/iderex/jellyfin-plugin-discover/commits/22906201b9735c2e4f939cb94a5e8cebf6a4fe85/check-runs --jq '.check_runs[] | select(.name=="DCO sign-off") | .conclusion'
    success

So the gate is proven in both directions, and the refusal is sharper than a
fixture would have been: it caught a trailer that looks right to a person reading
it, because the name matches and only the address does not.

## The supply-chain self-audit cannot be made to refuse anything

`scorecard.yml` has no step that fails on a finding. It runs the analysis, keeps
the result as an artefact, and uploads it to the code-scanning tab. A score is
published; nothing is refused. That is the design rather than a gap in it, and it
means there is no input that turns this workflow red for the reason it exists.

It also does not run on a pull request at all, which its own comment states and
gives the reason for. Its triggers are a weekly schedule, a branch protection
change, and a push to `main`:

    gh api repos/iderex/jellyfin-plugin-discover --jq .default_branch
    master

This repository's default branch is `master`, so the push trigger names a branch
that does not exist here and never fires, and the job's own condition requires
the ref to be the default branch besides. What remains is the schedule. Which
workflows name a branch this repository does not have is #28's, and it is not
repaired here.

So the honest statement is that this guard is a report and not a control. Kept as
a report it is worth having; counted as one of the checks standing behind a
merge, it would be decoration.

## What this page does not say

It does not say the five guards are sufficient, or that what each one refuses is
the whole of what it should refuse. Each section is one input and one verdict.

It does not cover any check this repository added for itself. Those carry their
own proofs next to the rules they enforce.

The three staged runs were produced from a single commit and read afterwards. No
second person watched them.
