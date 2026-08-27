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
2. A rule that fires on another rule's fixture **that its own `Subject`
   reaches**. Each fixture breaks exactly one invariant, so a pattern broad
   enough to catch such a neighbour is refused before it starts refusing real
   work for the wrong reason.

   What it compares is every fixture file outside this rule's own fixture
   directory that `Subject` matches and `Except` does not, judged at the
   fixture's own path. Leg 3 excludes the fixtures from the tree it reads, so
   this is the only leg that reads them, and the count of what it compared is
   on the line it prints: a rule whose subject reaches none of its neighbours'
   fixtures says so rather than reporting a pass it did not earn. Which rules
   are in that state is printed by the run rather than listed here, and it
   moves whenever a fixture is added.

   What it no longer refuses is a fire on a fixture the `Subject` does not
   reach. That fire costs nothing, because `Subject` is applied at leg 3 and
   nowhere else, so the pattern is never run against such a file in the tree.
   The fire is printed as a `note` rather than dropped, so the breadth stays
   visible. Refusing it refused a rule for a property it does not have, and it
   made a rule that discriminates by where a line sits impossible to write at
   all: such a rule's pattern reads a shape that is ordinary in the fixtures of
   the rules that discriminate by what a line says. #316 is where that was
   argued and changed.

   Its bound is the fixture's path. This leg asks `Subject` about where the
   fixture is stored rather than about the file it stands for, because the path
   is the only thing here that can be read. A fixture stored where no other
   rule's subject reaches is compared by nobody, which is a property of the
   layout rather than of the leg. Where this leg compares nothing, what is left
   holding an over-broad pattern is leg 3, which runs it inside the subject the
   rule declares; a pattern broad enough to reach real work there is still red,
   and one broad only outside the subject reaches no work to refuse.

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
written example and not the neighbours its own subject reaches. They do not
prove it catches every way the invariant can be broken, and no leg here could.

## Invariants that do not exist yet

`not-yet.md` holds the ones whose subject has not arrived. Each names the issue
that will bring the subject and therefore the rule. That file is prose, and
nothing refuses an invariant that belongs on it and is not there.
