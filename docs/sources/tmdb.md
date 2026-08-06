# TMDB

Source: tmdb

Host: api.themoviedb.org

Host: image.tmdb.org

The terms are at <https://www.themoviedb.org/api-terms-of-use>. The page states
`Last Updated: October 20, 2023`, and every clause quoted below was read there
on the date above. Nothing on this page is legal advice; the reasoning behind
that sentence is in [the directory's README](README.md).

No adapter for this source exists yet. [#74](https://github.com/iderex/jellyfin-plugin-discover/issues/74)
builds it, and until it does, every row below is a limit this plugin does not
cross rather than a behaviour it has. The rows say which of the two they are.

## What the terms require

| Clause | Obligation                                                                                         | Where it lands                                                                                                                                                                                            | How a reader checks it                                                                         |
| ------ | -------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| 1.C    | Do not cache anything obtained from TMDB for longer than six months.                               | [#68](https://github.com/iderex/jellyfin-plugin-discover/issues/68) picks the retention number, which question 8 on [#2](https://github.com/iderex/jellyfin-plugin-discover/issues/2) caps at six months. | Owed. No catalogue exists to hold anything, so nothing is retained today.                      |
| 1.C    | Do not sell, lease or sublicense the API, access to it, or its content, or derive revenue from it. | [#82](https://github.com/iderex/jellyfin-plugin-discover/issues/82) writes the rule for what may leave the server and in what form.                                                                       | Owed. A limit this plugin does not cross; no export, no shared cache and no re-hosting exists. |
| 1.C    | Do not conceal the identity of the application using the API.                                      | [#74](https://github.com/iderex/jellyfin-plugin-discover/issues/74) sends a request identifying this plugin.                                                                                              | Owed. No request is made today.                                                                |
| 1.C    | Do not use excessive bandwidth or degrade the source's systems.                                    | [#78](https://github.com/iderex/jellyfin-plugin-discover/issues/78) holds the rate limit, the backoff and the stop.                                                                                       | Owed. No request is made today.                                                                |
| 1.C    | Do not use the API for image hosting.                                                              | [#62](https://github.com/iderex/jellyfin-plugin-discover/issues/62) has clients fetch artwork from the source rather than re-hosting it.                                                                  | Owed. Nothing in this tree serves an image.                                                    |
| 1.C    | Do not use the API with a machine learning or artificial intelligence application.                 | Nowhere. A limit this plugin does not cross.                                                                                                                                                              | The plugin has no such component and none is planned in any issue in this plan.                |
| 1.D    | On termination, cease use and purge the content held.                                              | [#72](https://github.com/iderex/jellyfin-plugin-discover/issues/72) throws the catalogue away on demand and on removal.                                                                                   | Owed. No catalogue exists to purge.                                                            |
| 2      | Commercial use needs a separate written agreement.                                                 | Nowhere. A limit this plugin does not cross.                                                                                                                                                              | The plugin is not sold and nothing in it takes payment.                                        |
| 3      | Display the TMDB logo to identify the use of TMDB, and carry the required notice.                  | [#76](https://github.com/iderex/jellyfin-plugin-discover/issues/76) renders the notice where a user sees it.                                                                                              | Owed. The notice appears nowhere in the tree today.                                            |
| 3      | Any TMDB logo used must be less prominent than the marks identifying this application.             | [#76](https://github.com/iderex/jellyfin-plugin-discover/issues/76), including the answer that no logo is used at all.                                                                                    | Owed. No logo ships in the package today.                                                      |

Every "owed" above is a row with no code behind it. The obligation still holds,
and it holds as a limit rather than as a plan: the behaviour it forbids does not
exist in this tree, and the issue named is where the behaviour that has to meet
it arrives. A row is not met because an issue exists for it.

## The wording the terms fix

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
