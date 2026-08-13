# TMDB

Source: tmdb

Host: api.themoviedb.org

Host: image.tmdb.org

Terms read: 2026-08-06

The terms are at <https://www.themoviedb.org/api-terms-of-use>. The page states
`Last Updated: October 20, 2023`, and every clause quoted below was read there
on the date above. Nothing on this page is legal advice; the reasoning behind
that sentence is in [the directory's README](README.md).

No adapter for this source exists yet. [#74](https://github.com/iderex/jellyfin-plugin-discover/issues/74)
builds it, and until it does, every row below is a limit this plugin does not
cross rather than a behaviour it has. The rows say which of the two they are.

## What the terms require

| Clause | Obligation                                                                                         | Where it lands                                                                                                                                                                                            | How a reader checks it                                                                                                                    |
| ------ | -------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| 1.C    | Do not cache anything obtained from TMDB for longer than six months.                               | [#68](https://github.com/iderex/jellyfin-plugin-discover/issues/68) picks the retention number, which question 8 on [#2](https://github.com/iderex/jellyfin-plugin-discover/issues/2) caps at six months. | Owed. No catalogue exists to hold anything, so nothing is retained today.                                                                 |
| 1.C    | Do not sell, lease or sublicense the API, access to it, or its content, or derive revenue from it. | [What may leave this server](#what-may-leave-this-server) below is the rule, from [#82](https://github.com/Flowfin/jellyfin-plugin-discover/issues/82).                                                   | Read the rule and the table under it. Nothing refuses a breach of it, and no export, shared cache or re-hosting exists to breach it with. |
| 1.C    | Do not conceal the identity of the application using the API.                                      | [#74](https://github.com/iderex/jellyfin-plugin-discover/issues/74) sends a request identifying this plugin.                                                                                              | Owed. No request is made today.                                                                                                           |
| 1.C    | Do not use excessive bandwidth or degrade the source's systems.                                    | [#78](https://github.com/iderex/jellyfin-plugin-discover/issues/78) holds the rate limit, the backoff and the stop.                                                                                       | Owed. No request is made today.                                                                                                           |
| 1.C    | Do not use the API for image hosting.                                                              | [#62](https://github.com/iderex/jellyfin-plugin-discover/issues/62) has clients fetch artwork from the source rather than re-hosting it.                                                                  | Owed. Nothing in this tree serves an image.                                                                                               |
| 1.C    | Do not use the API with a machine learning or artificial intelligence application.                 | Nowhere. A limit this plugin does not cross.                                                                                                                                                              | The plugin has no such component and none is planned in any issue in this plan.                                                           |
| 1.D    | On termination, cease use and purge the content held.                                              | [#72](https://github.com/iderex/jellyfin-plugin-discover/issues/72) throws the catalogue away on demand and on removal.                                                                                   | Owed. No catalogue exists to purge.                                                                                                       |
| 2      | Commercial use needs a separate written agreement.                                                 | Nowhere. A limit this plugin does not cross.                                                                                                                                                              | The plugin is not sold and nothing in it takes payment.                                                                                   |
| 3      | Display the TMDB logo to identify the use of TMDB, and carry the required notice.                  | [#76](https://github.com/iderex/jellyfin-plugin-discover/issues/76) renders the notice where a user sees it.                                                                                              | Owed. The notice appears nowhere in the tree today.                                                                                       |
| 3      | Any TMDB logo used must be less prominent than the marks identifying this application.             | [#76](https://github.com/iderex/jellyfin-plugin-discover/issues/76), including the answer that no logo is used at all.                                                                                    | Owed. No logo ships in the package today.                                                                                                 |

Every "owed" above is a row with no code behind it. The obligation still holds,
and it holds as a limit rather than as a plan: the behaviour it forbids does not
exist in this tree, and the issue named is where the behaviour that has to meet
it arrives. A row is not met because an issue exists for it.

## What may leave this server

Clause 1.C forbids selling, leasing or sublicensing the API, access to it, or
its content. The clause is about a commercial arrangement and this rule is
narrower than the clause on purpose, because a rule a feature can be checked
against has to say what may move rather than what may be sold.

Content means what a response carried: names, original names, descriptions,
dates, artwork locations, and anything derived from those that reproduces them.
An identifier is not content. It is a reference to a record kept at the source,
it carries none of the record, and whoever holds one has to ask the source
themselves.

The rule, in four sentences.

1. Content stays on the server that fetched it. It reaches the users of that
   server, through the surface, and it reaches nothing else.
2. Identifiers may cross to another plugin on the same server, and to nothing
   off it. That is the handover in
   [#94](https://github.com/Flowfin/jellyfin-plugin-discover/issues/94), which
   passes provider identifiers rather than the catalogue record for exactly this
   reason.
3. Artwork is referenced where the source keeps it and no copy is held, so the
   question of passing it on does not arise. What does happen is a client
   fetching from the source's own host, which tells that host something, and
   saying so is
   [#116](https://github.com/Flowfin/jellyfin-plugin-discover/issues/116).
4. Nothing aggregates content across installations. One server's catalogue is
   one server's.

### Every feature in this plan that moves source data

| Feature                                                                                                                                                    | What moves                                                                                                                                                                                                              | Against the rule                                                                                                                                                         |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| The surface, [#54](https://github.com/Flowfin/jellyfin-plugin-discover/issues/54) and [#55](https://github.com/Flowfin/jellyfin-plugin-discover/issues/55) | Names, years and descriptions, to a signed-in user of this server.                                                                                                                                                      | Allowed by 1. This is the use the source is reached for.                                                                                                                 |
| The catalogue at rest, [#65](https://github.com/Flowfin/jellyfin-plugin-discover/issues/65)                                                                | Content onto the disk of the server that fetched it.                                                                                                                                                                    | Allowed by 1, and bounded in time by the retention in [#68](https://github.com/Flowfin/jellyfin-plugin-discover/issues/68) rather than by this rule.                     |
| Artwork, [#62](https://github.com/Flowfin/jellyfin-plugin-discover/issues/62)                                                                              | A location, not an image. The image travels from the source's host to whoever fetches it, and never through this plugin.                                                                                                | Allowed by 3.                                                                                                                                                            |
| The seam handover, [#94](https://github.com/Flowfin/jellyfin-plugin-discover/issues/94)                                                                    | Provider identifiers, the kind, a title and year for display, the asking user's identifier, a want identifier, a version. The list is fixed in [0004](../decisions/0004-what-crosses-the-seam-to-a-requests-plugin.md). | Allowed by 2. It crosses to a plugin on the same server and not off it, and what crosses is a reference plus the little a person needs to recognise what they asked for. |
| Fixtures in this repository, [#48](https://github.com/Flowfin/jellyfin-plugin-discover/issues/48)                                                          | Captured responses, into a public repository, which is content leaving the server it was fetched to.                                                                                                                    | The one case this rule bears on directly. #48 minimises what is committed to the shape and the field names with synthetic values, and this rule is why it has to.        |
| The diagnostic page, [#110](https://github.com/Flowfin/jellyfin-plugin-discover/issues/110)                                                                | Text an operator copies into a bug report, which leaves the server by hand.                                                                                                                                             | Allowed only while it carries counts, times and identifiers. A diagnostic that quoted a title or a description would take content off the server, and #110 owes that.    |

### Named as refused

Each of these is a feature somebody will propose, and each breaches the rule
above. They are written here so that the answer is a decision already taken
rather than an argument had once per proposal.

- An export of the catalogue, in any format, to anywhere off the server.
- Artwork served from the server rather than referenced at the source, which
  would also make this plugin an image host and breach the separate clause
  against that.
- A cache, index or catalogue shared between installations, in either
  direction.
- Telemetry, analytics or crash reporting that carries a title, a description
  or any other field a response supplied.

A future feature that needs one of these is not refused by anything mechanical.
Nothing in `tools/invariants/rules/` has an export or an outbound payload as its
subject, and no test judges what a feature moves, so this list is read by a
person or not at all.

Two sentences are quoted rather than paraphrased, because their exact form is
what the terms require.

The notice, from section 3:

    This [website, program, service, application, product] uses TMDB and the
    TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.

The prominence requirement, from section 3:

    Any use of any TMDB logos in Your Application must be less prominent than
    the logos or marks that primarily describe or identify Your Application.

[#76](https://github.com/iderex/jellyfin-plugin-discover/issues/76) is where the
notice text lands in one place that every rendering takes it from. Until then it
lives here and nowhere else, so there is one copy rather than two.

## The key

The key this source is reached with is not decided.
[#77](https://github.com/iderex/jellyfin-plugin-discover/issues/77) holds it and
it is question 4 on [#2](https://github.com/iderex/jellyfin-plugin-discover/issues/2):
whether this project registers a key of its own and ships it, or every operator
supplies theirs. The terms bear on that question and this page does not answer
it.

## What this page does not cover

It reads the API terms of use and nothing else. TMDB's general terms of use are
referenced from clause 1.B and have not been read for this page, so any
obligation that lives only in that document is absent here rather than absent
from the source. The same applies to anything the source states outside its
terms, in documentation or in a response header.

It also says nothing about whether this reading is correct in law. It is an
engineering reading, made so the code has something to be checked against.
