#!/usr/bin/env bash
#
# docs/gate-parity.md claims to account for every workflow file in this
# repository. Nothing read that claim, so it drifted: abi-matches-the-line.yml
# landed after the tables were written, was named on neither of them, and the
# sentence saying they accounted for everything stayed true-looking above a
# count nobody had re-run. It was found by hand, months later, and the same
# defect can arrive again the next time a workflow is added.
#
# This is the reader. Three legs, each reported before the run ends, so a red
# run names every reason rather than the first one.
#
#   1. A workflow with no mention. Every file in .github/workflows/ is named in
#      the page, in backticks, in either table or in the prose around them.
#   2. A row with no workflow. Every name in the first column of the second
#      table, which lists checks this repository runs that the target does not,
#      is a file that exists.
#   3. A count that has stopped counting. The number the page prints under
#      `ls .github/workflows | wc -l` is the number the tree has.
#
# Only leg 1 has ever fired in anger. Leg 2 is its other direction, and leg 3 is
# the line that was wrong beside it.
#
# Nothing is downloaded and nothing is installed. git, grep, sed and awk are
# what a checkout already needs.
#
# What it cannot do. It judges that a name appears, never that the row saying it
# is true, that the verdict beside it is the right one, or that the one line of
# reasoning says anything. It reads this repository only: whether the first
# table still describes the target gate is a comparison against another
# repository over the network, which is the command at the top of the page and
# is a person's job. And leg 3 anchors on the exact command text, so rewording
# that line fails this leg rather than silently skipping it, which is the
# direction a check about a stale claim should fail in.

set -euo pipefail

repo=$(git rev-parse --show-toplevel)
cd "$repo"

page=docs/gate-parity.md
dir=.github/workflows
count_command='ls .github/workflows | wc -l'

fail=0

if [ ! -f "$page" ]; then
  echo "::error::$page does not exist, so nothing accounts for the workflows in $dir."
  exit 1
fi

workflows=$(git ls-files -- "$dir/*")
if [ -z "$workflows" ]; then
  echo "::error::$dir holds no tracked file. A parity check with no workflows to account for passes everything."
  exit 1
fi

# Leg 1: a workflow with no mention.
while IFS= read -r path; do
  [ -n "$path" ] || continue
  base=${path##*/}
  # Backticks rather than a bare substring: the page names target workflows too,
  # and a name that only ever appeared inside a longer word would satisfy a
  # looser match without anybody having written a row.
  if grep -qF -- "\`$base\`" "$page"; then
    echo "ok    leg 1: $base is named in $page."
  else
    echo "FAIL  leg 1: $dir/$base is in the tree and $page never names it."
    fail=1
  fi
done <<< "$workflows"

# Leg 2: a row with no workflow. The section is bounded by its own heading and
# the next one, so a row added to the first table is not read here by accident.
rows=$(awk '/^## Checks here that the target does not run$/{inside=1;next} /^## /{inside=0} inside' "$page" \
  | sed -n 's/^| *`\([^`]*\)` *|.*/\1/p')
if [ -z "$rows" ]; then
  echo "FAIL  leg 2: no row was read out of the second table in $page, so this leg judged nothing."
  fail=1
else
  while IFS= read -r name; do
    [ -n "$name" ] || continue
    if [ -f "$dir/$name" ]; then
      echo "ok    leg 2: $page has a row for $name and the file is there."
    else
      echo "FAIL  leg 2: $page has a row for $name and $dir/$name does not exist."
      fail=1
    fi
  done <<< "$rows"
fi

# Leg 3: a count that has stopped counting.
printed=$(grep -A1 -F -- "$count_command" "$page" | sed -n 's/^ *\([0-9]\{1,\}\) *$/\1/p' | head -1)
actual=$(printf '%s\n' "$workflows" | wc -l | tr -d ' ')
if [ -z "$printed" ]; then
  echo "FAIL  leg 3: $page prints no number under '$count_command', so the count it claims cannot be read."
  fail=1
elif [ "$printed" != "$actual" ]; then
  echo "FAIL  leg 3: $page says '$count_command' prints $printed and it prints $actual."
  fail=1
else
  echo "ok    leg 3: $page says $printed workflows and the tree has $actual."
fi

if [ "$fail" -ne 0 ]; then
  echo "::error::docs/gate-parity.md no longer accounts for the workflows in this tree. Every workflow file carries a row or a mention on that page, every row in its second table names a file that exists, and the count it prints is the count the tree has."
  exit 1
fi

echo "$page accounts for every workflow in $dir, and its count is the tree's."
