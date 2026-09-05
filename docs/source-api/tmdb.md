# What TMDB's API does

This page is what the source's own API documentation says about the four things
this plugin has to decide against it: how many requests it will take, what
language and region it can be asked in, whether adult titles can be excluded
before they arrive, and whether a content rating ceiling can be asked for.

It is not the terms of use. Those are in [`docs/sources/tmdb.md`](../sources/tmdb.md),
which is where an obligation on this plugin's behaviour lives. Nothing here is
an obligation; it is what the API offers, and the two go stale at different
rates and for different reasons.

Read on 2026-08-08, except for `## The six addresses this plugin asks`, which
was read on 2026-08-18, and `### The two addresses a rating comes from`, which
was read on 2026-08-27. Each carries its own date because the readings are of
different pages and go stale separately. Every value below carries the page it
came from. A value with no page next to it is not on this page.

## The request budget

Read at <https://developer.themoviedb.org/docs/rate-limiting>.

The page states that the legacy limit of 40 requests per 10 seconds was disabled
in December 2019, and that what stands in its place is deliberately vague:

    we do still have some upper limits to help mitigate needlessly high bulk
    scraping. They sit somewhere in the 40 requests per second range

with, in the same place, "This limit could change at any time". A caller that
goes over gets a `429` and the page says to respect it.

What that is worth to #87. It is a number, and it is a number the source
declines to commit to, in a sentence written to say so. A cadence divided out of
it is a cadence divided out of an approximation that may move without notice, so
the arithmetic that issue asks for has to carry that rather than quote 40 as a
budget. The `429` is the part that does not move: whatever the ceiling is on a
given day, the source tells a caller it has been reached, and #78 is where
backing off on that answer lives.

The page names no rate-limit response header. Whether the response carries one
is not answered by the documentation and is not asserted here.

## Language and region

Read at <https://developer.themoviedb.org/docs/languages> and at
<https://developer.themoviedb.org/reference/discover-movie>.

The `language` parameter is an ISO 639-1 code, optionally paired with an ISO
3166-1 alpha-2 country code, in the form `en-US` or `pt-BR`. On the discover
endpoint its default is `en-US`. `region` is a separate parameter on the same
endpoint.

The languages page names two things that are not translated at all, person names
and characters, and describes them as gaps rather than as a rule.

What is not stated, and it is the thing #81 went looking for. Neither page says
what comes back when a title has no translation in the requested language.
Whether a field is absent, empty, or filled from the original language is not
documented on either of the two pages read, so the fallback order #81 asks for
cannot be sourced from the source. It is either this plugin's decision, made
explicitly, or a measurement somebody takes against a live response and records
here. Choosing it by watching what one response happened to do would be neither.

## Excluding adult titles

Read at <https://developer.themoviedb.org/reference/discover-movie>.

`include_adult` is a boolean on the discover endpoint and its documented default
is `false`.

What that is worth to #93. The exclusion is expressible at the request, so the
titles do not have to arrive and be discarded, and it is already the direction
the parameter defaults to. That answers the first of that issue's conditions in
the cheaper direction and it removes the case its fifth condition was written
against, for this endpoint. Sending the parameter explicitly is still worth
doing rather than relying on the default, because a default is the source's to
change and an explicit `false` is this plugin's statement.

This is what the discover endpoint documents. Whether every other endpoint this
plugin might call takes the same parameter is not read here.

## A content rating ceiling

Read at <https://developer.themoviedb.org/reference/discover-movie>.

The discover endpoint documents `certification`, `certification.gte` and
`certification.lte`, each of which the page says is used together with
`certification_country`.

What that is worth to #93 and to #57. A ceiling is expressible at the request
rather than only on the way out, which is what #93's second condition wanted, and
it is expressed per country, which means the ceiling is not a single value but a
value plus the country whose rating scheme it belongs to. That is a shape the
configuration has to carry, and it is cheaper to know before #103 draws a control
for it than afterwards.

What the page does not say is what comes back for a title the source holds no
certification for. That is the case #93's third condition is about, and its
answer there, that an absent rating is treated as above any configured maximum,
does not depend on this page.

## Sorting and paging

Read at <https://developer.themoviedb.org/reference/discover-movie>.

`sort_by` is an enumeration of fourteen values and its default is
`popularity.desc`. `page` is an integer whose default is 1.

What is not stated on any page read here: how many results one page returns, and
whether there is a highest page number. #61 declares a page size of this
plugin's own and #91 needs to know whether a rank is recoverable from a response
or only from a position in it, and neither question is answered by what is
written above. Both need a measurement against a live response or a page nobody
has found yet, and the absence is recorded rather than filled in.

One of those two is answered by the section below rather than by a live
response, and the sentence above is left as it stands because it says what the
discover endpoint states. A reader who stops here has read a third of the
question: the discover endpoint is not an endpoint this plugin asks.

## The six addresses this plugin asks

Everything above is the discover endpoint, and the adapter in this tree asks it
for nothing. The six paths it builds are literals chosen by a switch:

    git grep -nE '"(trending|tv|movie)' -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:834:            "trending" => series ? "trending/tv/week" : "trending/movie/week",
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:835:            "popular" => series ? "tv/popular" : "movie/popular",
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:836:            "top-rated" => series ? "tv/top_rated" : "movie/top_rated",
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:852:        if (locale.Region is { } region && path is "movie/popular" or "movie/top_rated")

The fourth match is not a path. It is the test in the same method that decides
which of the six a region reaches, and it names two of them by the same
literals, so a pattern reading the paths reads it too. What it is for is the
section on a region below.

Read on 2026-08-18, one reference per address:

- `trending/movie/week` at <https://developer.themoviedb.org/reference/trending-movies>
- `trending/tv/week` at <https://developer.themoviedb.org/reference/trending-tv>
- `movie/popular` at <https://developer.themoviedb.org/reference/movie-popular-list>
- `tv/popular` at <https://developer.themoviedb.org/reference/tv-series-popular-list>
- `movie/top_rated` at <https://developer.themoviedb.org/reference/movie-top-rated-list>
- `tv/top_rated` at <https://developer.themoviedb.org/reference/tv-series-top-rated-list>

The six do not agree with one another, which is why this is a table per address
rather than a paragraph about the family:

| Address               | Query parameters documented  | `adult` on a result | Certification on a result | `popularity`, `vote_average`, `vote_count` on a result |
| --------------------- | ---------------------------- | ------------------- | ------------------------- | ------------------------------------------------------ |
| `trending/movie/week` | `language`                   | yes                 | no                        | yes                                                    |
| `trending/tv/week`    | `language`                   | yes                 | no                        | yes                                                    |
| `movie/popular`       | `language`, `page`, `region` | yes                 | no                        | yes                                                    |
| `tv/popular`          | `language`, `page`           | no                  | no                        | yes                                                    |
| `movie/top_rated`     | `language`, `page`, `region` | yes                 | no                        | yes                                                    |
| `tv/top_rated`        | `language`, `page`           | no                  | no                        | yes                                                    |

`time_window` is the trending path's own last segment rather than a query
parameter, and both trending references document it as the enumeration `day` or
`week` with a default of `day`. This plugin spells `week` into the path.

`language` carries a documented default of `en-US` on all six.

### Paging is documented on four of the six

Neither trending reference documents `page` or any other paging parameter. The
adapter sends `page` to all six addresses. What the two trending addresses do
with a parameter their reference does not list is not something a reading
settles in either direction, and an undocumented parameter that is honoured is
exactly the case a reference page cannot report. That half stays a question for
the source rather than for its pages, and #61 already carries it with the
assertion that pins the address the adapter built.

The envelope around `results` on `movie/popular` documents `page`,
`total_pages` and `total_results` beside it, so how many pages a question has is
recoverable from the answer. How many results one page returns, and whether
there is a highest page number, are stated on none of the six, so the page size
#61 declares stays this plugin's own number rather than the source's.

### A region can be asked for on two of the six

`region` is documented on `movie/popular` and on `movie/top_rated`, and on
neither trending address and neither television address.

What that is worth to #81. Its first condition asks for a language and a region
sent to the source. Language is expressible on every address this plugin asks,
so that half is a change to what the adapter builds rather than a thing the
source withholds. Region is expressible on a third of them, and the four that do
not document it are not closed by sending it anyway. So the answer that issue
takes has to be per address rather than one pair of values sent everywhere, and
a shelf whose address takes no region is a shelf whose region is whatever the
source decides.

That answer is taken, and it is the per-address one this table asks for. A
language stated to the adapter is sent to all six; a region stated to it is sent
to `movie/popular` and to `movie/top_rated` and to no other address:

    git grep -n 'ARegionReachesOnlyTheAddressesThatDocumentOne' -- Jellyfin.Plugin.Template.Tests/TmdbSourceAdapterTests.cs
    Jellyfin.Plugin.Template.Tests/TmdbSourceAdapterTests.cs:646:    public async Task ARegionReachesOnlyTheAddressesThatDocumentOne(string name, DiscoverTitleKind kind, string expected)

Four of that test's six rows are the addresses that must not carry one, which is
the half a reader should check: the two rows that do carry it would pass against
an adapter appending a region everywhere.

Where the pair comes from is not settled by this page and is not settled in the
tree. The adapter is told, in the same way it is told a credential, and nothing
on a running server tells it anything yet.

### No address this plugin asks returns a content rating

None of the six documents a certification, a content rating or an age rating on
a result.

What that is worth to #55 and to #57. #55's fourth condition carries the rating
where the source supplies one, so that #57 has something to filter on. On the
addresses this plugin asks, the source supplies none, so a rating is a second
request per title rather than a field taken off an answer already in hand. That
is a cost against the budget in `## The request budget` and against #78, and it
is a different decision from the per-country shape recorded under
`## A content rating ceiling`, which is about asking for a ceiling rather than
about reading one back.

This paragraph said the second request was against an endpoint nobody here had
read. Two have been read since, one per kind, and the section below is what they
document. What the sentence concluded is unchanged: the cost is per title.

### The two addresses a rating comes from

Read on 2026-08-27, one reference per kind:

- `movie/{movie_id}/release_dates` at <https://developer.themoviedb.org/reference/movie-release-dates>
- `tv/{series_id}/content_ratings` at <https://developer.themoviedb.org/reference/tv-series-content-ratings>

Neither documents a query parameter at all. Each takes the title's own
identifier in the path and nothing else, so neither can be asked about a shelf.

The two do not answer in the same shape, and the difference is not cosmetic:

| Address                          | Where the rating is                                              | What is beside it in the same entry                        |
| -------------------------------- | ---------------------------------------------------------------- | ---------------------------------------------------------- |
| `movie/{movie_id}/release_dates` | `certification`, inside a `release_dates` array inside `results` | `iso_639_1`, `note`, `release_date`, `type`, `descriptors` |
| `tv/{series_id}/content_ratings` | `rating`, directly on an entry of `results`                      | `descriptors`                                              |

Both carry `iso_3166_1` on every entry of `results`, so neither returns one
rating for a title: both return a rating per country. The movie address nests
one level deeper, and the certification hangs off an entry of the inner array
rather than off the country.

What one costs. A shelf of twenty titles is twenty of these requests on top of
the one request that fetched the shelf, and the six shelves in
[`docs/shelves.md`](../shelves.md) at twenty titles each are a hundred and
twenty on top of six. That is twenty-one times the requests a refresh makes
today, against the ceiling under `## The request budget` that the source
declines to commit to.

Whether the second request can be folded into another one. Read at
<https://developer.themoviedb.org/docs/append-to-response> on 2026-08-27:

    The movie, TV show, TV season, TV episode and person detail methods all
    support a query parameter called `append_to_response`.

Detail methods, which none of the six addresses this plugin asks is. So the
mechanism does not fold a certification into a list answer. What it folds is a
certification into a detail request for one title, which is still one request
per title. Whether it can be used on a list endpoint is not stated on that page
and is not inferred here.

What that is worth to #55, #57 and #93:

- #55's fourth condition has a price rather than an unknown. Carrying the rating
  is one request per title, from whichever of the two addresses the kind
  decides, and the per-country shape arrives with it rather than being
  avoidable.
- #57's third condition asks for a rule for a title whose rating the source did
  not supply. On these two addresses that case has two forms rather than one: a
  `results` array that is empty, and one that holds no entry for the country the
  ceiling belongs to. Neither reference says how often either occurs and nothing
  here measures it.
- #93's second condition wants titles above a maximum not stored at all rather
  than filtered on the way out. On these two addresses the ceiling is read back
  per title rather than asked for, because the parameters under
  `## A content rating ceiling` are the discover endpoint's and none of the six
  is discover. So a title kept out of the catalogue still costs the request that
  found out it should be.

What this section does not settle. Neither reference was asked against a live
response, so what an answer holds for a title with no certification is
documented rather than observed. Neither page states a rate cost of its own, and
the arithmetic above is over the shelf table rather than over a refresh, since
no refresh exists. That these two are the cheapest route to a rating is not
established either: the detail methods named above were not read, and a list
address that returned a certification is not something these two references
would mention.

### An adult flag comes back on four of the six

Both trending references and both movie list references document an `adult`
boolean on a result. Neither television list reference documents one.

What that is worth to #93. `docs/limits.md` already carries that no reference
for these addresses documents a parameter leaving adult titles out, so the
exclusion cannot be made at the request. This is what is left after that. An
exclusion made on the answer is available for four of the six and has nothing to
read on the other two, so a television shelf built on `tv/popular` or
`tv/top_rated` has no adult signal of its own at all.

`movie/popular` and `movie/top_rated` each say the endpoint is a discover call
underneath and point at discover for its filters. Read at
<https://developer.themoviedb.org/reference/movie-top-rated-list>:

    This call is really just a discover call behind the scenes. If you would
    like to tweak any of the default filters head over and read about discover.

Whether that makes `include_adult` or a certification parameter available on
those two addresses is not stated by either page. Nothing here infers it, and
the parameter lists in the table are what those pages document.

### Every address returns a popularity and a vote

All six document `popularity`, `vote_average` and `vote_count` on a result.

What that is worth to #91. That issue's first condition wants the order of a
shelf decided by this plugin from fields on the record, and its record said a
shelf whose premise is a ranking has nothing on the record to derive one from,
so the source's own sequence was the only thing carrying it. That was a true
statement about the record in this tree and not about the source. Three numbers
come back on every one of the six, and this section said the adapter mapped none
of them under a command exiting 1. Two of the three are mapped now:

    git grep -inE 'popularity|vote_average|vote_count' -- Jellyfin.Plugin.Template/ ; echo "exit=$?"
    Jellyfin.Plugin.Template/Catalogue/DiscoverTitleOrder.cs:55:    /// record. TMDB documents one, <c>popularity</c>, on every address this
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:635:            VoteAverage = Score(entry, "vote_average"),
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:636:            VoteCount = Count(entry, "vote_count")
    exit=0

The first of the three matches is a remark rather than a mapping, and it says
why the third number is not carried: `popularity` is the source's own composite,
it moves daily, and a shelf sorted on it rearranges for reasons neither a user
nor this plugin can state, which is what #91 exists against. That decision was
taken on #91 and the argument is at `DiscoverTitleOrder`. What this section
settles is unchanged: the input exists, and a ranked shelf does not have to
depend on arrival sequence for want of one.

## What was not read

This section said that no endpoint but discover had been read and that no
response schema had. Six more have been read since, and they are the six this
plugin asks. What is left unread is smaller than it was, and it is written out
rather than described.

The authentication scheme and the image configuration endpoint were not read.
The discover endpoint's own response schema was not read either: what the
section above records of discover is its parameters.

This paragraph also named the append-to-response mechanism and every endpoint
that answers about a single title as unread. Three pages have been read since,
and they are the two certification addresses and append-to-response, under
`### The two addresses a rating comes from`. What is still unread of that family
is the detail methods themselves: `movie/{movie_id}` and `tv/{series_id}` were
not read, so what a detail answer holds beside a certification is not on this
page.

The six references were read for the parameters they document and for the
fields their result schemas document. Nothing else on those six pages was read,
including their error responses and their examples.

Nothing here was verified against a live response. Every statement on this page
is a reading of documentation, and documentation is a claim by the source about
its own behaviour.
