#!/usr/bin/env bash
#
# Refuse a package whose declared ABI does not name the server line it was built
# against (#30), and refuse two packages of one build that declare the same one.
#
#   scripts/abi-matches-the-line.sh prove
#   scripts/abi-matches-the-line.sh judge <package> <packages.lock.json> <declared-abi>
#   scripts/abi-matches-the-line.sh distinct <file-of-declared-abis>
#
# The only thing telling a server whether a package fits it is targetAbi in the
# packaged metadata, and the server reads it as a floor with no ceiling: a
# package built against one line's package set and declaring that line's floor
# is offered to every later server, which loads it and then meets whatever
# signature moved. Nothing on the server refuses that, so the refusal lives
# here.
#
# The mistake this is really for arrives with the build matrix. build.yaml
# carries one targetAbi literal, so the moment a build produces a package per
# line, every leg ships the same floor unless something compares each package
# with the line it was built for. `distinct` is the other half of that shape: a
# matrix leg that silently built the same thing twice leaves two packages that
# agree, and two packages of one build that declare the same ABI is a state no
# correct matrix produces.
#
# IT HOLDS NO COPY OF THE MAPPING. The declared ABI and the declared package
# version come in as arguments, and the caller reads them from
# Directory.Build.props, which #15 made the one place a server line is stated.
# A table in here would be a second statement of the line that nothing compares
# against the first, which is the defect #15 closed.
#
# It is a script rather than a block inside the workflow so it can be run
# against a package by hand and watched refusing without a runner, which is the
# same reason tools/package-contents.sh is a script.
#
# `prove` is not documentation of a proof, it is the proof, and the workflow
# runs it before it judges anything real. Each near-miss below is one value away
# from a tree that passes, and each is asserted to be refused for its own reason
# and for no other.
#
# WHAT IT CANNOT DO. It compares the line a version names, not the bytes: a
# package built against 10.11.11 and declaring 10.11.0.0 passes, which is
# correct, and one built against a patch nobody shipped would pass too. It reads
# the resolved versions out of the lock file rather than out of the assembly, so
# a build that ignored the lock file is invisible to it; RestoreLockedMode in
# Directory.Build.props is what makes that reading true on the gate, and it is
# on only where CI is set. And it says nothing about whether the declared line
# is the right line to support, which is question 1 on #2.
set -euo pipefail

# The line a version names: the first two dot-separated fields, so 10.11.11 and
# 10.11.0.0 both name 10.11. Written once and used by every leg, because two
# spellings of this is how the two halves of a comparison stop agreeing.
line_of() {
  printf '%s' "$1" | cut -d. -f1,2
}

# Every Jellyfin package the lock file resolved, as `id<TAB>version`. A line at
# a time, in the order the file has them: an id line opens a block and the first
# resolved line after it closes it. That is enough for a lock file, which is
# written by NuGet and not by a person, and it is stated here rather than
# implied because a hand-edited file with a different shape would read as an
# empty set.
jellyfin_packages() {
  awk '
    match($0, /^[[:space:]]*"[A-Za-z0-9._]+": \{/) {
      id = $0
      sub(/^[[:space:]]*"/, "", id)
      sub(/": \{.*$/, "", id)
    }
    match($0, /"resolved": "/) {
      version = $0
      sub(/^.*"resolved": "/, "", version)
      sub(/".*$/, "", version)
      if (id != "") {
        print id "\t" version
        id = ""
      }
    }
  ' "$1" | grep '^Jellyfin\.' || true
}

# meta.json out of a package. A directory is accepted as well as a zip so that a
# package already unpacked, or a fixture that never was one, can be judged the
# same way.
meta_json_of() {
  local package="$1"
  if [ -d "$package" ]; then
    if [ ! -f "$package/meta.json" ]; then
      echo "::error::$package carries no meta.json, so it declares no ABI at all." >&2
      return 1
    fi
    cat "$package/meta.json"
  elif [ -f "$package" ]; then
    if ! unzip -p "$package" meta.json 2>/dev/null; then
      echo "::error::$package carries no meta.json, so it declares no ABI at all." >&2
      return 1
    fi
  else
    echo "::error::$package does not exist, so there is nothing to judge." >&2
    return 1
  fi
}

judge() {
  local package="$1" lock="$2" declared_abi="$3"

  if [ ! -f "$lock" ]; then
    echo "::error::$lock does not exist, so there is nothing to say what this package was built against."
    return 1
  fi

  local meta
  meta=$(meta_json_of "$package") || return 1

  local packaged_abi
  packaged_abi=$(printf '%s' "$meta" | sed -n 's/.*"targetAbi"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)

  if [ -z "$packaged_abi" ]; then
    echo "::error::The package's meta.json states no targetAbi. A package with no floor is offered to every server there is."
    return 1
  fi

  if [ -z "$declared_abi" ]; then
    echo "::error::No declared ABI was passed in, so there is nothing to compare the package with. Failing closed rather than judging a package against an empty string."
    return 1
  fi

  local resolved
  resolved=$(jellyfin_packages "$lock")

  if [ -z "$resolved" ]; then
    echo "::error::$lock resolves no Jellyfin package, so nothing says which line this was compiled against. Failing closed rather than reading an empty set as agreement."
    return 1
  fi

  local fail=0
  local packaged_line declared_line
  packaged_line=$(line_of "$packaged_abi")
  declared_line=$(line_of "$declared_abi")

  echo "package declares targetAbi ${packaged_abi}, which names line ${packaged_line}"
  echo "the build was told line ${declared_line}, whose floor is ${declared_abi}"

  if [ "$packaged_abi" != "$declared_abi" ]; then
    echo "FAIL  the package declares ${packaged_abi} and the line it was built for declares ${declared_abi}."
    fail=1
  fi

  while IFS=$'\t' read -r id version; do
    [ -n "$id" ] || continue
    local package_line
    package_line=$(line_of "$version")
    if [ "$package_line" = "$declared_line" ]; then
      echo "ok    ${id} ${version}"
    else
      echo "FAIL  ${id} resolved to ${version}, which names line ${package_line}, and the package declares line ${declared_line}."
      fail=1
    fi
  done <<< "$resolved"

  if [ "$fail" -ne 0 ]; then
    echo "::error::This package's declared ABI and the package set it compiled against do not name one server line. A server reads targetAbi as a floor with no ceiling, so a package that disagrees with itself here is loaded by servers it was never built against."
    return 1
  fi

  echo "The declared ABI and every Jellyfin package this compiled against name line ${declared_line}."
}

distinct() {
  local declared="$1"

  if [ ! -f "$declared" ]; then
    echo "::error::$declared does not exist, so there is nothing to compare."
    return 1
  fi

  local values
  values=$(grep -v '^[[:space:]]*$' "$declared" || true)

  if [ -z "$values" ]; then
    echo "::error::$declared names no ABI at all. A build that produced no package is not a build that produced consistent ones."
    return 1
  fi

  local repeated
  repeated=$(printf '%s\n' "$values" | sort | uniq -d)

  if [ -n "$repeated" ]; then
    echo "FAIL  more than one package of this build declares:"
    printf '%s\n' "$repeated" | sed 's/^/        /'
    echo "::error::Two packages of one build declare the same ABI. That is the shape a matrix leaves when a leg silently built the same thing twice, and it ships two packages a server cannot tell apart."
    return 1
  fi

  echo "Every package of this build declares its own ABI:"
  printf '%s\n' "$values" | sed 's/^/  /'
}

# A fixture package and lock file, written into a directory. Four arguments:
# where, the ABI the package declares, the version its Jellyfin packages
# resolved to, and the framework the lock file is keyed by.
write_fixture() {
  local where="$1" abi="$2" resolved="$3"
  mkdir -p "$where/package"
  cat > "$where/package/meta.json" <<META
{
  "guid": "00000000-0000-0000-0000-000000000000",
  "name": "Fixture",
  "targetAbi": "${abi}",
  "framework": "net9.0"
}
META
  cat > "$where/packages.lock.json" <<LOCK
{
  "version": 1,
  "dependencies": {
    "net9.0": {
      "Jellyfin.Controller": {
        "type": "Direct",
        "resolved": "${resolved}"
      },
      "Jellyfin.Model": {
        "type": "Transitive",
        "resolved": "${resolved}"
      }
    }
  }
}
LOCK
}

prove() {
  local root
  root=$(mktemp -d)
  # shellcheck disable=SC2064
  trap "rm -rf '$root'" EXIT

  local failed=0

  expect_refusal() {
    local what="$1"
    shift
    if "$@" > "$root/out" 2>&1; then
      echo "FAIL  ${what}: it passed, so the near-miss proves nothing."
      sed 's/^/        /' "$root/out"
      failed=1
    else
      echo "ok    ${what}"
    fi
  }

  expect_pass() {
    local what="$1"
    shift
    if "$@" > "$root/out" 2>&1; then
      echo "ok    ${what}"
    else
      echo "FAIL  ${what}: it refused a tree it should accept."
      sed 's/^/        /' "$root/out"
      failed=1
    fi
  }

  # The near-miss the issue names: a declared ABI one line away from the package
  # set. One value differs from the corrected fixture below it.
  write_fixture "$root/abi-one-line-off" "10.10.0.0" "10.11.11"
  expect_refusal \
    "a package declaring one line and compiled against another is refused" \
    judge "$root/abi-one-line-off/package" "$root/abi-one-line-off/packages.lock.json" "10.11.0.0"

  write_fixture "$root/corrected" "10.11.0.0" "10.11.11"
  expect_pass \
    "the same fixture with that one value corrected passes" \
    judge "$root/corrected/package" "$root/corrected/packages.lock.json" "10.11.0.0"

  # The other direction. The package agrees with what the build was told and the
  # package set does not, which is what a matrix leg that restored the wrong line
  # leaves behind.
  write_fixture "$root/packages-one-line-off" "10.11.0.0" "10.10.11"
  expect_refusal \
    "a package compiled against another line's package set is refused" \
    judge "$root/packages-one-line-off/package" "$root/packages-one-line-off/packages.lock.json" "10.11.0.0"

  # A package that declares nothing. An absent floor is not a low one; the server
  # would refuse to read it, and a check that shrugged here would pass a package
  # nobody can install.
  mkdir -p "$root/no-abi/package"
  printf '{ "name": "Fixture" }\n' > "$root/no-abi/package/meta.json"
  cp "$root/corrected/packages.lock.json" "$root/no-abi/packages.lock.json"
  expect_refusal \
    "a package whose meta.json states no targetAbi is refused" \
    judge "$root/no-abi/package" "$root/no-abi/packages.lock.json" "10.11.0.0"

  # A lock file that resolves no Jellyfin package. An empty set is not agreement,
  # and this is the leg that stops a broken reader from reading as a clean one.
  mkdir -p "$root/no-packages"
  cp -r "$root/corrected/package" "$root/no-packages/package"
  printf '{ "version": 1, "dependencies": { "net9.0": { } } }\n' > "$root/no-packages/packages.lock.json"
  expect_refusal \
    "a lock file resolving no Jellyfin package is refused" \
    judge "$root/no-packages/package" "$root/no-packages/packages.lock.json" "10.11.0.0"

  # The matrix shape. Two packages of one build that agree about the floor.
  printf '10.11.0.0\n10.11.0.0\n' > "$root/same-abi"
  expect_refusal \
    "two packages of one build declaring the same ABI are refused" \
    distinct "$root/same-abi"

  printf '10.11.0.0\n12.0.0.0\n' > "$root/two-abis"
  expect_pass \
    "two packages declaring their own ABI pass" \
    distinct "$root/two-abis"

  printf '' > "$root/no-abis"
  expect_refusal \
    "a build that produced no package at all is refused" \
    distinct "$root/no-abis"

  if [ "$failed" -ne 0 ]; then
    echo "::error::This check does not refuse what it says it refuses. Nothing it passes afterwards means anything."
    return 1
  fi

  echo "Every near-miss above was refused for its own reason, and every corrected one passed."
}

case "${1:-}" in
  prove)
    [ "$#" -eq 1 ] || {
      echo "usage: $0 prove" >&2
      exit 2
    }
    prove
    ;;
  judge)
    [ "$#" -eq 4 ] || {
      echo "usage: $0 judge <package> <packages.lock.json> <declared-abi>" >&2
      exit 2
    }
    judge "$2" "$3" "$4"
    ;;
  distinct)
    [ "$#" -eq 2 ] || {
      echo "usage: $0 distinct <file-of-declared-abis>" >&2
      exit 2
    }
    distinct "$2"
    ;;
  *)
    echo "usage: $0 prove | judge <package> <packages.lock.json> <declared-abi> | distinct <file-of-declared-abis>" >&2
    exit 2
    ;;
esac
