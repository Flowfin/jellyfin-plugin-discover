#!/usr/bin/env bash
#
# Refuse a built package whose contents are not exactly what build.yaml names
# (#35). Two arguments: the package, and the manifest it is judged against.
#
#   tools/package-contents.sh <package.zip> build.yaml
#
# The failure this prevents is a package that installs and is not the thing the
# manifest describes. `artifacts:` is what a reader takes as the answer to what
# ships, and nothing until now compared it with the zip. A file dropped from the
# packaging tool's output leaves a manifest that still names it, and a file
# added leaves one that does not, and both install cleanly on a server.
#
# It is a script rather than a block inside the workflow so that it can be run
# against a package by hand, which is what lets it be watched refusing without a
# runner. The workflow calls it and holds no copy of the comparison.
#
# Both directions are reported before it exits, so a red run names every
# difference rather than the first one.
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: $0 <package.zip> <build.yaml>" >&2
  exit 2
fi

artifact="$1"
manifest="$2"

for f in "$artifact" "$manifest"; do
  if [ ! -f "$f" ]; then
    echo "::error::$f does not exist, so there is nothing to compare."
    exit 1
  fi
done

# The `artifacts:` block, up to the next key at column zero. The range's end is
# searched from the line after the start, so the `artifacts:` line does not end
# its own block. Quotes are optional because the manifest's other readers in
# this tree treat them that way.
named=$(
  sed -n '/^artifacts:/,/^[^[:space:]#-]/p' "$manifest" |
    sed -n 's/^[[:space:]]*-[[:space:]]*"\{0,1\}\([^"]*\)"\{0,1\}[[:space:]]*$/\1/p' |
    sort -u
)

if [ -z "$named" ]; then
  echo "::error::$manifest names no artifact, so nothing says what the package should carry."
  exit 1
fi

# meta.json is written by the packaging tool rather than named by the manifest,
# and the install step already refuses a package without one, so it is expected
# here rather than reported as a surprise. Nothing else is added to the set: an
# entry the tool starts shipping is a change somebody should have to look at.
expected=$(printf '%s\nmeta.json\n' "$named" | sort -u)

present=$(unzip -Z1 "$artifact" | sort -u)

missing=$(comm -23 <(printf '%s\n' "$expected") <(printf '%s\n' "$present"))
extra=$(comm -13 <(printf '%s\n' "$expected") <(printf '%s\n' "$present"))

fail=0

if [ -n "$missing" ]; then
  echo "FAIL  the package does not carry what $manifest names:"
  printf '%s\n' "$missing" | sed 's/^/        /'
  fail=1
fi

if [ -n "$extra" ]; then
  echo "FAIL  the package carries what $manifest does not name:"
  printf '%s\n' "$extra" | sed 's/^/        /'
  fail=1
fi

if [ "$fail" -ne 0 ]; then
  echo "::error::The built package is not what build.yaml describes. A package that installs is not the same as a package that ships what its manifest says it ships."
  exit 1
fi

echo "The package carries exactly what $manifest names, and meta.json:"
printf '%s\n' "$present" | sed 's/^/  /'
