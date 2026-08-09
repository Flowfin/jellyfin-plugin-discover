---
name: Bug report
about: Something the plugin does that it should not, or does not do that it should
title: ""
labels: bug
assignees: ""
---

Read
[the status section of the README](https://github.com/Flowfin/jellyfin-plugin-discover#status)
first. Most of what this plugin is described as doing is not built yet, so a
report about browsing, shelves or a metadata source is likely to be answered
with the issue that carries the work rather than with a fix.

## Server

Which Jellyfin server version, and how it is run: a container image and its tag,
a distribution package, or something else. The version on its own is not enough,
because the package declares a floor and a server below it does not load the
plugin at all.

## Client

Which client, and its version. Web, Android, Android TV, a television app,
something else. Name it even when the answer feels irrelevant. What a user sees
here is drawn by the client rather than by the plugin, so one server state looks
different in two of them and a report with no client named cannot be placed.

## Plugin version

The version the dashboard shows on the plugin's page, and whether it was
installed from a manifest or copied in by hand.

## What happened

What you saw, in the words of what you saw. A screenshot where the complaint is
about what was drawn.

## What you expected instead

What you thought would happen, and what led you to expect it.

## How to reproduce it

The steps, from a state somebody else can reach. Say whether it happens every
time or only sometimes.

## The server log

The lines around the failure, from the server's own log rather than from the
dashboard's summary. Remove any API key before pasting: a source key in a URL is
still a key.

## Other plugins

Which other plugins are installed and enabled. Say "none" where that is the
case, because a report from a server carrying only this plugin and one from a
server carrying eight are different reports.
