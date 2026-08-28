# The shelves that ship

What a first install browses, why each shelf is there, and what each one costs
the server it runs on. The set is fixed at 1.0: an operator chooses whether a
shelf is shown, never what a shelf asks for. That is the answer to question 5 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), taken on
2026-08-24, with operator-defined shelves a later and additive step.

This page states the set and the reasoning for it. It is not a description of a
mechanism: nothing in the tree reads it. The record that holds a shelf as data is
[#85](https://github.com/Flowfin/jellyfin-plugin-discover/issues/85) and it
exists now.

    git grep -c 'class Shelf\|record Shelf' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ; echo "exit=$?"
    origin/master:Jellyfin.Plugin.Template/Shelves/Shelf.cs:1
    exit=0

What has not happened is the six below becoming instances of it. That is #86's
own fourth condition and it is the difference between a record that admits a
shelf and a set that is data, so the table under this sentence is still prose.

## The set

Three questions, each asked for both kinds of title, which is six shelves.

| Shelf            | Question    | Kind   | What it is for                                                                                         |
| ---------------- | ----------- | ------ | ------------------------------------------------------------------------------------------------------ |
| Trending films   | `trending`  | Movie  | What is being watched elsewhere this week. The shelf that changes most between refreshes.              |
| Trending series  | `trending`  | Series | The same for series, which move on a different rhythm from films and would be buried in a mixed shelf. |
| Popular films    | `popular`   | Movie  | The steady baseline. It is what a first-run server has to show when nothing is trending yet.           |
| Popular series   | `popular`   | Series | The series baseline, for the same reason.                                                              |
| Top-rated films  | `top-rated` | Movie  | Older titles a server is likely to be missing, which trending and popular never surface.               |
| Top-rated series | `top-rated` | Series | The series equivalent, and the only shelf here that reaches beyond the last few years.                 |

A shelf is one name and one kind rather than one name covering both, because the
kind is carried on the question rather than parsed back out of a name:

    git show origin/master:Jellyfin.Plugin.Template/Sources/SourceQuery.cs | grep -A5 'public readonly record struct SourceQuery('
    public readonly record struct SourceQuery(
        string Name,
        DiscoverTitleKind Kind,
        int? StartIndex,
        int? Limit)
    {

and there are two kinds and no third:

    git show origin/master:Jellyfin.Plugin.Template/Catalogue/DiscoverTitleKind.cs | grep -E '^\s+(Movie|Series) = [0-9]'
        Movie = 1,
        Series = 2

## Why these three names and not others

The three are the questions the one implemented source answers directly:

    git show origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs | grep -E '^\s+"(trending|popular|top-rated)" =>'
                "trending" => series ? "trending/tv/week" : "trending/movie/week",
                "popular" => series ? "tv/popular" : "movie/popular",
                "top-rated" => series ? "tv/top_rated" : "movie/top_rated",

That is the constraint a set chosen on taste would meet late rather than early. A
name is a vocabulary every adapter is answerable to: an adapter handed a name it
has no question for answers `NotConfigured` rather than guessing, so a fourth
name is not a row in this table. It is a question every present and future source
has to either answer or decline, and the cost of it grows with the number of
sources rather than with the number of users.

So the set costs no adapter work today, and that is a property of these three
names rather than of the number six. A name outside them would be a shelf that is
empty on every server until somebody writes the question for it, and an empty
shelf is indistinguishable at the surface from a shelf whose key was rejected,
which is the state
[#92](https://github.com/Flowfin/jellyfin-plugin-discover/issues/92) has to read
thinnest.

## What each shelf costs

The source pages in twenties:

    git show origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs | grep 'private const int PageSize'
        private const int PageSize = 20;

So one shelf of at most twenty titles is one request per refresh, and a shelf
wanting more is one request per twenty. The six shelves at twenty titles each are
six requests per refresh; at forty each they are twelve. That is the figure to
weigh against a source's rate budget, which is
[#78](https://github.com/Flowfin/jellyfin-plugin-discover/issues/78), and it is
per refresh rather than per user: the catalogue is fetched once for the server.

The other two costs are per title rather than per shelf, and neither is settled
here. How many titles a shelf may hold at all is
[#58](https://github.com/Flowfin/jellyfin-plugin-discover/issues/58), which bounds
what this plugin writes into the library database. What a title costs on disk and
in that database is
[#71](https://github.com/Flowfin/jellyfin-plugin-discover/issues/71), which has
not been measured.

**The total is therefore stated and not defended.** Six shelves times whatever
per-shelf bound #58 lands is the default item count, and I have no measurement to
weigh it against. That is the second condition on
[#86](https://github.com/Flowfin/jellyfin-plugin-discover/issues/86) and it stays
open on this page rather than being answered by it. A reader who takes six as a
small number is agreeing with a count rather than with a measurement.

## Every one of them works on a first run

A shelf here cannot depend on what anybody has watched, and that is by
construction rather than by choice. A question is a name, a kind, a start index
and a limit, quoted above: there is no user on it and no history. So each of the
six asks the same thing on a server whose key was entered a minute ago, with no
library and nobody signed in, as it does on a server that has been running for a
year.

That stops being free the day question 7 on #2 is answered in favour of
personalisation, and
[#90](https://github.com/Flowfin/jellyfin-plugin-discover/issues/90) is where that
lands. The set above is meant to be readable as the set that survives either
answer.

## What this page does not decide

- How many titles a shelf holds, and the bound on what reaches the library
  database. That is #58.
- The order titles are drawn in. That is
  [#91](https://github.com/Flowfin/jellyfin-plugin-discover/issues/91), and it is
  this plugin's rather than the source's.
- Whether a title the server already has is filtered out. Answered on #2 on
  2026-08-24 as filtered, and
  [#89](https://github.com/Flowfin/jellyfin-plugin-discover/issues/89) is where it
  is built.
- How often a refresh runs. That is
  [#87](https://github.com/Flowfin/jellyfin-plugin-discover/issues/87).
- Turning a shelf off. The fourth condition on #86 asks that this be one setting.
  The record now carries the flag a setting would move, and there is no setting
  and nothing that reads the flag, so a shelf turned off today is turned off in
  nobody's sight.

## What holds this page true

Nothing. No check reads it, no test asserts the set against anything, and the
table above is prose beside code rather than derived from it. The set moves into
the record on #86 and this page becomes the reasoning beside the data; until then
a name added to the adapter and not to this table, or the reverse, is caught by a
reader or not at all.

One column of the table is held now and the rest is not, and the difference is
worth knowing before it is read as more than it is. The three questions are a
closed set the record carries, and the suite asserts that every member of it is
one the shipped adapter answers, so a question added in one place and not the
other reds rather than becoming a shelf that is empty on every server. Which six
rows ship, what each is called and what each is for is unheld, because none of it
is anywhere but here.

The commands on this page are re-run on the mainline by
`tools/documented-commands/run.sh`, so a quotation that stops printing what is
pasted under it is refused. That reaches the five blocks above and none of the
sentences between them.
