#!/usr/bin/env bash
#
# Ask a running server, as a signed-in user over HTTP, whether the discover
# surface is among the views it hands that user (#38).
#
# This is the claim the whole plan rests on: that the surface reaches every
# client with no client change. The server-side half of it is answerable with
# no client at all, because the views a client asks for are built by
# UserViewManager, which folds the channels a user may see into the list it
# returns. Read at the two targeted tags of https://github.com/jellyfin/jellyfin:
#
#     git grep -n -A3 'GetChannelsInternalAsync(new ChannelQuery' v10.11.11 -- Emby.Server.Implementations/Library/UserViewManager.cs
#     v10.11.11:Emby.Server.Implementations/Library/UserViewManager.cs:121:                var channelResult = _channelManager.GetChannelsInternalAsync(new ChannelQuery
#     v10.11.11:Emby.Server.Implementations/Library/UserViewManager.cs-122-                {
#     v10.11.11:Emby.Server.Implementations/Library/UserViewManager.cs-123-                    UserId = user.Id
#     v10.11.11:Emby.Server.Implementations/Library/UserViewManager.cs-124-                }).GetAwaiter().GetResult();
#
# The request below sends no includeExternalContent parameter, which is what a
# client sends. The server's default for it is true, so the answer this reads is
# the answer a client gets rather than one this script asked for:
#
#     curl -sL https://raw.githubusercontent.com/jellyfin/jellyfin/v10.11.11/MediaBrowser.Model/Library/UserViewQuery.cs | sed -n '9,14p'
#         public UserViewQuery()
#         {
#             IncludeExternalContent = true;
#             PresetViews = Array.Empty<CollectionType?>();
#         }
#
# It is a script rather than a step in a workflow so it can be pointed at a
# server somebody started by hand and watched answering both ways, which is what
# makes the `absent` mode below evidence instead of an assertion. That mode is
# the whole reason this takes an expectation as an argument: a probe that only
# ever runs against a server carrying the plugin cannot tell a surface that
# appeared from a reader that says yes to everything.
#
# What it does not do. It says what the server returns, never what any client
# draws with it, which is #115 and is a matrix rather than a check. It asserts
# the surface is in the list and nothing about what is underneath, because every
# level of the surface answers empty until a shelf exists. And it drives the
# startup wizard, so it is for a server with no data rather than for one
# somebody uses.
#
# Usage:
#
#     tools/discover-surface-appears.sh <base-url> <surface-name> present|absent
#
# It needs curl and jq and reaches nothing but the base URL it was given.

set -euo pipefail

base="${1:-}"
surface="${2:-}"
expectation="${3:-}"

if [ -z "$base" ] || [ -z "$surface" ] || [ -z "$expectation" ]; then
  echo "usage: $0 <base-url> <surface-name> present|absent" >&2
  exit 2
fi

case "$expectation" in
  present | absent) ;;
  *)
    echo "::error::The expectation has to be present or absent, and it was '${expectation}'."
    exit 2
    ;;
esac

user=discover-probe
password=discover-probe-password
client='MediaBrowser Client="discover-probe", Device="gate", DeviceId="discover-probe", Version="0.0.0.0"'

# say <method> <path> [body] performs one request and prints the body. A status
# outside 2xx fails here rather than three lines later where the failure would
# read as the server answering something unexpected.
#
# The refusal goes to standard error rather than standard output, and that is
# not tidiness. Every call whose answer is not wanted sends standard output to
# /dev/null, so a refusal written there is thrown away and the run ends with the
# step's exit code and no line saying what the server refused. It did, on the
# first run of this script.
say() {
  local method="$1" path="$2" body="${3:-}"
  local out status
  out="$(mktemp)"

  if [ -n "$body" ]; then
    status=$(curl -sS -o "$out" -w '%{http_code}' -X "$method" \
      -H 'Content-Type: application/json' \
      -H "Authorization: ${client}${token:+, Token=\"$token\"}" \
      --data "$body" "${base}${path}")
  else
    status=$(curl -sS -o "$out" -w '%{http_code}' -X "$method" \
      -H "Authorization: ${client}${token:+, Token=\"$token\"}" \
      "${base}${path}")
  fi

  case "$status" in
    2*) ;;
    *)
      echo "::error::${method} ${path} answered ${status}." >&2
      cat "$out" >&2
      echo >&2
      rm -f "$out"
      return 1
      ;;
  esac

  cat "$out"
  rm -f "$out"
}

token=''

echo "--- waiting for ${base} to answer ---"
ready=0
for _ in $(seq 1 60); do
  if curl -sS -f -o /dev/null "${base}/System/Info/Public"; then
    ready=1
    break
  fi
  sleep 5
done

if [ "$ready" -ne 1 ]; then
  echo "::error::${base} never answered /System/Info/Public, so nothing below was asked."
  exit 1
fi

version=$(say GET /System/Info/Public | jq -r '.Version')
echo "The server at ${base} reports version ${version}."

echo "--- finishing the startup wizard ---"
say POST /Startup/Configuration \
  '{"UICulture":"en-US","MetadataCountryCode":"US","PreferredMetadataLanguage":"en"}' > /dev/null
say POST /Startup/User \
  "$(jq -n --arg n "$user" --arg p "$password" '{Name:$n,Password:$p}')" > /dev/null
say POST /Startup/RemoteAccess \
  '{"EnableRemoteAccess":true,"EnableAutomaticPortMapping":false}' > /dev/null
say POST /Startup/Complete > /dev/null

echo "--- signing in ---"
authenticated=$(say POST /Users/AuthenticateByName \
  "$(jq -n --arg n "$user" --arg p "$password" '{Username:$n,Pw:$p}')")

token=$(printf '%s' "$authenticated" | jq -r '.AccessToken')
user_id=$(printf '%s' "$authenticated" | jq -r '.User.Id')

if [ -z "$token" ] || [ "$token" = null ] || [ -z "$user_id" ] || [ "$user_id" = null ]; then
  echo "::error::The server answered the sign-in without an access token or a user id."
  printf '%s\n' "$authenticated"
  exit 1
fi

echo "Signed in as ${user}."

echo "--- what the server hands that user as views ---"
views=$(say GET "/UserViews?userId=${user_id}")
printf '%s' "$views" | jq -r '.Items[] | "  \(.Name)\t\(.Type)\t\(.CollectionType // "-")"'

found=$(printf '%s' "$views" | jq --arg name "$surface" '[.Items[] | select(.Name == $name)] | length')

if [ "$expectation" = present ]; then
  if [ "$found" -eq 0 ]; then
    echo "::error::A signed-in user's views do not carry '${surface}'. The surface does not reach a client."
    exit 1
  fi

  kind=$(printf '%s' "$views" | jq -r --arg name "$surface" 'first(.Items[] | select(.Name == $name) | .Type)')
  if [ "$kind" != "Channel" ]; then
    echo "::error::'${surface}' is in the views as a ${kind} rather than as a Channel, so it is not the surface this plugin offered."
    exit 1
  fi

  echo "'${surface}' is in the views a signed-in user is handed, as a Channel."
else
  if [ "$found" -ne 0 ]; then
    echo "::error::A server with no plugin installed still carries '${surface}' in its views, so the reading above is about something other than this plugin."
    exit 1
  fi

  echo "A server without the plugin carries no '${surface}', so the reading above is about the plugin rather than about the server."
fi
