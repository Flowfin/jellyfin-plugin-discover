#!/usr/bin/env bash
#
# Refuse a release whose version the changelog does not name (#119). Two
# arguments: the version being released, and the changelog it is judged against.
#
#   tools/changelog-names-the-version.sh <version> CHANGELOG.md
#
# The failure this prevents is a version that reached the default branch with a
# line under Unreleased, sat there while other work landed, and was then tagged
# without anybody moving that line under a heading of its own. The release goes
# out, and the file this project points at as the place a version's changes are
# stated in its own words says nothing about the version people installed. The
# release notes do not close that gap: they are composed from the commit range
# by the forge and are a different artefact with a different author.
#
# It is a script rather than a block inside the workflow so that it can be run
# by hand against any changelog, which is what lets it be watched refusing
# without pushing a tag. The workflow calls it and holds no copy of the
# comparison.
#
# Two legs, and both are reported before it exits.
#
#   1. a heading naming the version exists
#   2. that heading has something under it
#
# The second is here because the mistake the first one catches has a near
# neighbour: the heading is moved in and the lines are left where they were, so
# the file names the version and still says nothing about it.
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: $0 <version> <CHANGELOG.md>" >&2
  exit 2
fi

version="$1"
changelog="$2"

if [ ! -f "$changelog" ]; then
  echo "::error::$changelog does not exist, so there is nothing to compare the version against."
  exit 1
fi

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?$ ]]; then
  echo "::error::'$version' is not three or four numeric parts (X.Y.Z or X.Y.Z.W), so it is not a version this project releases."
  exit 1
fi

# A three part number is padded to four on both sides before they are compared,
# because 1.4.0 and 1.4.0.0 are the same version written twice. That is the same
# equivalence the release run already applies between the manifest and the
# assembly, and a heading refused for writing the version the shorter way would
# be refused for punctuation rather than for content.
pad() {
  case "$1" in
  *.*.*.*) printf '%s' "$1" ;;
  *) printf '%s.0' "$1" ;;
  esac
}

want="$(pad "$version")"

# Every second level heading, with its line number. The version is the first
# word after the marker, so a heading that carries a date or a name beside it
# still matches. Anything that is not a version, Unreleased first, simply does
# not compare equal.
found=""
while IFS=: read -r line text; do
  head="${text#\#\# }"
  head="${head%% *}"
  if [[ "$head" =~ ^[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?$ ]] && [ "$(pad "$head")" = "$want" ]; then
    found="$line"
    break
  fi
done < <(grep -n '^## ' "$changelog" || true)

if [ -z "$found" ]; then
  echo "::error::$changelog has no heading naming version $version. Move the lines describing it out of Unreleased and under a '## $version' heading of its own, then tag."
  exit 1
fi

# From the line after the heading to the line before the next second level
# heading, or to the end of the file. A section is empty when every line in that
# range is blank.
next="$(awk -v start="$found" 'NR > start && /^## / { print NR; exit }' "$changelog")"
if [ -z "$next" ]; then
  next="$(awk 'END { print NR + 1 }' "$changelog")"
fi

body="$(awk -v a="$found" -v b="$next" 'NR > a && NR < b' "$changelog" | tr -d '[:space:]')"
if [ -z "$body" ]; then
  echo "::error::$changelog names version $version at line $found and says nothing under it. The heading was moved in without the lines it was meant to carry."
  exit 1
fi

echo "$changelog names version $version, at line $found."
