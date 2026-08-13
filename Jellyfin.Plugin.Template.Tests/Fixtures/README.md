# Fixtures

Recorded responses from a metadata source, for the tests that parse them. This
page is the position on what may be committed here and in what form, and it is
here rather than only on
[#48](https://github.com/Flowfin/jellyfin-plugin-discover/issues/48) because the
next person adding a fixture will read the directory.

This directory holds no fixture yet. Nothing in this repository parses a source
response, so there is nothing to record:

    git grep -l ': IMetadataSource' -- 'Jellyfin.Plugin.Template/*.cs' ; echo "exit=$?"
    exit=1

The first parser is
[#74](https://github.com/Flowfin/jellyfin-plugin-discover/issues/74). What the
set of fixtures then has to cover is the fifth condition on #48 and is not
restated here.

## Nothing captures a fixture during a test run

A fixture is recorded by a route a maintainer runs deliberately. The suite never
records one, because a suite that can record from a source is a suite that can
reach one, and a test that reaches a source is what
[#46](https://github.com/Flowfin/jellyfin-plugin-discover/issues/46) exists to
refuse.

The headless rule is not what stops it. Its three prohibitions are a display,
elevation and a machine trust store, and a network call is none of them. Where it
touches this at all is the replacement it names for the refused trust-store test,
one injected handler in front of every outbound call, and that handler does not
exist yet:

    git grep -n 'The handler does not exist yet' -- Jellyfin.Plugin.Template.Tests/HEADLESS.md

That route does not exist. #48 owes it, and until it lands a fixture is captured
by hand and this page is what says what may be kept from it.

## What may be committed

The shape, the field names, and the edge cases a parser has to survive. Not the
response.

Synthetic values everywhere a test does not depend on a real one. A parser test
that needs a title needs a title, not the one the source returned, and a fixture
carrying the real one is carrying it for no reason a test can name.

Where a real value is needed, the fixture says which source it came from, the
date it was captured, and under what claim it may be here. The claim is a pointer
to that source's page under `docs/sources/`, not a reading of the terms written
out again beside the fixture, because two readings of one clause disagree the day
either is edited.

The reason for the minimum is that this repository is public, so a captured page
of a source's content committed here is that content leaving the server it was
fetched to. That judgement is a row on the source's own terms page rather than
something this directory decides:

    git grep -n 'Fixtures in this repository' -- docs/sources/tmdb.md

How long a fetched response may be held on a server is a different question,
belongs to the same page and to
[#68](https://github.com/Flowfin/jellyfin-plugin-discover/issues/68), and is not
answered here.

## How a fixture is stored

Base64 in source, decoded by the test that reads it.

The bytes reaching a parser have to be exact, and nothing in this tree keeps them
that way. The repository declares no normalisation of its own:

    git ls-files | grep -c '\.gitattributes$'
    0

So what happens to a fixture's bytes on the way into git is a property of the
clone that commits it rather than of this tree. In a clone whose git
configuration turns the conversion on, the carriage returns are gone between the
working tree and the index, and nothing prints anything about it. Measured in a
scratch repository rather than in this one:

    git init -q . && git config core.autocrlf true
    printf 'a\r\nb\r\n' > fixture.txt
    od -c fixture.txt
    0000000   a  \r  \n   b  \r  \n
    0000006

    git add fixture.txt
    git show :fixture.txt | od -c
    0000000   a  \n   b  \n
    0000004

A response body a parser has to survive is exactly the file where those two bytes
are the content rather than the formatting, and a fixture that lost one still
reads correctly in a diff. Declaring normalisation in this tree would fix what
every clone does from that point on and would do nothing about a byte a clone
dropped before it landed, so the two are not equivalent options. Storing the
bytes in a form no line-ending rule can touch is the half that holds without
depending on anything outside this tree.

## What refuses any of this

Nothing. No check reads this page, no rule in `tools/invariants/rules/` judges
what a fixture may carry or how it is stored, and a fixture committed as a raw
response with the source's content in it would pass every run in this repository.
This is read by a person or it is not read at all.
