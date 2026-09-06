#!/usr/bin/env bash
#
# Refuse a release package that is not the package the gate built (#35, #119).
# Two arguments: the package the release run built, and the package the gate
# uploaded for the same commit.
#
#   tools/package-matches-the-gate.sh <release.zip> <gate.zip>
#
# The failure this prevents is a release whose bytes nobody checked. The gate
# builds a package on every pull request and a server is asked to load it; the
# release path builds again from the tagged commit, and until now nothing
# compared the two. Same tool at the same pin on the same commit is a reason to
# expect the same result and is not a measurement that it happened, and the
# package a user installs is the one the release path built.
#
# WHAT IS COMPARED IS THE ARCHIVE'S ENTRIES AND NOT THE ARCHIVE. Two packages
# built from one commit minutes apart are not the same file: the packaging tool
# stamps each entry with the minute it was written and puts the moment of the
# build into `meta.json` as `timestamp`, so the archives' checksums differ while
# every byte of the plugin agrees. Measured on the first release, where the
# gate's archive and the released one carried the same `Jellyfin.Plugin.Template.dll`
# to the byte and differed only in those stamps. So this compares:
#
#   1. the list of entries, in both directions
#   2. every entry other than meta.json, byte for byte
#   3. meta.json with `timestamp` removed
#
# It is a script rather than a block inside the workflow so that it can be run
# by hand against two archives, which is what lets it be watched refusing
# without pushing a tag. The workflow calls it and holds no copy of the
# comparison.
#
# Every difference is reported before it exits, so a red run names all of them
# rather than the first.
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: $0 <release.zip> <gate.zip>" >&2
  exit 2
fi

ours="$1"
theirs="$2"

for f in "$ours" "$theirs"; do
  if [ ! -f "$f" ]; then
    echo "::error::$f does not exist, so there is nothing to compare."
    exit 1
  fi
done

for tool in unzip jq cmp comm; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "::error::$tool is not on PATH, and the comparison needs it."
    exit 1
  fi
done

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

mkdir -p "$work/ours" "$work/theirs"
unzip -q "$ours" -d "$work/ours"
unzip -q "$theirs" -d "$work/theirs"

# Entry names from the archives themselves rather than from the unpacked trees,
# so an entry that unzip declined to write is still counted as present.
ours_entries=$(unzip -Z1 "$ours" | sort -u)
theirs_entries=$(unzip -Z1 "$theirs" | sort -u)

fail=0

only_ours=$(comm -23 <(printf '%s\n' "$ours_entries") <(printf '%s\n' "$theirs_entries"))
only_theirs=$(comm -13 <(printf '%s\n' "$ours_entries") <(printf '%s\n' "$theirs_entries"))

if [ -n "$only_ours" ]; then
  echo "FAIL  the release package carries entries the gate's package does not:"
  printf '%s\n' "$only_ours" | sed 's/^/        /'
  fail=1
fi

if [ -n "$only_theirs" ]; then
  echo "FAIL  the gate's package carries entries the release package does not:"
  printf '%s\n' "$only_theirs" | sed 's/^/        /'
  fail=1
fi

# Entries both carry are compared one by one. A directory entry has no bytes to
# compare and is skipped by name.
shared=$(comm -12 <(printf '%s\n' "$ours_entries") <(printf '%s\n' "$theirs_entries"))
compared=0

while IFS= read -r entry; do
  [ -n "$entry" ] || continue
  case "$entry" in */) continue ;; esac

  if [ "$entry" = "meta.json" ]; then
    # Two files rather than two process substitutions: on a Windows shell jq
    # cannot write into the pipe the latter opens, and the comparison is meant
    # to run by hand on whatever machine has the two archives.
    jq -S 'del(.timestamp)' "$work/ours/$entry" >"$work/ours.meta"
    jq -S 'del(.timestamp)' "$work/theirs/$entry" >"$work/theirs.meta"
    if ! diff "$work/ours.meta" "$work/theirs.meta" >"$work/meta.diff"; then
      echo "FAIL  meta.json differs beyond its timestamp:"
      sed 's/^/        /' "$work/meta.diff"
      fail=1
    fi
  elif ! cmp -s "$work/ours/$entry" "$work/theirs/$entry"; then
    echo "FAIL  $entry differs between the release package and the gate's package:"
    printf '        release: %s\n' "$(sha256sum "$work/ours/$entry" | cut -d' ' -f1)"
    printf '        gate:    %s\n' "$(sha256sum "$work/theirs/$entry" | cut -d' ' -f1)"
    fail=1
  fi
  compared=$((compared + 1))
done <<<"$shared"

if [ "$compared" -eq 0 ]; then
  echo "::error::The two packages share no entry, so nothing was compared."
  exit 1
fi

if [ "$fail" -ne 0 ]; then
  echo "::error::The release package is not the package the gate built. What the gate tested is not what this run would ship, and the release is refused rather than published on the strength of a shared tool and pin."
  exit 1
fi

echo "The release package is the gate's package: $compared entries compared, every one identical, meta.json identical apart from its timestamp."
printf '%s\n' "$shared" | sed 's/^/  /'
