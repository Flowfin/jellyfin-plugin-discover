# The limits and the awkward cases

Everything this plugin cannot make good, in one place, so a reader deciding
whether to put it on their server meets it here rather than after.

Two rules hold the page together.

It points rather than restates. Every limit below was established somewhere
else, on an issue or on a page beside this one, and that is where the reasoning
and the commands are. A limit written twice drifts, and the copy a user reads
is the one nobody was looking at while the behaviour changed.

It says how each limit is known. A thing read out of the server's source and a
thing watched happening on a running server are different claims, and a page
that prints them in the same voice is a page that has quietly upgraded one of
them. The column below carries that difference and the section after the tables
says what it currently costs.

## Read this first

Almost nothing described in this repository is built. That is not a limit of
the kind the tables hold, it is the frame every row sits in, and it is stated
where somebody installing the plugin meets it rather than only here:

    git grep -n -A3 'description:' build.yaml

The rows below are therefore of two kinds. Some are limits of what the plugin
does today. Others are limits of the server this plugin builds on, established
in advance because they decide what can be built at all. Each row's pointer
says which.

## What a user meets

| What happens                                                                                                                                                                                                                                                                            | Where it is established                                                                                                                | How it is known |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- | --------------- |
| Pressing play on a title the server does not have gets a successful answer describing one source that cannot be played, rather than a refusal a client could draw as a message. Each client does something different with that, and none of the differences is this plugin's to choose. | [#56](https://github.com/Flowfin/jellyfin-plugin-discover/issues/56)                                                                   | Read            |
| A shelf standing empty and a shelf that no longer exists are one answer with no entries and a total of zero. Nothing a client can see separates them.                                                                                                                                   | [#54](https://github.com/Flowfin/jellyfin-plugin-discover/issues/54)                                                                   | Read            |
| What a given client draws from what the server sends is untried. The server's half is checkable and the client's half needs a client, a screen and somebody looking at it.                                                                                                              | [`what-a-green-suite-proves.md`](what-a-green-suite-proves.md), [#115](https://github.com/Flowfin/jellyfin-plugin-discover/issues/115) | Read            |
| Whether a user's own device contacts the source's image host, or whether the server fetches artwork on their behalf, is open. An operator should assume the open answer rather than the comfortable one.                                                                                | [`what-leaves-the-server.md`](what-leaves-the-server.md), [#62](https://github.com/Flowfin/jellyfin-plugin-discover/issues/62)         | Read            |

## What an operator meets

| What happens                                                                                                                                                                                     | Where it is established                                                                                                                    | How it is known |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------ | --------------- |
| A credential the source rejected reaches the plugin as the same answer an absent credential does, with none of the source's own words in it.                                                     | [#78](https://github.com/Flowfin/jellyfin-plugin-discover/issues/78), [#92](https://github.com/Flowfin/jellyfin-plugin-discover/issues/92) | Read            |
| A shelf named something the shipped source has no question for produces that same answer a third time, so a naming mistake and an unconfigured server look alike.                                | [#86](https://github.com/Flowfin/jellyfin-plugin-discover/issues/86)                                                                       | Read            |
| Where a source states a wait as a date rather than as a number of seconds, that wait is not read, and the refusal arrives as one that named no wait at all.                                      | [#78](https://github.com/Flowfin/jellyfin-plugin-discover/issues/78)                                                                       | Read            |
| The surface's name cannot be changed by an operator, and that is deliberate rather than missing. Changing it in a release is a breaking change rather than a wording improvement.                | [`title-identity.md`](title-identity.md), [#60](https://github.com/Flowfin/jellyfin-plugin-discover/issues/60)                             | Read            |
| Installing an older build over a newer one is not supported. The configuration is the half that loses data on a server rather than in a build, so a copy of it is worth keeping before doing it. | [`what-a-downgrade-does.md`](what-a-downgrade-does.md)                                                                                     | Read            |

## What is left behind

| What happens                                                                                                                                                                                                                                                                      | Where it is established                                                                                                                        | How it is known       |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- | --------------------- |
| A rename of the surface makes every title a new row, and the previous subtree is not under any parent the refresh queries. What goes looking for it is a clean-up on a different query rather than the refresh.                                                                   | [`title-identity.md`](title-identity.md), [#219](https://github.com/Flowfin/jellyfin-plugin-discover/issues/219)                               | Read                  |
| Removing a row does not remove what a user marked on it. Both server lines move that data to a placeholder and stamp when the move happened.                                                                                                                                      | [`title-identity.md`](title-identity.md), [#108](https://github.com/Flowfin/jellyfin-plugin-discover/issues/108)                               | Read                  |
| The task that deletes detached user data once its stamp is old enough offers no trigger of its own, so on a server where nobody has given it one it does not run. A mark on a title this plugin removed therefore outlives the item, the uninstall and the server's own clean-up. | [`title-identity.md`](title-identity.md), [#108](https://github.com/Flowfin/jellyfin-plugin-discover/issues/108)                               | Read                  |
| The plugin's own folder goes on an uninstall and the configuration file does not, because it is written elsewhere. Removing it is a manual step.                                                                                                                                  | [#108](https://github.com/Flowfin/jellyfin-plugin-discover/issues/108), [#112](https://github.com/Flowfin/jellyfin-plugin-discover/issues/112) | Read                  |
| One operation removes everything this plugin wrote under its own directory. Running it on a fresh install and running it twice are both quiet, and any write afterwards brings the directory back.                                                                                | [#72](https://github.com/Flowfin/jellyfin-plugin-discover/issues/72)                                                                           | Watched, in the suite |
| That operation reaches nothing in the server's own database. Rows the server made from this plugin's answers go by the server's own routes rather than by it, and the two are separate events.                                                                                    | [#72](https://github.com/Flowfin/jellyfin-plugin-discover/issues/72), [#108](https://github.com/Flowfin/jellyfin-plugin-discover/issues/108)   | Read                  |

## What has never been measured

| What happens                                                                                                                                                                                                          | Where it is established                                                | How it is known |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- | --------------- |
| How long a refresh takes on a large library, and what the server is like while it runs, is untimed. Every piece of the per-title work is cheap at a hundred titles and none of it has been run at a hundred thousand. | [#194](https://github.com/Flowfin/jellyfin-plugin-discover/issues/194) | Not measured    |
| What the catalogue costs on disk and what it adds to the library database is unmeasured.                                                                                                                              | [#71](https://github.com/Flowfin/jellyfin-plugin-discover/issues/71)   | Not measured    |
| Whether a source's terms are met in spirit as well as in code is a judgement rather than a check. The pages under `docs/sources/` are where the reading is, and no route reads their prose.                           | [`what-a-green-suite-proves.md`](what-a-green-suite-proves.md)         | Not measured    |

## Everything above was read, and that is a limit of this page

One row says watched and the rest say read. What that means is that every claim
here except that one comes from the server's source or from this tree, with the
command beside it on the page or issue that holds it, and not from a server that
was started and looked at.

Read is not weak. A behaviour read out of the code at two named tags is a
better claim than a memory of a server somebody once ran. It is also the claim
that has already been wrong here once: a limit about what a rename leaves
behind was written from a careful reading of one route, and the behaviour had
two, which is
[#219](https://github.com/Flowfin/jellyfin-plugin-discover/issues/219). The
reading was not careless. It was a reading, and a reading can miss a route in a
way watching cannot.

So this page does not yet do the thing
[#114](https://github.com/Flowfin/jellyfin-plugin-discover/issues/114) asks of
it, which is that each entry say what a user or operator actually sees in the
words of what was observed. A server is booted by
[#38](https://github.com/Flowfin/jellyfin-plugin-discover/issues/38) and a
client is put in front of a person by
[#115](https://github.com/Flowfin/jellyfin-plugin-discover/issues/115). As each
of those turns a row from read into seen, the row changes and this section gets
shorter. Nothing here should be rewritten in the voice of an observation before
then.

## What this list may be missing

Nothing derives it. I assembled it by reading the open issues and the pages
under `docs/`, and no route in this tree compares the two against this page, so
a limit established tomorrow does not appear here by itself and nothing goes
red for its absence. The same is true of every other page under `docs/`.

The two populations a reader can walk to check it:

    git ls-tree -r --name-only origin/master -- docs/

    gh issue list --repo Flowfin/jellyfin-plugin-discover --state open --limit 300 \
      --json number,title --jq '.[] | "\(.number)\t\(.title)"'

One thing is deliberately absent rather than missed. What pressing play looks
like in each client, as opposed to what the server sends, is not here because
nobody has tried a client. It is one row above, marked read, rather than a set
of rows describing behaviour nobody has seen.

This page is not linked from `README.md`, and neither is any other page under
`docs/`. What the README carries is
[#111](https://github.com/Flowfin/jellyfin-plugin-discover/issues/111).
