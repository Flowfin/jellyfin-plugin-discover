#!/usr/bin/env bash
#
# Refuse a published manifest that does not carry the newest release, or
# carries it with a checksum the package does not have (#120, #124). Four
# arguments: the manifest as fetched, this plugin's guid, the tag of the newest
# finished release, and that release's package as downloaded from the release.
#
#   tools/manifest-carries-the-release.sh <manifest.json> <guid> <tag> <package.zip>
#
# The failure this prevents is the one #120 opens with: a publish that reports
# success and leaves the manifest listing the previous version, so the release
# exists, every run is green, and nothing is installable. Its neighbour is the
# one #124 names: an entry that lists the version with a checksum the bytes at
# the address do not have, so the catalogue is current, the freshness run is
# green, and every server that tries the install refuses it. Both leave no
# trace in this tree, because the manifest is written and served elsewhere;
# the only way to know is to read the published bytes and compare them with
# the release, on a schedule rather than at the moment of publishing.
#
# It is a script rather than a block inside the workflow so that it can be run
# by hand against any manifest and any archive, which is what lets it be
# watched refusing without waiting for a publish to go wrong. The workflow
# fetches the three inputs and calls it, and holds no copy of the comparison.
#
# Four legs, and every one of them is reported before it exits, so a red run
# names every reason rather than the first.
#
#   1. the manifest parses as a list of plugins and carries this guid
#   2. the release's version is listed under that guid
#   3. that entry's address names this tag and this package by name
#   4. that entry's checksum is the package's own
#
# What it cannot do. It reads one manifest and one release: a second channel
# published under another address is a second run with other arguments, and
# nothing here enumerates channels. It compares against the package on the
# release page rather than against the bytes at the manifest's own address, so
# an address serving different bytes from the release it names is outside leg
# 4 and is what leg 3 exists to make unlikely. And it judges the newest
# finished release only: an older version dropped from the manifest is not
# read here.
set -euo pipefail

if [ "$#" -ne 4 ]; then
  echo "usage: $0 <manifest.json> <guid> <tag> <package.zip>" >&2
  exit 2
fi

manifest="$1"
guid="$2"
tag="$3"
package="$4"

for f in "$manifest" "$package"; do
  if [ ! -f "$f" ]; then
    echo "::error::$f does not exist, so there is nothing to compare."
    exit 1
  fi
done

for tool in jq md5sum; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "::error::$tool is not on PATH, and the comparison needs it."
    exit 1
  fi
done

if [ -z "$guid" ] || [ -z "$tag" ]; then
  echo "::error::The guid and the tag are both required, and one of them is empty."
  exit 1
fi

# The version a tag names. The release tags here are the four-part version with
# the channel behind a hyphen, and a manifest entry carries the version alone,
# so the channel suffix and a leading v are taken off, and a three-part number
# is padded to four on both sides before they are compared, because 1.4.0 and
# 1.4.0.0 are the same version written twice.
pad() {
  case "$1" in
  *.*.*.*) printf '%s' "$1" ;;
  *) printf '%s.0' "$1" ;;
  esac
}

version=${tag#v}
version=${version%%-*}
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?$ ]]; then
  echo "::error::'$tag' does not name a version of three or four numeric parts, so it is not a release this project publishes."
  exit 1
fi
version=$(pad "$version")

fail=0

# Leg 1: the manifest parses and carries this guid. A body that does not parse
# is refused rather than read as empty, because a catalogue that answered with
# an error page would otherwise pass every leg below as a plugin nobody lists.
if ! jq -e 'type == "array"' "$manifest" >/dev/null 2>&1; then
  echo "FAIL  leg 1: $manifest does not parse as a list of plugins."
  echo "::error::The manifest could not be read, so nothing below was judged."
  exit 1
fi

entry=$(jq -c --arg g "$guid" '[.[] | select((.guid | ascii_downcase) == ($g | ascii_downcase))] | .[0] // empty' "$manifest")
if [ -z "$entry" ]; then
  echo "FAIL  leg 1: the manifest lists no plugin with guid $guid."
  echo "::error::This plugin is not in the manifest, so its release cannot be listed there."
  exit 1
fi
echo "ok    leg 1: the manifest lists guid $guid as $(printf '%s' "$entry" | jq -r '.name // "an unnamed plugin"')."

# Leg 2: the release's version is listed. Versions are compared padded on both
# sides, for the reason above.
listed=$(printf '%s' "$entry" | jq -c --arg v "$version" '
  [.versions[]? | select(
    ((.version | tostring) as $x | if ($x | split(".") | length) == 3 then $x + ".0" else $x end) == $v
  )] | .[0] // empty')
if [ -z "$listed" ]; then
  echo "FAIL  leg 2: version $version, from tag $tag, is not listed under this guid. The manifest lists:"
  printf '%s' "$entry" | jq -r '.versions[]? | "        \(.version)  \(.timestamp // "-")"'
  fail=1
  echo "::error::The newest finished release is not in the manifest, which is the failure #120 is written against."
  exit 1
fi
echo "ok    leg 2: version $version is listed, stamped $(printf '%s' "$listed" | jq -r '.timestamp // "-"')."

# Leg 3: the entry points at this tag and this package. A checksum that agrees
# with a package at some other address is a comparison with the wrong bytes,
# so the address is read before the checksum is.
source_url=$(printf '%s' "$listed" | jq -r '.sourceUrl // ""')
package_name=$(basename "$package")
case "$source_url" in
*"/releases/download/$tag/$package_name")
  echo "ok    leg 3: the entry's address names tag $tag and $package_name."
  ;;
*)
  echo "FAIL  leg 3: the entry's address does not end in /releases/download/$tag/$package_name:"
  echo "        $source_url"
  fail=1
  ;;
esac

# Leg 4: the checksum is the package's own. The catalogue publishes MD5, which
# is what a server compares on install, so that is what is compared here; it is
# an identity check against the bytes the release page serves and not a
# statement about the digest.
expected=$(printf '%s' "$listed" | jq -r '.checksum // ""' | tr '[:upper:]' '[:lower:]')
actual=$(md5sum "$package" | awk '{print tolower($1)}')
if [ -z "$expected" ]; then
  echo "FAIL  leg 4: the entry carries no checksum."
  fail=1
elif [ "$expected" != "$actual" ]; then
  echo "FAIL  leg 4: the entry's checksum is not the package's:"
  echo "        manifest  $expected"
  echo "        package   $actual  ($package_name)"
  fail=1
else
  echo "ok    leg 4: the entry's checksum $expected is the package's own."
fi

if [ "$fail" -ne 0 ]; then
  echo "::error::The published manifest disagrees with the newest release. What every server installing from it would meet is above, one line per leg."
  exit 1
fi

echo "The manifest carries release $tag as version $version, at its own address, with the package's own checksum."
