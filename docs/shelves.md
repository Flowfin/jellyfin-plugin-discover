# The shelves that ship

What a first install browses, why each shelf is there, and what each one costs
the server it runs on. The set is fixed at 1.0: an operator chooses whether a
shelf is shown, never what a shelf asks for. That is the answer to question 5 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), taken on
2026-08-24, with operator-defined shelves a later and additive step.

This page states the set and the reasoning for it. The record that holds a shelf
as data is
[#85](https://github.com/Flowfin/jellyfin-plugin-discover/issues/85) and it
exists now.

    git grep -c 'class Shelf\|record Shelf' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ; echo "exit=$?"
    origin/master:Jellyfin.Plugin.Template/Refresh/ShelfRefreshResult.cs:1
    origin/master:Jellyfin.Plugin.Template/Shelves/Shelf.cs:1
    exit=0

The second file is not a second shelf. It is what one run of the refresh in
[#87](https://github.com/Flowfin/jellyfin-plugin-discover/issues/87) did to one
shelf, which is state of a run rather than a definition, and #85's record says
in its own words why the two are not one type: everything on it changes without
the shelf's definition changing.

The six below are instances of it rather than rows nothing reads, which is #86's
own fourth condition and the difference between a record that admits a shelf and
a set that is data:

    git grep -l 'class ShippedShelves' -- 'Jellyfin.Plugin.Template/*.cs'
    Jellyfin.Plugin.Template/Shelves/ShippedShelves.cs

So the table under this sentence is no longer prose in every column. Which of its
columns are held against that set and which are not is the last section of this
page, and it is worth reading before the table is taken for more than it is.

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

## Titles this server already has

A shelf leaves out a title this server already holds, because a discover page's
whole premise is titles it does not. The rule is one sentence over both kinds a
shelf can carry:

> A title is owned when the server holds at least one part of it, where a part
> is the film for a movie and an episode for a series.

The series half is the one an operator cannot derive from watching a shelf, and
it is why it is written here. A series the server carries as a row with no
episode under it — added by hand, or left behind when the files went — is not
something anybody in the household can watch, so it does not count as owned and
the shelf may still offer it. One episode is enough to make it owned, and the
shelf stops offering it from then on.

What is compared is the identifiers a source supplied and never the title text.
Two films can share a name, and a comparison by name takes the one this server
does not have off the shelf along with the one it does.

The comparison is made when a shelf is refreshed rather than when somebody opens
it. That costs one library question per title per refresh, paid on a schedule,
instead of the same questions paid again every time a client draws a row. What
that number is on a library large enough to matter has not been measured, and
[#89](https://github.com/Flowfin/jellyfin-plugin-discover/issues/89) is where it
belongs.

A server answers that question now, through an adapter over its own library
beside the one the discover page itself is drawn through. What it asks about a
film is the film; what it asks about a series is the series first, by
identifier, and then how many episodes the server holds under it. Rows the
server carries for something it does not have — the missing episodes an operator
can have shown — are left out of both counts, because a row nobody can play is
not a part.

**Nothing here has been measured on a real library.** The comparison is one
library question per title per refresh by construction, and what that is in
seconds on a library large enough to matter is unmeasured.
[#89](https://github.com/Flowfin/jellyfin-plugin-discover/issues/89) is where
that measurement belongs, and it is also where whether a user who cannot see an
item the server does have should be shown it anyway is still undecided: today
the question is asked of the server rather than of a user, so the answer is the
same for everybody.

## A shelf that came back empty, and a shelf nobody asked

Both are a row with nothing in it, and they are not the same thing. One is a
source that answered and had nothing to offer; the other is a shelf no refresh
has ever reached. An operator told only that a row is empty cannot tell a quiet
source from a plugin that has never run.

What separates them is not the contents, since both are no titles. A shelf whose
source answered with nothing has a catalogue document holding no titles. A shelf
no run has ever reached has no document at all.

**The absence means three things and the empty document means one.** A shelf has
no document when no run has reached it, when its source has never once answered,
and when every record it held aged past the retention and the document was
removed. So the empty document is a state an operator can be told about exactly,
and the absence is one they cannot, and which of the three they are shown is
[#63](https://github.com/Flowfin/jellyfin-plugin-discover/issues/63) with a page
to show it on.

## What this page does not decide

- How many titles a shelf holds, and the bound on what reaches the library
  database. That is #58.
- The order titles are drawn in. That is
  [#91](https://github.com/Flowfin/jellyfin-plugin-discover/issues/91), and it is
  this plugin's rather than the source's.
- What a refresh costs on a library large enough for the owned-title comparison
  above to be worth timing, and whether a title is hidden from a user who could
  not have seen the server's own copy of it. Both are
  [#89](https://github.com/Flowfin/jellyfin-plugin-discover/issues/89).
- How often a refresh runs. That is
  [#87](https://github.com/Flowfin/jellyfin-plugin-discover/issues/87).
- Turning a shelf off. The fourth condition on #86 asks that this be one setting.
  Every shelf above arrives on, carrying the flag a setting would move, and there
  is still no setting and nothing that reads the flag, so a shelf turned off
  today is turned off in nobody's sight.

## What holds this page true

Three of the table's four columns, in both directions, and nothing else on the
page.

`ShippedShelvesTests` reads this file, takes the rows between the header and the
first line that is not one, and compares the first three cells of each against
the set the build ships: what the row is called, the question in the spelling an
adapter actually receives, and which kind of title it holds. A row here that no
build ships fails, and a shelf a build ships with no row here fails, so a name
added in one place and not the other reddens rather than being caught by a reader
or not at all. The order is compared too, because the top level is drawn in the
order the set states.

    git grep -l 'ShippedShelvesTests' -- 'Jellyfin.Plugin.Template.Tests/*.cs'
    Jellyfin.Plugin.Template.Tests/ShippedShelvesTests.cs

The question column is taken from the query a shelf composes rather than from the
name of an enum member, so it is held against the string an adapter keys on. The
three questions are themselves a closed set the record carries, and the suite
already asserts that every member of it is one the shipped adapter answers, so a
question added in one place and not the other reds rather than becoming a shelf
that is empty on every server.

**The fourth column is unheld, and so is every sentence on this page.** What each
shelf is for is a judgement, no reading of the tree makes it, and a row whose
reason is wrong passes exactly like one whose reason is right. The same goes for
the argument about why these three names and not others, for the request
arithmetic, and for the paragraph saying the total is stated and not defended.

The commands on this page are re-run by `tools/documented-commands/run.sh`, so a
quotation that stops printing what is pasted under it is refused. Which blocks
that reaches is printed by the run rather than counted here, and the blocks
reading `origin/master` are compared against the mainline and against the tree
being pushed, so a change moving a line one of them cites is refused on its own
pull request.
