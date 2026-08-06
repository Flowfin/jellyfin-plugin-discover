#!/usr/bin/env bash
#
# The invariant lint. Reads every rule in rules/, and refuses four things:
#
#   1. a rule that does not fire on its own fixture
#   2. a rule that fires on another rule's fixture
#   3. a rule that fires on the tracked tree
#   4. a rule with no fixture, or a fixture with no rule
#
# Nothing is downloaded and nothing is installed. git and the shell are all it
# uses, which are what a checkout already needs.
#
# Every leg is reported before the run ends, so a red run names every reason
# rather than the first one.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
rules_dir="$here/rules"
fixtures_dir="$here/fixtures"
repo=$(git -C "$here" rev-parse --show-toplevel)
cd "$repo"

# Written relative to the repository root, because every pathspec below is
# resolved from there, and a rule file that had to know where it lived would be
# a second place the layout is written down. Asked of git rather than cut off
# the absolute path: a checkout on Windows spells the two in different ways and
# a prefix that does not match leaves the exclusions silently doing nothing.
rel_here=$(git -C "$here" rev-parse --show-prefix)
rel_here=${rel_here%/}
rel_fixtures="$rel_here/fixtures"
rel_rules="$rel_here/rules"

fail=0

field() {
  # The first line whose start is "<name>: ". A missing field is refused rather
  # than defaulted, so a rule cannot be half written.
  sed -n "s/^$2: //p" "$1" | head -1
}

# git grep exits 1 when nothing matched and 2 or more when it could not read. A
# scanner error taken for a clean tree is this check reporting that a tree it
# never read is fine, so it fails closed. The result comes back in a variable
# rather than through a command substitution, because an exit inside one would
# end a subshell and leave the run going.
scan() {
  local rc=0
  SCAN_OUT=$(git grep -nIE "$@") || rc=$?
  if [ "$rc" -ge 2 ]; then
    echo "::error::invariants scanner error (git grep exit $rc) - failing closed instead of assuming there is nothing to find."
    exit 1
  fi
}

rule_files=$(find "$rules_dir" -name '*.rule' | sort)
if [ -z "$rule_files" ]; then
  echo "::error::No rule exists in $rules_dir. An invariant lint with no invariants passes everything."
  exit 1
fi

ids=""
while IFS= read -r rule; do
  [ -n "$rule" ] || continue
  base=${rule##*/}
  id=${base%.rule}
  ids="$ids $id"

  pattern=$(field "$rule" Pattern)
  subject=$(field "$rule" Subject)
  except=$(field "$rule" Except)
  issue=$(field "$rule" Issue)

  if [ -z "$pattern" ] || [ -z "$subject" ] || [ -z "$except" ] || [ -z "$issue" ]; then
    echo "FAIL  $id: the rule file is missing one of Pattern, Subject, Except or Issue."
    fail=1
    continue
  fi

  # Leg 4a: a rule with no fixture.
  if [ ! -d "$fixtures_dir/$id" ] || [ -z "$(find "$fixtures_dir/$id" -type f)" ]; then
    echo "FAIL  $id: leg 4: no fixture in $rel_fixtures/$id, so nothing proves this rule fires."
    fail=1
    continue
  fi

  # Leg 1: the rule fires on its own fixture.
  scan "$pattern" -- "$rel_fixtures/$id"
  if [ -z "$SCAN_OUT" ]; then
    echo "FAIL  $id: leg 1: the pattern does not fire on its own fixture in $rel_fixtures/$id."
    fail=1
  else
    echo "ok    $id: leg 1: fires on its own fixture."
  fi

  # Leg 2: the rule fires on no other rule's fixture. A pattern broad enough to
  # reach a neighbour would start refusing real work for a reason its prose does
  # not name.
  scan "$pattern" -- "$rel_fixtures" ":!$rel_fixtures/$id"
  if [ -n "$SCAN_OUT" ]; then
    echo "FAIL  $id: leg 2: the pattern also fires on another rule's fixture:"
    printf '%s\n' "$SCAN_OUT" | sed 's/^/        /'
    fail=1
  else
    echo "ok    $id: leg 2: silent on every other fixture."
  fi

  # Leg 3: the rule is silent on the tracked tree. The fixtures are excluded
  # here and nowhere else: they exist to break the rules.
  excludes=":!$rel_fixtures"
  if [ "$except" != "none" ]; then
    excludes="$excludes $except"
  fi
  # Word splitting is what carries several pathspecs out of one field. Globbing
  # is turned off across the split so a pathspec like *.cs reaches git as it was
  # written instead of being expanded against the working directory first.
  set -f
  # shellcheck disable=SC2086
  scan "$pattern" -- $subject $excludes
  set +f
  if [ -n "$SCAN_OUT" ]; then
    echo "FAIL  $id: leg 3: the tracked tree breaks this invariant (#$issue):"
    printf '%s\n' "$SCAN_OUT" | sed 's/^/        /'
    fail=1
  else
    echo "ok    $id: leg 3: silent on the tracked tree."
  fi
done <<< "$rule_files"

# Leg 4b: a fixture with no rule. A fixture that outlives its rule is a file
# breaking an invariant nothing holds any more, which reads as covered.
for dir in "$fixtures_dir"/*/; do
  [ -d "$dir" ] || continue
  name=$(basename "$dir")
  if [ ! -f "$rules_dir/$name.rule" ]; then
    echo "FAIL  $name: leg 4: a fixture with no rule in $rel_rules/$name.rule."
    fail=1
  fi
done

if [ "$fail" -ne 0 ]; then
  echo "::error::An invariant of this plugin is broken, or a rule cannot show that it bites. tools/invariants/README.md says what each leg refuses and what none of them can do."
  exit 1
fi

echo "Every invariant fires on its own fixture, on no other, and on nothing in the tree:$ids"
