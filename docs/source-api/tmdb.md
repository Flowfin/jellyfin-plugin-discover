# What TMDB's API does

This page is what the source's own API documentation says about the four things
this plugin has to decide against it: how many requests it will take, what
language and region it can be asked in, whether adult titles can be excluded
before they arrive, and whether a content rating ceiling can be asked for.

It is not the terms of use. Those are in [`docs/sources/tmdb.md`](../sources/tmdb.md),
which is where an obligation on this plugin's behaviour lives. Nothing here is
an obligation; it is what the API offers, and the two go stale at different
rates and for different reasons.

Read on 2026-08-08. Every value below carries the page it came from. A value
with no page next to it is not on this page.

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

## What was not read

The endpoint reference above is the discover endpoint. No other endpoint was
read. The authentication scheme, the response schemas, the image configuration
endpoint and the append-to-response mechanism were not read.

Nothing here was verified against a live response. Every statement on this page
is a reading of documentation, and documentation is a claim by the source about
its own behaviour.
