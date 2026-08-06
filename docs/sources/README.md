# Metadata source terms

Every metadata source this plugin talks to has a page in this directory. The
page turns that source's terms of use into obligations on this plugin's
behaviour, one row per clause, and says for each one where the behaviour lives
and how a reader checks that it is met.

A source cannot be added without its page. `source-terms` refuses an adapter
whose page is missing, refuses a hostname written in the tracked C# that no page
declares, and refuses a page that does not say when its terms were read. What
that check cannot do is in
[the workflow](../../.github/workflows/source-terms.yml), next to what it does.

## What a page carries

Three lines are read by the check and have to appear at the start of a line.

    Source: <token>
    Host: <hostname>
    Terms read: <YYYY-MM-DD>

`Source` is the token in the page's file name, and it is the same token an
adapter carries: a file `FooSourceAdapter.cs` anywhere in the tree needs
`docs/sources/foo.md`. `Host` is repeated once per hostname the source is
reached at, and the check refuses a hostname written in the tracked C# that no
page declares. `Terms read` is the day somebody opened the terms and wrote the
page against what they said on that day.

The rest of the page is prose and a table, and no route reads it.

## Why the date matters

Terms change, and an undated reading is a claim about today made from an
unknown day. A page whose date is old is not wrong; it is a page that says how
old it is, which is the only thing a reader can act on.

## Hosts that are not sources

A hostname can appear in the C# without being a source: a namespace URI in a
serialised document is the case that already exists. Those are declared
below, one per line and at the start of the line, with the reason beside them.
The check reads these lines, so a hostname is either a source with a page or an
exception written down where a reader sees it.

Not a source: www.w3.org - XML namespace URIs in a serialised configuration document, not an address anything is fetched from.

## This is not legal advice

Nothing in this directory is legal advice, and nobody here is a lawyer. These
pages are an engineering reading of a document, written so the code can be
checked against it. Where a page says a clause means a particular constraint,
that is this project's reading and not a legal opinion. The terms themselves
are linked from every page and they are the authority.
