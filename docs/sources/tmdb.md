# TMDB

Source: tmdb

Host: api.themoviedb.org

Host: image.tmdb.org

Terms read: 2026-08-06

The terms are at <https://www.themoviedb.org/api-terms-of-use>. The page states
`Last Updated: October 20, 2023`, and every clause quoted below was read there
on the date above. Nothing on this page is legal advice; the reasoning behind
that sentence is in [the directory's README](README.md).

An adapter for this source exists, and nothing in this plugin reaches it. It
landed under [#74](https://github.com/Flowfin/jellyfin-plugin-discover/issues/74):

    git grep -n ': IMetadataSource' -- 'Jellyfin.Plugin.Template/*.cs'
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:52:public sealed class TmdbSourceAdapter : IMetadataSource

    git grep -n 'TmdbSourceAdapter' -- 'Jellyfin.Plugin.Template/*.cs' ':!Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs' ; echo "exit=$?"
    exit=1

Nothing constructs it, so a server running this build sends this source nothing.
That splits a row below three ways rather than two: a limit this plugin does not
cross, an obligation something already meets, and an obligation whose code is in
the tree and is unreached. The rows say which of the three they are, and the
third is not the second.

## What the terms require

| Clause | Obligation                                                                                         | Where it lands                                                                                                                                                                                                                                                                                                                               | How a reader checks it                                                                                                                                                                                                                                                                                                                                                                                              |
| ------ | -------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1.C    | Do not cache anything obtained from TMDB for longer than six months.                               | Ninety days, answered as question 8 on [#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) on 2026-08-24 and held by `CatalogueRetention` in the plugin, which refuses a configured value above any active source's ceiling. [#68](https://github.com/Flowfin/jellyfin-plugin-discover/issues/68) carries what is left of it. | Read `CatalogueRetention.Default` and `CatalogueRetentionTests`. A refresh applies the number to what is stored: a document it did not refresh has records past the retention removed, and is itself removed where none survive, held by `CatalogueRetentionSweepTests`. Owed for the rest: nothing reads a stored document back for a client yet, so what a retention refuses to serve has nothing to serve it to. |
| 1.C    | Do not sell, lease or sublicense the API, access to it, or its content, or derive revenue from it. | [What may leave this server](#what-may-leave-this-server) below is the rule, from [#82](https://github.com/Flowfin/jellyfin-plugin-discover/issues/82).                                                                                                                                                                                      | Read the rule and the table under it. `tools/invariants/rules/no-source-content-off-this-server.rule` refuses an export and a telemetry component by name; artwork served from here and the reading half of a shared cache are outside it, and no export, shared cache or re-hosting exists in this tree to breach the clause with.                                                                                 |
| 1.C    | Do not conceal the identity of the application using the API.                                      | [#74](https://github.com/Flowfin/jellyfin-plugin-discover/issues/74) put the plugin's own name and version in a `User-Agent` header on every request the adapter builds.                                                                                                                                                                     | Read `Identity()` and the header line beside it in `TmdbSourceAdapter`. Owed for the rest: nothing constructs that adapter, so no request is made today.                                                                                                                                                                                                                                                            |
| 1.C    | Do not use excessive bandwidth or degrade the source's systems.                                    | [#78](https://github.com/Flowfin/jellyfin-plugin-discover/issues/78) holds the rate limit, the backoff and the stop. The adapter holds the half that is a reading: it turns a refusal for rate into one of its four answers and never asks a second time.                                                                                    | Read `SourceAnswer.RateLimited` and the `TooManyRequests` branch in `TmdbSourceAdapter`. Owed for the rest, and it is the larger half: nothing paces, waits or runs on a timer, and nothing constructs the adapter, so no request is made today.                                                                                                                                                                    |
| 1.C    | Do not use the API for image hosting.                                                              | [#62](https://github.com/Flowfin/jellyfin-plugin-discover/issues/62) has clients fetch artwork from the source rather than re-hosting it.                                                                                                                                                                                                    | Owed. Nothing in this tree serves an image.                                                                                                                                                                                                                                                                                                                                                                         |
| 1.C    | Do not use the API with a machine learning or artificial intelligence application.                 | Nowhere. A limit this plugin does not cross.                                                                                                                                                                                                                                                                                                 | The plugin has no such component and none is planned in any issue in this plan.                                                                                                                                                                                                                                                                                                                                     |
| 1.D    | On termination, cease use and purge the content held.                                              | [#72](https://github.com/Flowfin/jellyfin-plugin-discover/issues/72) throws the catalogue away on demand and on removal.                                                                                                                                                                                                                     | Owed. No catalogue exists to purge.                                                                                                                                                                                                                                                                                                                                                                                 |
| 2      | Commercial use needs a separate written agreement.                                                 | Nowhere. A limit this plugin does not cross.                                                                                                                                                                                                                                                                                                 | The plugin is not sold and nothing in it takes payment.                                                                                                                                                                                                                                                                                                                                                             |
| 3      | Display the TMDB logo to identify the use of TMDB, and carry the required notice.                  | [#76](https://github.com/Flowfin/jellyfin-plugin-discover/issues/76) rendered the notice in the two places this plugin can render anything: the surface's own description and the configuration page.                                                                                                                                        | Read `SourceNotice.Tmdb`, the summary in `DiscoverSurface`, and the paragraph in `configPage.html`. `ConfigurationPageTests.ThePageCarriesTheSourcesNoticeVerbatim` and `DiscoverSurfaceTests.TheSurfaceDescriptionCarriesTheSourcesNotice` refuse a change to the wording. Owed for the rest: which clients draw a channel's description is unobserved, which is that issue's second condition.                    |
| 3      | Any TMDB logo used must be less prominent than the marks identifying this application.             | [#76](https://github.com/Flowfin/jellyfin-plugin-discover/issues/76), answered by using no logo of the source's at all.                                                                                                                                                                                                                      | No TMDB logo is in the package or on the configuration page, and that page says using none is how the requirement is met rather than leaving the absence to be noticed. A requirement about relative prominence is met by there being nothing of the source's to be prominent.                                                                                                                                      |

Every "owed" above is an obligation nothing running meets. Most of them are owed
with no code behind them at all: the behaviour the clause governs does not exist
in this tree, and the issue named is where it arrives. Two are owed with code in
the tree that nothing reaches, which is a weaker kind of owed and not a met row.
A row is not met because an issue exists for it, and it is not met because the
code that would meet it has been written.

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

Two of the four are refused by something mechanical now, and two are not.
`tools/invariants/rules/no-source-content-off-this-server.rule` reads every
tracked `*.cs` file for a member that exports or uploads, and for the names a
telemetry, analytics or crash-reporting component arrives under, and the run
reds on a match. It landed with [#82](https://github.com/Flowfin/jellyfin-plugin-discover/issues/82),
and `tools/invariants/run.sh` is what makes it bite.

What is still read by a person or not at all is the rest, and it is the larger
half. The rule matches names and never payloads, so a member doing exactly what
an export does under a different name passes it, and so does a payload whose
contents nobody can read from a line of source. Artwork served from this server
is bytes taking a route rather than a name, and what stands between this plugin
and it is `no-network-outside-source-adapter` plus the record carrying a
location rather than an image, which is
[#62](https://github.com/Flowfin/jellyfin-plugin-discover/issues/62). The
reading side of a shared cache is a call to a host and is held the same way. No
test judges what a feature moves, and none of the four is refused by anything
outside this repository's own tracked text.

Two sentences are quoted rather than paraphrased, because their exact form is
what the terms require.

The notice, from section 3:

    This [website, program, service, application, product] uses TMDB and the
    TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.

The prominence requirement, from section 3:

    Any use of any TMDB logos in Your Application must be less prominent than
    the logos or marks that primarily describe or identify Your Application.

[#76](https://github.com/Flowfin/jellyfin-plugin-discover/issues/76) landed the
notice as a constant, so this page is no longer the only place it lives:

    git grep -n 'public const string Tmdb' -- Jellyfin.Plugin.Template/Surface/SourceNotice.cs
    Jellyfin.Plugin.Template/Surface/SourceNotice.cs:50:    public const string Tmdb =

The surface's description takes the text from there. The configuration page
carries a second copy, because it is a static asset embedded in the assembly
with no substitution step between a constant and the bytes a browser receives,
so what stands between the two copies is a test rather than a construction. That
bound is written at the constant, where somebody adding a third rendering meets
it.

## The key

Every operator supplies their own key. That is the answer to question 4 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), taken on
2026-08-24, and it was taken as the defensible reading of these terms: the rate
budget belongs to whoever registered, and this project does not answer for how
every install spends a key it registered once and shipped everywhere.

What the answer costs an operator is a registration before any shelf has
anything on it, and what it costs this project is that a revoked or exhausted
key is one install's problem rather than a fleet-wide outage.
[`../support.md`](../support.md) states that position.

What is left is not the decision.
[#77](https://github.com/Flowfin/jellyfin-plugin-discover/issues/77) holds the
rest of it: there is nowhere for an operator to put a key yet, so nothing
validates one when it is entered and nothing tells an operator what to do while
none is configured. The adapter takes the credential as an argument and declines
to ask when it has none, which is the half that exists:

    git grep -n 'private readonly bool _configured' -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:76:    private readonly bool _configured;

## What this page does not cover

It reads the API terms of use and nothing else. TMDB's general terms of use are
referenced from clause 1.B and have not been read for this page, so any
obligation that lives only in that document is absent here rather than absent
from the source. The same applies to anything the source states outside its
terms, in documentation or in a response header.

It also says nothing about whether this reading is correct in law. It is an
engineering reading, made so the code has something to be checked against.
