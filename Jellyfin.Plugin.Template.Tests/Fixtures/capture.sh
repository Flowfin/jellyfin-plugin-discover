#!/usr/bin/env bash
#
# Record one response from a metadata source, deliberately (#48).
#
#   Jellyfin.Plugin.Template.Tests/Fixtures/capture.sh <name> <url> <directory>
#
# The failure this prevents is a captured response arriving in this repository
# with the source's content still in it. Capturing and committing are two acts
# and the second one is a judgement, so this route performs the first and
# refuses to perform the second: it will not write into a checkout of this
# repository, and what it produces has to be minimised by hand before any of it
# is added here. README.md beside this script is the position on what may then
# be kept and in what form.
#
# It is a script rather than anything the suite can reach, because a suite that
# can record from a source is a suite that can reach one, which is what #46
# exists to refuse. Nothing in the test project runs it and nothing schedules
# it. What holds that today is that no test in this repository starts a process
# at all; no check refuses one that starts to, and that gap is stated in
# README.md rather than implied away here.
#
# The key is read from DISCOVER_SOURCE_KEY and sent as a bearer credential. It
# is never an argument and never part of the URL, because both spellings put a
# live credential into a shell history and into every log that records the
# command, which is the door #80 is about. A URL that already carries one is
# refused rather than fetched.
#
# Which source, which endpoint and which key are the caller's. This route knows
# none of them, so it stays correct when the first adapter picks them (#74).
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "usage: $0 <name> <url> <directory>" >&2
  echo "  name        what the capture is called, used for the two files written" >&2
  echo "  url         the full request URL, with no credential in it" >&2
  echo "  directory   where to write, and it may not be inside this repository" >&2
  exit 2
fi

name="$1"
url="$2"
destination="$3"

if ! command -v curl >/dev/null 2>&1; then
  echo "::error::curl is not on the path, so there is nothing here that can fetch a response."
  exit 1
fi

# A name that is a path is a name that escapes the directory the caller chose.
case "$name" in
  *[!A-Za-z0-9._-]* | "" | .*)
    echo "::error::'$name' is not a usable capture name. Letters, digits, dot, underscore and hyphen, not starting with a dot."
    exit 1
    ;;
esac

# The credential must not be in the URL. Refused rather than stripped: a route
# that quietly repaired the URL would leave the caller believing the spelling is
# fine, and the copy in their shell history is already written by then.
lowered=$(printf '%s' "$url" | tr '[:upper:]' '[:lower:]')
case "$lowered" in
  *api_key=* | *apikey=* | *access_token=* | *"?token="* | *"&token="*)
    echo "::error::The URL carries a credential as a query parameter. Take it out and export DISCOVER_SOURCE_KEY instead; a key in a URL reaches the shell history, the process list and every log that records the request."
    exit 1
    ;;
esac

case "$url" in
  https://*) ;;
  *)
    echo "::error::'$url' is not an https URL. A response recorded over anything else is not the response the source sent."
    exit 1
    ;;
esac

# The refusal this route exists for. Both sides are resolved by the shell rather
# than compared as written, because a checkout on Windows spells the same
# directory in more than one way and a prefix test on the raw strings would let
# the tracked tree through while looking like it had been checked.
if [ ! -d "$destination" ]; then
  echo "::error::'$destination' is not a directory. Make it first; this route does not choose where a capture lands."
  exit 1
fi

repository=$(cd "$(dirname "$0")" && git rev-parse --show-toplevel)
repository=$(cd "$repository" && pwd -P)
resolved=$(cd "$destination" && pwd -P)

case "$resolved/" in
  "$repository"/*)
    echo "::error::'$destination' is inside $repository. A capture is not a fixture: what may be committed is the minimised form described in $(dirname "$0")/README.md, and writing the raw response into a checkout is how the unminimised one gets added by accident."
    exit 1
    ;;
esac

body="$resolved/$name.b64"
provenance="$resolved/$name.provenance"

for existing in "$body" "$provenance"; do
  if [ -e "$existing" ]; then
    echo "::error::$existing already exists. A capture is not overwritten, because the one on disk may be the one somebody has already minimised."
    exit 1
  fi
done

raw=$(mktemp)
headers=$(mktemp)
trap 'rm -f "$raw" "$headers"' EXIT

authorization=()
credential="absent, so the request was made unauthenticated"
if [ -n "${DISCOVER_SOURCE_KEY:-}" ]; then
  authorization=(--header "Authorization: Bearer ${DISCOVER_SOURCE_KEY}")
  credential="present, and sent as a bearer credential"
fi

# --fail is deliberately absent. An error body is one of the shapes a parser has
# to survive, so a response the source refused is a capture worth having rather
# than a failure of this route. The status goes in the provenance and is printed,
# so a capture of an error cannot be mistaken for a capture of an answer.
status=$(
  curl --silent --show-error --location --max-time 30 \
    --dump-header "$headers" \
    --output "$raw" \
    --write-out '%{http_code}' \
    "${authorization[@]}" \
    "$url"
)

bytes=$(wc -c <"$raw" | tr -d '[:space:]')
content_type=$(sed -n 's/^[Cc]ontent-[Tt]ype:[[:space:]]*//p' "$headers" | tr -d '\r' | tail -1)

base64 <"$raw" | fold -w 76 >"$body"

cat >"$provenance" <<PROVENANCE
Capture: $name
URL: $url
Status: $status
Content-Type: ${content_type:-not stated by the source}
Bytes: $bytes
Credential: $credential
Captured: $(date -u '+%Y-%m-%d')

Answer both of these before any part of this capture is added to
Jellyfin.Plugin.Template.Tests/Fixtures/, and put the answers in the fixture
rather than here.

Source: which source this came from, as the name of its page under docs/sources/.

Claim: why a value kept from this response may be in a public repository, as a
pointer to that page rather than as a reading of the terms written out again.

What is committed is the shape, the field names and the edge case, with
synthetic values everywhere a test does not depend on a real one. This file and
the .b64 beside it are the whole response and are not that.
PROVENANCE

echo "ok    $bytes bytes, HTTP $status, written as base64 to $body"
echo "ok    provenance stub at $provenance"

case "$status" in
  2*) ;;
  *)
    echo "note  HTTP $status is not an answer. Recorded on purpose, because an error body is a shape a parser has to survive, and the status is in the provenance so it cannot be read later as an answer."
    ;;
esac

echo "note  Nothing here may be committed as it stands. Minimise it first, against $(dirname "$0")/README.md."
