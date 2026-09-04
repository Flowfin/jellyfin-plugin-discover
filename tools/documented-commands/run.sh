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
# A CHECKOUT HOLDING PART OF THE HISTORY IS REFUSED BEFORE A PAGE IS READ. The
# history is part of what the pages assert, so a truncated one turns this check
# around: a correct page is reported as drifted, which is worse than the drift it
# was built to find. The refusal is at the top of this file with the reason and
# the way out, and #406 is the eight mainline runs that bought it.
#
# A BLOCK NAMING `origin/master`, OFF THE MAINLINE, IS RUN TWICE. Half of this
# check's failures on the default branch were one class: such a block was not
# judged at all while the branch that broke it was open, and was judged for the
# first time on the run after the merge, which is the one moment nobody is
# waiting on. It is now read against the mainline and against the tree being
# pushed, and which of the two the paste still agrees with decides the answer:
#
#   both     nothing to say, and the block counts as judged.
#   mainline only    refused. The page is right about the mainline today and
#                    this change is what makes it wrong, which is the whole
#                    population above, caught one merge earlier.
#   tree only        the mainline is already wrong about the block and this
#                    change repairs it, so it is reported as a repair.
#   neither          reported and passed over. Either the mainline is already
#                    red or this branch is behind one that made it so, and
#                    refusing here would refuse a branch cut before somebody
#                    else moved a quoted line - which is the failure mode that
#                    gets a check turned off.
#
# The reason the block reads `origin/master` in the first place survives that: a
# page and the file it quotes are still changeable in one pull request, because
# what is refused is a paste that agrees with the mainline and not with the tree
# being landed. A page rewritten together with what it quotes agrees with the
# tree and is a repair rather than a refusal. This is #383.
#
# On the mainline the two commits are the same commit, so the block is run once
# and nothing above applies.
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
#   5. A block naming `origin/master` that the mainline already disagrees with
#      is passed over rather than refused, so a branch cut behind a red mainline
#      is told about that block and not stopped by it. The mainline run is what
#      refuses it, which is where the six failures this arm was built for were
#      caught, and this arm moves none of them earlier.
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

# A CHECKOUT HOLDING PART OF THE HISTORY CANNOT JUDGE A PAGE THAT READS IT, and
# the way it fails is the failure this check exists to prevent, pointed the
# other way. `git log` walks to the graft and stops, so a commit whose parents
# were never fetched reads as the commit that added every file under it, and a
# page pasting the real one is refused for being right. That is a refusal
# manufactured by the checkout rather than found on the page, and a reader who
# trusts it edits a page that was correct.
#
# So the depth is refused here, where the reason can be named, rather than
# discovered at whichever block happens to read history first. #406 is where it
# cost eight consecutive mainline runs, every one of them naming a page that
# agreed with its command on any checkout carrying this repository's history.
if [ "$(git rev-parse --is-shallow-repository)" = "true" ]; then
  echo "::error::This checkout holds part of the history, so a block reading the history would be judged against a truncated one and a correct page refused. Run 'git fetch --unshallow', or check out with fetch-depth: 0, and run this again."
  exit 1
fi

rel_here=$(git -C "$here" rev-parse --show-prefix)
rel_here=${rel_here%/}
rel_fixtures="$rel_here/fixtures"

# Verbs that read and never write. A verb outside this set is refused rather
# than run, whatever else the line says.
readonly_verbs=" grep ls-files ls-tree log show blame diff rev-parse describe shortlog cat-file "

# The two commits a block naming `origin/master` is read against. The first is
# the mainline the page describes and the second is the tree being pushed; with
# the defaults the first run is the command exactly as the page writes it. They
# are settable so that `--prove` can hand this reader two commits it made itself
# and watch each arm of the comparison, which is the only way to exercise the
# arms at all: the mainline job checks out one tree and there is no second one
# on that machine for the comparison to differ against.
mainline_ref=${DOCUMENTED_COMMANDS_MAINLINE:-origin/master}
tree_ref=${DOCUMENTED_COMMANDS_TREE:-HEAD}

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
# request it is not, and the block is then read against both commits rather than
# against the mainline alone.
on_the_mainline() {
  local remote="" head=""
  remote=$(git rev-parse --verify --quiet "$mainline_ref") || return 1
  head=$(git rev-parse --verify --quiet "$tree_ref") || return 1
  [ "$remote" = "$head" ]
}

# The command with the mainline it names swapped for the commit handed in. Only
# outside quoted spans: a page that greps for the STRING `origin/master` is
# reading this checkout rather than the mainline, and rewriting its pattern
# would change what it looks for. `docs/plugin-catalogue.md` carries that shape,
# and it was skipped as a mainline read for as long as the test was made on the
# whole line.
with_ref() {
  printf '%s\n' "$1" | awk -v ref="$2" -v sq="'" -v dq='"' '
    {
      out = ""; q = ""; i = 1; n = length($0)
      while (i <= n) {
        c = substr($0, i, 1)
        if (q != "") { out = out c; if (c == q) { q = "" }; i++; continue }
        if (c == sq || c == dq) { q = c; out = out c; i++; continue }
        if (substr($0, i, 13) == "origin/master") { out = out ref; i += 13; continue }
        out = out c; i++
      }
      print out
    }'
}

# git prefixes each line it prints with the ref it was asked about, so a run
# against the tree being pushed answers `HEAD:path` where the page pastes
# `origin/master:path`. The prefix is put back before anything is compared, so
# what is judged is the two trees against each other rather than the spelling of
# the ref.
as_the_page_writes_it() {
  local ref=$1
  [ "$ref" = "origin/master" ] && { cat; return; }
  sed "s|^${ref}:|origin/master:|"
}

# True when the block reads the mainline, tested outside quoted spans for the
# reason `with_ref` gives.
reads_the_mainline() {
  case $(printf '%s' "$1" | sed "s/'[^']*'//g; s/\"[^\"]*\"//g") in
    *"origin/"*) return 0 ;;
  esac
  return 1
}

reason_to_skip() {
  local cmd=$1 verb="" bare=""

  # A placeholder a reader substitutes, and a shell redirection, are both
  # refused. Both are looked for outside quoted spans, because a `<` or a `>`
  # inside a grep pattern is neither: the first version of this check tested the
  # whole line and refused seven blocks for a generic type argument, a property
  # name in angle brackets and a `=>` in a pattern, including the two blocks on
  # README.md that show the surface is registered and which server line the tree
  # declares.
  bare=$(printf '%s' "$cmd" | sed "s/'[^']*'//g; s/\"[^\"]*\"//g")

  # The reasons are asked in the order that gives a reader the true one. A
  # command reaching a Jellyfin tag is refused for that rather than for the verb
  # it happens to start with.
  case $cmd in
    *"v10.11.11"* | *"v12.0-rc4"*)
      echo "reads a Jellyfin checkout at a tag, which is not this repository"
      return
      ;;
  esac

  case $bare in
    *"<"* | *">"*)
      echo "carries a placeholder or a redirection"
      return
      ;;
  esac

  case $cmd in
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

  # A block naming the mainline needs both commits to be readable, because off
  # the mainline it is read against each of them. Neither absence is the page's
  # fault, so both are a skip that says which commit was missing rather than a
  # refusal.
  if reads_the_mainline "$cmd"; then
    if ! git rev-parse --verify --quiet "$mainline_ref" >/dev/null; then
      echo "reads origin/master and this checkout does not carry it"
      return
    fi
    if ! on_the_mainline && ! git rev-parse --verify --quiet "$tree_ref" >/dev/null; then
      echo "reads origin/master and this checkout does not carry $tree_ref to compare it against"
      return
    fi
  fi

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
      if (line ~ /^    [^ ]/ || line ~ /^     /) {
        # A blank line inside an indented block belongs to the output. It is
        # held rather than written out at once, because what follows it decides
        # what it was: another line of output in the same block, or the gap
        # before the next block.
        if (held > 0 && line ~ /^    git /) { flush(); held = 0; indented = 1; start = NR; n = 0; body[++n] = substr(line, 5); next }
        for (; held > 0; held--) { body[++n] = "" }
        body[++n] = substr(line, 5); next
      }
      # A blank line with no output above it is the gap after a command handed
      # to the reader rather than the first line of an answer, so the block ends
      # there and whatever follows belongs to something else. docs/limits.md
      # carries that shape. A blank line with output above it is part of that
      # output.
      if (line ~ /^[[:space:]]*$/) { if (n <= 1) { flush(); indented = 0; held = 0; next } held++; next }
      flush(); indented = 0; held = 0
    }
    /^    git / { indented = 1; held = 0; start = NR; n = 0; body[++n] = substr(line, 5); next }
    { next }
    END { flush() }
  ' "$1"
}

# Run one block and say whether what came back is what the page pastes. The
# answer is carried in `run_out` so a caller comparing two commits can report
# both.
run_out=""
run_rc=0
agrees() {
  local cmd=$1 want=$2 exit_want=$3 ref=$4

  run_rc=0
  run_out=$(bash -c "$(with_ref "$cmd" "$ref")" 2>&1) || run_rc=$?
  run_out=${run_out//$'\r'/}
  run_out=$(printf '%s' "$run_out" | as_the_page_writes_it "$ref")

  [ "$run_out" = "$want" ] && { [ -z "$exit_want" ] || [ "$exit_want" = "$run_rc" ]; }
}

difference() {
  local cmd=$1 want=$2 exit_want=$3 got=$4 rc=$5
  printf '        command: %s\n' "$cmd"
  printf '        the page carries:\n'
  [ -n "$want" ] && printf '%s\n' "$want" | sed 's/^/          /'
  [ -n "$exit_want" ] && printf '          exit=%s\n' "$exit_want"
  printf '        the command prints:\n'
  [ -n "$got" ] && printf '%s\n' "$got" | sed 's/^/          /'
  [ -n "$exit_want" ] && printf '          exit=%s\n' "$rc"
  # A block carrying no status line is the ordinary case, and this function
  # reports rather than deciding: without the line, its own answer would be that
  # last test's, and a report would end the run wherever it is not a refusal.
  return 0
}

judge() {
  local file=$1 line=$2 cmd=$3 want=$4 exit_want=$5
  local on_mainline=0 on_tree=0 mainline_got="" mainline_rc=0

  # A block that does not name the mainline reads this checkout either way, so
  # there is one run and it is the command as the page writes it.
  if ! reads_the_mainline "$cmd" || on_the_mainline; then
    judged=$((judged + 1))
    if agrees "$cmd" "$want" "$exit_want" "$mainline_ref"; then
      say "ok    $file:$line: still prints what is pasted under it."
      return 0
    fi
    fail=1
    printf 'FAIL  %s:%s: the command no longer prints what is pasted under it.\n' "$file" "$line"
    difference "$cmd" "$want" "$exit_want" "$run_out" "$run_rc"
    return 0
  fi

  # Off the mainline the block is read twice, because a page that quotes a line
  # and the file it quotes are meant to be changeable in one pull request. What
  # separates that from a page this change is breaking is which of the two
  # commits the paste still agrees with.
  agrees "$cmd" "$want" "$exit_want" "$mainline_ref" && on_mainline=1
  mainline_got=$run_out
  agrees "$cmd" "$want" "$exit_want" "$tree_ref" && on_tree=1

  if [ "$on_mainline" = "1" ] && [ "$on_tree" = "1" ]; then
    judged=$((judged + 1))
    say "ok    $file:$line: still prints what is pasted under it, on the mainline and in this checkout."
    return 0
  fi

  if [ "$on_mainline" = "1" ]; then
    judged=$((judged + 1))
    fail=1
    printf 'FAIL  %s:%s: this change moves a line the page quotes.\n' "$file" "$line"
    printf '        The block agrees with the mainline and does not agree with this checkout, so\n'
    printf '        the page is right about the tree today and this change is what makes it wrong.\n'
    difference "$cmd" "$want" "$exit_want" "$run_out" "$run_rc"
    return 0
  fi

  if [ "$on_tree" = "1" ]; then
    judged=$((judged + 1))
    say "ok    $file:$line: the mainline disagrees with this block and this checkout agrees, so this change repairs it."
    return 0
  fi

  # Neither agrees. Either the mainline is already red on this block or this
  # branch is behind one that made it so, and neither is this change's doing.
  # Refusing here is what would get the check turned off.
  skipped=$((skipped + 1))
  say "skip  $file:$line: the mainline already disagrees with this block, so this change is not what makes it wrong."
  [ "$quiet" = "1" ] || difference "$cmd" "$want" "$exit_want" "$mainline_got" "$mainline_rc"
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

# Every leg is reported before the run ends, so a red run names every reason
# rather than the first one. Leg 1 is what makes this check worth counting: a
# lint nobody has watched refusing is a green tick with no evidence behind it.
# The count of legs is not written here, because it has already moved once and a
# number in a comment is one more thing to keep true.
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

  # Leg 3 is the refusal to run, and it is here because the arm it exercises
  # fires on no tracked page today: every block in the corpus that carries a
  # redirection is refused for an earlier reason, so without this the arm would
  # be a guard nobody has watched.
  if out=$(bash "$script" "$rel_fixtures/refuses-to-run.md" 2>&1); then
    case $out in
      *"carries a placeholder or a redirection"*)
        echo "ok    leg 3: a block carrying a redirection is refused rather than run."
        ;;
      *)
        echo "FAIL  leg 3: the block carrying a redirection was not refused for that reason:"
        printf '%s\n' "$out" | sed 's/^/        /'
        bad=1
        ;;
    esac
  else
    echo "FAIL  leg 3: the block carrying a redirection was run, or refused as a difference:"
    printf '%s\n' "$out" | sed 's/^/        /'
    bad=1
  fi

  # Legs 5 and 6 are one pair. Leg 5 is the page that pastes what its command
  # prints, blank line and all, and it was refused before the extractor carried
  # a blank line through; it also pins how many blocks that page is, because a
  # reader that carried the blank line too far would swallow the second of two
  # neighbouring commands as the first one's output and report one block where
  # there are three. Leg 6 is the same output pasted only as far as its first
  # blank line, which is what the page looked like when the reader had already
  # dropped the rest, and it has to stay refused.
  if out=$(bash "$script" "$rel_fixtures/holds-the-rule-across-a-blank-line.md" 2>&1); then
    case $out in
      *"3 block(s) re-run and agreeing, 0 not judged"*)
        echo "ok    leg 5: an output pasted across a blank line is judged, agrees, and is three blocks."
        ;;
      *)
        echo "FAIL  leg 5: the fixture whose output carries a blank line was read as something other than three agreeing blocks:"
        printf '%s\n' "$out" | sed 's/^/        /'
        bad=1
        ;;
    esac
  else
    echo "FAIL  leg 5: the fixture whose output carries a blank line was refused:"
    printf '%s\n' "$out" | sed 's/^/        /'
    bad=1
  fi

  if out=$(QUIET=1 bash "$script" "$rel_fixtures/breaks-the-rule-across-a-blank-line.md" 2>&1); then
    echo "FAIL  leg 6: the fixture that stops at its own blank line was not refused."
    bad=1
  else
    case $out in
      *"breaks-the-rule-across-a-blank-line.md"*"no longer prints what is pasted under it"*)
        echo "ok    leg 6: an output pasted only as far as its blank line is refused, and the refusal names it."
        ;;
      *)
        echo "FAIL  leg 6: the fixture that stops at its own blank line was refused for something other than its block:"
        printf '%s\n' "$out" | sed 's/^/        /'
        bad=1
        ;;
    esac
  fi

  # Legs 7, 8 and 9 are the three arms a block naming `origin/master` takes off
  # the mainline, and they are why this reader takes both commits from the
  # environment. The run that judges the tracked pages on the mainline has one
  # tree checked out and no second one to differ against, and a fixture pinning
  # two commits of this history would pin two commits that a rewritten branch
  # can move out from under it. What is handed in instead is this checkout's
  # head and that head's tree, which every checkout carries whatever it stands
  # on: two different objects, answering `git cat-file -t` differently, so all
  # three arms are exercised on every run of this check rather than only where a
  # branch happens to differ.
  local commit="" tree=""
  commit=$(git rev-parse --verify --quiet HEAD) || commit=""
  tree=$(git rev-parse --verify --quiet 'HEAD^{tree}') || tree=""

  if [ -z "$commit" ] || [ -z "$tree" ]; then
    echo "FAIL  legs 7-9: this checkout carries no head, so the two objects the arms are read against do not exist."
    bad=1
  else
    if out=$(QUIET=1 DOCUMENTED_COMMANDS_MAINLINE="$commit" DOCUMENTED_COMMANDS_TREE="$tree" \
      bash "$script" "$rel_fixtures/agrees-with-the-mainline-only.md" 2>&1); then
      echo "FAIL  leg 7: the block that agrees with the mainline and not with this checkout was not refused."
      bad=1
    else
      case $out in
        *"agrees-with-the-mainline-only.md"*"this change moves a line the page quotes"*)
          echo "ok    leg 7: a block the mainline agrees with and this checkout does not is refused before the merge."
          ;;
        *)
          echo "FAIL  leg 7: that block was refused for something other than moving a quoted line:"
          printf '%s\n' "$out" | sed 's/^/        /'
          bad=1
          ;;
      esac
    fi

    if out=$(DOCUMENTED_COMMANDS_MAINLINE="$tree" DOCUMENTED_COMMANDS_TREE="$commit" \
      bash "$script" "$rel_fixtures/agrees-with-the-mainline-only.md" 2>&1); then
      case $out in
        *"this checkout agrees, so this change repairs it"*)
          echo "ok    leg 8: the same block read the other way round is a repair rather than a refusal."
          ;;
        *)
          echo "FAIL  leg 8: a change repairing a block the mainline is wrong about was not read as one:"
          printf '%s\n' "$out" | sed 's/^/        /'
          bad=1
          ;;
      esac
    else
      echo "FAIL  leg 8: a change repairing a block the mainline is wrong about was refused:"
      printf '%s\n' "$out" | sed 's/^/        /'
      bad=1
    fi

    if out=$(DOCUMENTED_COMMANDS_MAINLINE="$commit" DOCUMENTED_COMMANDS_TREE="$tree" \
      bash "$script" "$rel_fixtures/agrees-with-neither-commit.md" 2>&1); then
      case $out in
        *"the mainline already disagrees with this block"*)
          echo "ok    leg 9: a block the mainline is already wrong about is reported and passed over."
          ;;
        *)
          echo "FAIL  leg 9: a block neither commit agrees with was not reported as the mainline's:"
          printf '%s\n' "$out" | sed 's/^/        /'
          bad=1
          ;;
      esac
    else
      echo "FAIL  leg 9: a block neither commit agrees with was refused, which would refuse a branch somebody else made red:"
      printf '%s\n' "$out" | sed 's/^/        /'
      bad=1
    fi
  fi

  if out=$(bash "$script" 2>&1); then
    echo "ok    leg 4: the tracked pages are silent."
    printf '%s\n' "$out" | tail -1 | sed 's/^/      /'
  else
    echo "FAIL  leg 4: a tracked page was refused:"
    printf '%s\n' "$out" | grep -A12 '^FAIL' | sed 's/^/        /'
    bad=1
  fi

  # Leg 10 is the guard on the checkout depth, and proving it needs a repository
  # that is really shallow rather than one told that it is. What is made is a
  # scratch clone of this checkout's head, one commit deep, with the reader under
  # proof copied over the one the clone carries, so what is exercised is the file
  # in hand rather than the last one committed. It is an init and a fetch: no
  # commit is authored, nothing is signed, and nothing is written into this
  # repository, which is the accident the allowlist above was built after.
  #
  # The other half of the proof is every leg here and the tracked-page pass,
  # which run in this checkout and are silent, so the guard is shown to bite on a
  # repository holding part of the history and to stay out of the way on one
  # holding all of it.
  local scratch="" made=""
  scratch=$(mktemp -d)
  if made=$( { git init --quiet "$scratch/dst" \
      && git -C "$scratch/dst" fetch --quiet --no-tags --depth=1 "file://$repo" HEAD \
      && git -C "$scratch/dst" checkout --quiet --detach FETCH_HEAD \
      && cp "$script" "$scratch/dst/$rel_here/run.sh"; } 2>&1 ); then
    if [ "$(git -C "$scratch/dst" rev-parse --is-shallow-repository)" != "true" ]; then
      echo "FAIL  leg 10: the scratch clone is not shallow, so this leg would pass without the guard ever being asked."
      bad=1
    elif out=$(QUIET=1 bash "$scratch/dst/$rel_here/run.sh" 2>&1); then
      echo "FAIL  leg 10: a checkout holding one commit of the history was judged rather than refused."
      bad=1
    else
      case $out in
        *"holds part of the history"*)
          echo "ok    leg 10: a checkout holding part of the history is refused, and the refusal names the depth."
          ;;
        *)
          echo "FAIL  leg 10: a checkout holding one commit was refused for something other than its depth:"
          printf '%s\n' "$out" | sed 's/^/        /'
          bad=1
          ;;
      esac
    fi
  else
    echo "FAIL  leg 10: the scratch clone this leg reads could not be made, so the guard on the checkout depth is unproven:"
    printf '%s\n' "$made" | sed 's/^/        /'
    bad=1
  fi
  rm -rf "$scratch" 2>/dev/null || true

  if [ "$bad" -ne 0 ]; then
    echo "::error::The documented-commands check did not hold its own legs."
    exit 1
  fi

  echo "The check fires on the fixture that breaks the rule, is silent on the one that holds it, refuses to run a block carrying a redirection, carries a blank line through an output and refuses one pasted only as far as it, refuses a block the mainline agrees with and this checkout does not, reads the same block the other way round as a repair, passes over one the mainline is already wrong about, refuses a checkout holding part of the history rather than judging pages against it, and is silent on the tracked pages."
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

# Only for the whole-tree run. A page named on the command line may legitimately
# carry nothing this check will judge, and the fixture proving the refusal to run
# is exactly that case.
if [ "$judged" -eq 0 ] && [ "$#" -eq 0 ]; then
  echo "::error::No block was judged over $subject, so this check found nothing because it looked at nothing."
  exit 1
fi

if [ "$fail" -ne 0 ]; then
  echo "::error::A page pastes a command beside an output the command no longer prints. Re-run the command as written and paste what it prints, or change the sentence the block supports."
  exit 1
fi

say "$judged block(s) re-run and agreeing, $skipped not judged, over $subject."
