Closes #

<!--
Delete a heading below only when it genuinely does not apply, and say so where
it stood. An empty section reads as nothing to say; a missing one reads as not
asked.

Before pushing:
  npx --yes prettier@3.6.2 --check .
  tools/invariants/run.sh
  dotnet build --configuration Release && dotnet test --configuration Release --no-build
Every commit needs a Signed-off-by line matching its author. See CONTRIBUTING.md.
-->

## What was wrong

What the tree did before this, with the command that shows it and the output
that command printed. Not a description of the change.

## What changed

What the tree does now, and why this shape rather than the neighbouring one.

## Proof that it bites

For a guard, a check or a rule: the run in which it refused the thing it names,
and the run in which the corrected tree passed. A guard nobody has watched refuse
anything is a claim rather than a control.

For a change that is not a guard, say which existing check would have caught a
mistake in it, or say that none would.

## The means

One sentence naming what this is made of and why that fits. A shell step, a C#
test, a document, a workflow. A means carried over from the last change is an
assumption about this one.

## Server version and client

For anything a user can see: which server line this was tried against, and which
client. Say "not tried against a server" where that is the case rather than
leaving it out.

## Not covered

What this deliberately leaves undone, and where each of those is picked up. A
section that says nothing here is the one a reader will check.

## Review

Who read this besides the author. Where nobody did, say so in that many words and
leave the evidence above to stand in place of a reader.
