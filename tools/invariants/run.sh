#!/usr/bin/env bash
#
# The invariant lint. Reads every rule in rules/, and refuses four things:
#
#   1. a rule that does not fire on its own fixture
#   2. a rule that fires on another rule's fixture that its own Subject reaches
#   3. a rule that fires on the tracked tree
#   4. a rule with no fixture, or a fixture with no rule
#   5. an exception that is a filename rather than a path, unless the rule
#      declares that glob on purpose
#   6. an exception that names no tracked file
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
  except_glob=$(field "$rule" Except-Glob)
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

  # The exceptions as both of the legs below need them. Leg 3 excludes the
  # fixtures on top of these; leg 2 is the leg that reads them.
  leg2_excludes=""
  if [ "$except" != "none" ]; then
    leg2_excludes="$except"
  fi

  # Leg 2: the rule fires on no other rule's fixture that its own Subject
  # reaches. A pattern broad enough to reach such a neighbour would start
  # refusing real work for a reason its prose does not name, and that is refused
  # here exactly as it was before.
  #
  # A fire on a fixture the Subject does not reach is a different thing and is
  # printed rather than refused (#316). What a rule refuses in the tree is
  # decided at leg 3, which is the only place the Subject is applied, so a
  # pattern reaching a file outside it refuses nothing extra. Refusing the rule
  # for that breadth refuses it for a property it does not have, and that is why
  # a rule discriminating by where a line sits could not be written here at all:
  # such a rule reads a shape that is ordinary in every other fixture.
  #
  # The Subject is applied at each fixture's own path rather than at the path a
  # fixture stands for, because the path is the only thing about a fixture this
  # runner can read. A fixture stored where no Subject reaches is therefore
  # compared by nobody, which is a property of the layout rather than of this
  # leg, and the count below is what keeps it readable.
  set -f
  # shellcheck disable=SC2086
  subject_files=$(git ls-files -- $subject $leg2_excludes)
  set +f
  compared=$(printf '%s\n' "$subject_files" |
    awk -v p="$rel_fixtures/" -v own="$rel_fixtures/$id/" \
      'index($0, p) == 1 && index($0, own) != 1 { n++ } END { print n + 0 }')

  scan "$pattern" -- "$rel_fixtures" ":!$rel_fixtures/$id"
  inside=""
  outside=""
  while IFS= read -r hit; do
    [ -n "$hit" ] || continue
    if printf '%s\n' "$subject_files" | grep -qxF -- "${hit%%:*}"; then
      inside="$inside$hit
"
    else
      outside="$outside$hit
"
    fi
  done <<< "$SCAN_OUT"

  if [ -n "$inside" ]; then
    echo "FAIL  $id: leg 2: the pattern also fires on another rule's fixture inside this rule's subject:"
    printf '%s' "$inside" | sed 's/^/        /'
    fail=1
  elif [ "$compared" -eq 0 ]; then
    echo "ok    $id: leg 2: no other rule's fixture is inside this rule's subject, so nothing was compared."
  else
    echo "ok    $id: leg 2: silent on all $compared other fixture files inside this rule's subject."
  fi
  if [ -n "$outside" ]; then
    echo "note  $id: leg 2: the pattern fires outside this rule's subject, where leg 3 never reads it:"
    printf '%s' "$outside" | sed 's/^/        /'
  fi

  # Leg 3: the rule is silent on the tracked tree. The fixtures are excluded
  # here and nowhere else: they exist to break the rules.
  excludes=":!$rel_fixtures $leg2_excludes"
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

  # Leg 5: an exception is a path rather than a name (#387). Every exception in
  # this directory once began with a wildcard, which makes it a filename suffix
  # rather than a place: a file anywhere in the tree whose name ends the same
  # way is outside the invariant, whatever it does and wherever it sits, and the
  # person adding that file chooses the name. A path is not something a later
  # file may choose, so an exception written as one cannot be taken that way.
  #
  # A glob is still the right shape where the exception is a convention rather
  # than a seam - a set nobody can enumerate today, whose membership is itself
  # what the rule is about. Such a glob is declared in Except-Glob, and the
  # declaration is what makes keeping it a decision somebody took rather than
  # one nobody looked at. The field carries no reason; the rule's prose does,
  # and this leg cannot read that.
  named=""
  undeclared_glob=""
  set -f
  if [ "$except" != "none" ]; then
    for spec in $except; do
      case ${spec#:!} in
        \**)
          case " $except_glob " in
            *" $spec "*) ;;
            *) named="$named $spec" ;;
          esac
          ;;
      esac
    done
  fi
  # The other direction: a declared glob that is no longer an exception. A
  # register that only fails one way rots into a list of permissions nobody
  # granted.
  for spec in $except_glob; do
    case " $except " in
      *" $spec "*) ;;
      *) undeclared_glob="$undeclared_glob $spec" ;;
    esac
  done
  set +f
  if [ -n "$named" ] || [ -n "$undeclared_glob" ]; then
    if [ -n "$named" ]; then
      echo "FAIL  $id: leg 5: an exception is a filename suffix rather than a path, so a later file can name its way out of this invariant:$named"
      echo "        Write it as the path it means, or declare it in Except-Glob and say in this rule why the glob is the right shape."
    fi
    if [ -n "$undeclared_glob" ]; then
      echo "FAIL  $id: leg 5: Except-Glob declares a pathspec that Except does not carry:$undeclared_glob"
    fi
    fail=1
  elif [ "$except" = "none" ]; then
    echo "ok    $id: leg 5: nothing is excepted."
  else
    echo "ok    $id: leg 5: every exception is a path or a declared glob."
  fi

  # Leg 6: an exception names something (#387). A carve-out that matches no
  # tracked file is either a rename that left its rule behind or a seam that
  # never arrived, and in both cases the rule is narrower or wider than its own
  # prose while every other leg stays green. It is also what makes a path safe
  # to write here: a project directory renamed under an anchored exception is
  # refused on this line, by name, rather than being diagnosed through leg 3
  # reporting that the tree breaks an invariant nobody broke.
  dangling=""
  if [ "$except" != "none" ]; then
    set -f
    for spec in $except; do
      # shellcheck disable=SC2086
      if [ -z "$(git ls-files -- ${spec#:!})" ]; then
        dangling="$dangling $spec"
      fi
    done
    set +f
  fi
  if [ -n "$dangling" ]; then
    echo "FAIL  $id: leg 6: an exception names no tracked file, so this rule carves out nothing and says it does:$dangling"
    fail=1
  elif [ "$except" = "none" ]; then
    echo "ok    $id: leg 6: nothing is excepted."
  else
    echo "ok    $id: leg 6: every exception names a tracked file."
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

echo "Every invariant fires on its own fixture, on no other, and on nothing in the tree, and excepts only paths that exist:$ids"
