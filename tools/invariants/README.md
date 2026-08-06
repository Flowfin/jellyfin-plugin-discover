# Invariants

The rules in this directory are this plugin's own. They are not a general code
scanner and they hold nothing a scanner would know about: each one is a property
this codebase has decided to have, written as a pattern that finds the shape
that breaks it.

Nothing here is downloaded. The runner is `run.sh`, the rules are the files in
`rules/`, and the only tools it uses are the ones a checkout already needs.

## Adding an invariant

Add a rule file and a fixture. The workflow is not edited, and neither is the
runner.

    rules/<id>.rule
    fixtures/<id>/<anything>.cs

The rule file carries five fields, each at the start of a line, and then prose
saying what failure the rule prevents.

    Id: no-wall-clock
    Pattern: <extended regular expression, as git grep -E reads it>
    Subject: <git pathspecs the pattern is run against>
    Except: <git pathspecs excluded from the subject, or the word none>
    Issue: <the issue this invariant comes from>

The fixture is a file that breaks the rule, and it is not compiled: `tools/` is
outside every project in the solution, so a fixture is text the build never
reads.

## What the runner refuses

Four legs, and every one of them runs on every invocation.

1. A rule that does not fire on its own fixture. A rule nobody has watched
   refuse anything is a claim rather than a control, so the proof is not a run
   somebody did once and wrote down; it is a leg of the check itself.
2. A rule that fires on another rule's fixture. Each fixture breaks exactly one
   invariant, so a pattern broad enough to catch a neighbour is refused before
   it starts refusing real work for the wrong reason.
3. A rule that fires on the tracked tree. This is the leg that refuses a change,
   and the other three exist so that this one can be trusted when it is silent.
4. A rule with no fixture, or a fixture with no rule. Both directions fail, so a
   rule cannot be added without its proof and a fixture cannot outlive the rule
   it proves.

## What the runner cannot do

It matches text, one line at a time. A violation spread across two lines, a name
that reaches the forbidden thing indirectly, or a call assembled at run time are
all invisible to it. That is the bound on every rule here and it is not a
property of any single one.

It reads the tracked tree, so a file that is not committed is not judged.

A rule is only as good as its pattern, and nothing here says a pattern describes
the invariant its prose claims. Legs 1 and 2 prove the pattern catches one
written example and not the others. They do not prove it catches every way the
invariant can be broken, and no leg here could.

## Invariants that do not exist yet

`not-yet.md` holds the ones whose subject has not arrived. Each names the issue
that will bring the subject and therefore the rule. That file is prose, and
nothing refuses an invariant that belongs on it and is not there.
