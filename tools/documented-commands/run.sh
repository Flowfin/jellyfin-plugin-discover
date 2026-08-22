#!/usr/bin/env bash
#
# Every command a tracked page pastes still prints what is pasted under it
# (#273).
#
# This repository writes its documentation by putting the command that produced
# a fact next to the fact. That is worth nothing once the command stops printing
# what the page says it prints, and it goes wrong quietly: a reader who agrees
# with the sentence has no cause to run the block underneath it. Two issues
# record that nothing here re-runs those commands, each written after a page had
# drifted and been caught by hand. This is the reader.
#
# Nothing is downloaded and nothing is installed. git and the shell are what a
# checkout already needs.
#
# WHAT A BLOCK IS. Either an indented block whose first line is exactly four
# spaces followed by `git `, or a fenced block whose first line is `git `. The
# first line is the command and every line after it is the output the page
# claims that command prints. A block with nothing after the command is a
# command handed to the reader rather than a claim, and is not judged.
#
# A trailing `exit=N` is read two ways, because this corpus writes it two ways.
# Where the command ends in an `echo` of its own status, that line is ordinary
# output and is compared as such. Where it does not, the line is the exit status
# the page claims and is compared against the status the command returns.
#
# WHAT IS RUN. The command line as written, from the repository root, with
# stderr merged into stdout, because what a page pastes is what a terminal
# showed. A carriage return is stripped from the answer: that is the checkout's
# line-ending policy rather than anything the command found.
#
# WHAT IS REFUSED RATHER THAN RUN, and why the list is an allowlist. The first
# run of this check met a page that demonstrates git's line-ending behaviour by
# transcribing `git init` and `git config` in a scratch directory. It ran them,
# in this repository, and wrote a value into the clone's own config. Nothing was
# lost and the value was the one already inherited, but a check that reads pages
# for a living will meet that shape again. So the leading verb has to be one of
# a small set that only reads, a block that transcribes more than one command is
# not judged at all, and a command that reaches outside this checkout is
# refused. Every refusal is printed with its reason and counted, so a run that
# judged less than the whole set cannot be read as one that judged it all.
#
# WHAT IT CANNOT DO, and the first of these is the largest.
#
#   1. A claim written as prose with no command under it is not a subject here.
#      Whether a sentence asserts a fact at all is a judgement about meaning and
#      no reading of this tree makes it.
#   2. A block whose command reads another repository's checkout, needs the
#      network, or carries a placeholder is skipped. Those are the blocks that
#      quote the server's source at a tag, which is most of this corpus.
#   3. A page may elide part of an output on purpose. Nothing can tell this
#      check that, so such a block has to fall into one of the skipped kinds or
#      it is reported as a difference.
#   4. It judges that the bytes agree, never that the sentence above them is the
#      right conclusion to draw from those bytes.
#
# EXECUTION. This runs command text out of tracked Markdown, which is the same
# trust boundary as the test suite: a change that can add a command here can add
# one to a test. The allowlist is the guard against the accident above rather
# than against an attack, and the job that calls this holds a read-only token
# and no secret.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
repo=$(git -C "$here" rev-parse --show-toplevel)
cd "$repo"

rel_here=$(git -C "$here" rev-parse --show-prefix)
rel_here=${rel_here%/}
rel_fixtures="$rel_here/fixtures"

# Verbs that read and never write. A verb outside this set is refused rather
# than run, whatever else the line says.
readonly_verbs=" grep ls-files ls-tree log show blame diff rev-parse describe shortlog cat-file "

judged=0
skipped=0
fail=0
quiet=${QUIET:-0}

say() {
  [ "$quiet" = "1" ] || printf '%s\n' "$1"
}

names_an_absent_object() {
  local token
  for token in $(printf '%s\n' "$1" | tr -c '0-9a-f' ' '); do
    case ${#token} in
      7 | 8 | 9 | 1? | 2? | 3? | 40)
        git cat-file -e "${token}^{object}" 2>/dev/null && continue
        return 0
        ;;
    esac
  done
  return 1
}

# True when this checkout is standing on the mainline, so a command reading
# `origin/master` reads the same commit as the one being judged. On a pull
# request it is not, and a page changed together with the file it quotes would
# otherwise be refused for describing the tree it landed in.
on_the_mainline() {
  local remote="" head=""
  remote=$(git rev-parse --verify --quiet origin/master) || return 1
  head=$(git rev-parse --verify --quiet HEAD) || return 1
  [ "$remote" = "$head" ]
}

reason_to_skip() {
  local cmd=$1 verb=""

  # The reasons are asked in the order that gives a reader the true one. A
  # command reaching a Jellyfin tag is refused for that rather than for the verb
  # it happens to start with.
  case $cmd in
    *"v10.11.11"* | *"v12.0-rc4"*)
      echo "reads a Jellyfin checkout at a tag, which is not this repository"
      return
      ;;
    *"<"* | *">"*)
      echo "carries a placeholder or a redirection"
      return
      ;;
    *"fetch"* | *"ls-remote"*)
      echo "reaches the network"
      return
      ;;
    *'$('* | *'`'*)
      echo "substitutes a command into itself"
      return
      ;;
    *"sudo"* | *"rm "* | *"curl"* | *"wget"* | *"chmod"* | *"eval"* | *"xargs"*)
      echo "names something this check will not run"
      return
      ;;
  esac

  case $cmd in
    *"origin/"*)
      if ! on_the_mainline; then
        echo "reads origin/master and this checkout is not standing on it"
        return
      fi
      ;;
  esac

  verb=${cmd#git }
  verb=${verb%% *}
  case "$readonly_verbs" in
    *" $verb "*) ;;
    *)
      echo "starts with 'git $verb', which is not one of the verbs this check runs"
      return
      ;;
  esac

  if names_an_absent_object "$cmd"; then
    echo "names an object this repository does not carry"
    return
  fi

  echo ""
}

# A line of pasted output that is itself a command means the block transcribes a
# session rather than one command and its answer, and the second command would
# be read as the first one's output.
#
# The test is a program name followed by a space, and nothing looser. A path
# under `tools/` was in this list until it turned out to match the output of
# `git grep -l -- tools/invariants/rules/`, which is a page's evidence rather
# than a second command, and two blocks were being skipped for it.
looks_like_a_command() {
  case $1 in
    git\ * | printf\ * | echo\ * | od\ * | cd\ * | mkdir\ * | sed\ * | awk\ * | grep\ * | dotnet\ * | npx\ * | docker\ *)
      return 0
      ;;
  esac
  return 1
}

extract() {
  awk -v file="$1" '
    function flush(  i) {
      if (n == 0) return
      printf "B\t%s\t%d\n", file, start
      printf "C\t%s\n", body[1]
      for (i = 2; i <= n; i++) printf "O\t%s\n", body[i]
      printf "E\n"
      n = 0
    }
    {
      line = $0
      sub(/\r$/, "", line)
    }
    fenced {
      if (line ~ /^[[:space:]]*```/) { flush(); fenced = 0; next }
      if (n == 0 && line !~ /^git /) { fenced = 2 }
      if (fenced == 1) { body[++n] = line }
      next
    }
    /^[[:space:]]*```/ { flush(); fenced = 1; start = NR + 1; n = 0; next }
    indented {
      if (line ~ /^    [^ ]/ || line ~ /^     /) { body[++n] = substr(line, 5); next }
      flush(); indented = 0
    }
    /^    git / { indented = 1; start = NR; n = 0; body[++n] = substr(line, 5); next }
    { next }
    END { flush() }
  ' "$1"
}

judge() {
  local file=$1 line=$2 cmd=$3 want=$4 exit_want=$5
  local rc=0 got=""

  got=$(bash -c "$cmd" 2>&1) || rc=$?
  got=${got//$'\r'/}

  judged=$((judged + 1))

  if [ "$got" = "$want" ] && { [ -z "$exit_want" ] || [ "$exit_want" = "$rc" ]; }; then
    say "ok    $file:$line: still prints what is pasted under it."
    return 0
  fi

  fail=1
  printf 'FAIL  %s:%s: the command no longer prints what is pasted under it.\n' "$file" "$line"
  printf '        command: %s\n' "$cmd"
  printf '        the page carries:\n'
  [ -n "$want" ] && printf '%s\n' "$want" | sed 's/^/          /'
  [ -n "$exit_want" ] && printf '          exit=%s\n' "$exit_want"
  printf '        the command prints:\n'
  [ -n "$got" ] && printf '%s\n' "$got" | sed 's/^/          /'
  [ -n "$exit_want" ] && printf '          exit=%s\n' "$rc"
  return 0
}

read_blocks() {
  local file=$1
  local line="" cmd="" why=""
  local -a out=()

  finish() {
    local want="" exit_want="" i=0 transcript=0

    if [ "${#out[@]}" -eq 0 ]; then
      skipped=$((skipped + 1))
      say "skip  $file:$line: no output is pasted, so the block is a command handed to the reader."
      return
    fi

    # The status line, when the command did not echo it itself.
    case $cmd in
      *'exit=$?'*) ;;
      *)
        i=$((${#out[@]} - 1))
        case ${out[$i]} in
          exit=*)
            exit_want=${out[$i]#exit=}
            unset 'out[$i]'
            ;;
        esac
        ;;
    esac

    for i in "${!out[@]}"; do
      if looks_like_a_command "${out[$i]}"; then
        transcript=1
        break
      fi
      if [ -z "$want" ] && [ "$i" = "0" ]; then
        want=${out[$i]}
      else
        want=$want$'\n'${out[$i]}
      fi
    done

    if [ "$transcript" = "1" ]; then
      skipped=$((skipped + 1))
      say "skip  $file:$line: the block transcribes more than one command."
      return
    fi

    if [ "${#out[@]}" -eq 0 ] && [ -z "$exit_want" ]; then
      skipped=$((skipped + 1))
      say "skip  $file:$line: no output is pasted, so the block is a command handed to the reader."
      return
    fi

    why=$(reason_to_skip "$cmd")
    if [ -n "$why" ]; then
      skipped=$((skipped + 1))
      say "skip  $file:$line: $why."
      return
    fi

    judge "$file" "$line" "$cmd" "$want" "$exit_want"
  }

  while IFS= read -r record; do
    case $record in
      B$'\t'*)
        line=${record##*$'\t'}
        cmd=""
        out=()
        ;;
      C$'\t'*)
        cmd=${record#C$'\t'}
        ;;
      O$'\t'*)
        out+=("${record#O$'\t'}")
        ;;
      E)
        finish
        ;;
    esac
  done < <(extract "$file")
}

# Three legs, each reported before the run ends, so a red run names every reason
# rather than the first one. Leg 1 is what makes this check worth counting: a
# lint nobody has watched refusing is a green tick with no evidence behind it.
prove() {
  local script="$here/run.sh" bad=0 out=""

  if out=$(QUIET=1 bash "$script" "$rel_fixtures/breaks-the-rule.md" 2>&1); then
    echo "FAIL  leg 1: the fixture that breaks the rule was not refused."
    bad=1
  else
    case $out in
      *"breaks-the-rule.md"*"no longer prints what is pasted under it"*)
        echo "ok    leg 1: the fixture that breaks the rule is refused, and the refusal names it."
        ;;
      *)
        echo "FAIL  leg 1: the fixture that breaks the rule was refused for something other than its block:"
        printf '%s\n' "$out" | sed 's/^/        /'
        bad=1
        ;;
    esac
  fi

  if out=$(QUIET=1 bash "$script" "$rel_fixtures/holds-the-rule.md" 2>&1); then
    echo "ok    leg 2: the fixture that holds the rule is silent."
  else
    echo "FAIL  leg 2: the fixture that holds the rule was refused:"
    printf '%s\n' "$out" | sed 's/^/        /'
    bad=1
  fi

  if out=$(bash "$script" 2>&1); then
    echo "ok    leg 3: the tracked pages are silent."
    printf '%s\n' "$out" | tail -1 | sed 's/^/      /'
  else
    echo "FAIL  leg 3: a tracked page was refused:"
    printf '%s\n' "$out" | grep -A12 '^FAIL' | sed 's/^/        /'
    bad=1
  fi

  if [ "$bad" -ne 0 ]; then
    echo "::error::The documented-commands check did not hold its own three legs."
    exit 1
  fi

  echo "The check fires on the fixture that breaks the rule, is silent on the one that holds it, and is silent on the tracked pages."
}

if [ "${1:-}" = "--prove" ]; then
  prove
  exit 0
fi

if [ "$#" -gt 0 ]; then
  files=$(printf '%s\n' "$@")
  subject="the pages named on the command line"
else
  files=$(git ls-files -- '*.md' ":!$rel_fixtures/*")
  subject="every tracked page outside $rel_fixtures"
fi

if [ -z "$files" ]; then
  echo "::error::No page was read, so this check judged nothing."
  exit 1
fi

while IFS= read -r file; do
  [ -n "$file" ] || continue
  if [ ! -f "$file" ]; then
    echo "::error::$file is not a file, so it cannot be read."
    exit 1
  fi
  read_blocks "$file"
done <<< "$files"

if [ "$judged" -eq 0 ]; then
  echo "::error::No block was judged over $subject, so this check found nothing because it looked at nothing."
  exit 1
fi

if [ "$fail" -ne 0 ]; then
  echo "::error::A page pastes a command beside an output the command no longer prints. Re-run the command as written and paste what it prints, or change the sentence the block supports."
  exit 1
fi

say "$judged block(s) re-run and agreeing, $skipped not judged, over $subject."
