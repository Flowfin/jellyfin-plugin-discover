# Fixtures

Recorded responses from a metadata source, for the tests that parse them. This
page is the position on what may be committed here and in what form, and it is
here rather than only on
[#48](https://github.com/Flowfin/jellyfin-plugin-discover/issues/48) because the
next person adding a fixture will read the directory.

There is a parser now, and the set has its first members:

    git grep -l ': IMetadataSource' -- 'Jellyfin.Plugin.Template/*.cs'
    Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs

They are in [`TmdbFixtures.cs`](TmdbFixtures.cs) beside this page rather than in
files of their own, which is what "base64 in source" below means taken
literally: a constant a test reads, with the reason each one exists written at
it. Nine of them, covering the six shapes the fifth condition on
[#48](https://github.com/Flowfin/jellyfin-plugin-discover/issues/48) asks for
and three the adapter's own mapping needs. What each shape is for is at the
constant and is not restated here.

**None of them was captured.** Every byte in that file was written by hand, so
no row of a source's terms bears on any of it, no provenance line is owed on any
of it, and the question of what claim would let a real value sit in a public
repository does not arise for the set as it stands. The field names are the
source's and the values are not, which is the whole of what the section on what
may be committed asks for. The next person adding one from a real response is
the reader that section is written for.

## Nothing captures a fixture during a test run

A fixture is recorded by a route a maintainer runs deliberately. The suite never
records one, because a suite that can record from a source is a suite that can
reach one, and a test that reaches a source is what
[#46](https://github.com/Flowfin/jellyfin-plugin-discover/issues/46) exists to
refuse.

The headless rule is not what stops it. Its three prohibitions are a display,
elevation and a machine trust store, and a network call is none of them. Where it
touches this at all is the replacement it names for the refused trust-store test,
one injected handler in front of every outbound call, and that page says how much
of it is built rather than this one saying it again:

    git grep -c 'Part of it is built' -- Jellyfin.Plugin.Template.Tests/HEADLESS.md
    Jellyfin.Plugin.Template.Tests/HEADLESS.md:1

This paragraph said the handler does not exist yet and handed the reader a search
for that sentence, on a page that stopped carrying it when the first half of the
handler landed. The row there names which half is built and which is not, so a
pointer is what this page owes and a second account of it is what it does not.

The route is [`capture.sh`](capture.sh) beside this page. Nothing in the test
project names it and nothing in the test project starts a process, so `dotnet
test` cannot reach it:

    git grep -nE 'ProcessStartInfo|Process\.Start\(|new Process\(|capture\.sh' -- 'Jellyfin.Plugin.Template.Tests/*.cs' 'Jellyfin.Plugin.Template.Tests/*.csproj' ; echo "exit=$?"
    exit=1

The pattern asks for a call rather than for the bare name, and that is the repair
of a defect rather than a preference. A guard in the suite refuses a process
launch and spells the launch in the remark saying what it refuses, so the search
matched the refusal and exited 0 while the line under it read exit=1.

Half of what this paragraph used to deny has a check behind it now, and the other
half does not. `SuiteAssemblyReferencesTests` reads the assemblies the built test
assembly references and refuses any name its allow-list does not carry:

    git grep -n 'public static void TheSuiteReferencesNothingOutsideTheAllowedSet' -- Jellyfin.Plugin.Template.Tests/SuiteAssemblyReferencesTests.cs
    Jellyfin.Plugin.Template.Tests/SuiteAssemblyReferencesTests.cs:44:    public static void TheSuiteReferencesNothingOutsideTheAllowedSet()

A test launching this script through `Process` puts System.Diagnostics.Process
into that assembly, and the list does not name it:

    grep -vE '^[[:space:]]*#|^[[:space:]]*$' Jellyfin.Plugin.Template.Tests/allowed-test-assembly-references.txt | grep -iE 'diagnostics|process' ; echo "exit=$?"
    exit=1

What is not held is the rest of what this paragraph used to say. A launch the
compiler writes no reference for, a runtime resolved by name, and a test that
merely names this script all leave every route in this tree green.

## Capturing one

    Jellyfin.Plugin.Template.Tests/Fixtures/capture.sh <name> <url> <directory>

Three arguments and no fourth, because the key is not one of them. A credential
on a command line is in the shell history and in the process list of whoever ran
it, so the script reads `DISCOVER_SOURCE_KEY` from the environment and sends it
as a bearer credential. A URL that already carries one as a query parameter is
refused rather than repaired, since by the time the script could repair it the
copy in the history is already written. That door is [#80](https://github.com/Flowfin/jellyfin-plugin-discover/issues/80)'s.

It writes two files under the directory it is given, `<name>.b64` and
`<name>.provenance`. The first is the response body and nothing else, so
`base64 -d` reads it back with no container format for a future test to know
about. The second carries the URL, the status, the content type, the byte count,
whether a credential was sent and the date, and it asks the two questions this
page's next section answers.

**It will not write into a checkout of this repository.** That is the point of
it rather than a precaution. Capturing and committing are two acts and the
second is a judgement, so a route that could put the raw response into
`Fixtures/` would make the minimisation below something a person skips by
accident rather than something they have to do:

    Jellyfin.Plugin.Template.Tests/Fixtures/capture.sh trending 'https://api.themoviedb.org/3/trending/movie/day' Jellyfin.Plugin.Template.Tests/Fixtures
    ::error::'Jellyfin.Plugin.Template.Tests/Fixtures' is inside <checkout>. A capture is not a fixture: ...
    exit=1

The message names the checkout it resolved, and that path is elided above
because it is one machine's rather than anything a reader can check. Run the
command to see your own.

A response the source refused is recorded rather than treated as a failed run,
because an error body is one of the shapes the fifth condition on #48 asks a
parser to survive. The status is written into the provenance and printed, so a
capture of an error cannot later be read as a capture of an answer:

    Jellyfin.Plugin.Template.Tests/Fixtures/capture.sh refused-without-a-key 'https://api.themoviedb.org/3/movie/550' /tmp/cap48
    ok    103 bytes, HTTP 401, written as base64 to /tmp/cap48/refused-without-a-key.b64
    ok    provenance stub at /tmp/cap48/refused-without-a-key.provenance
    note  HTTP 401 is not an answer. Recorded on purpose, ...

That run is the whole of what has been observed against a source: one
unauthenticated request to a host `docs/sources/tmdb.md` already declares,
answered with an authentication error carrying no content. No key has been sent
through this script and no answer from a source has been recorded with it.

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

Nothing, and that answer is unchanged. No rule in `tools/invariants/rules/`
judges what a fixture may carry or how it is stored, and a fixture committed as a
raw response with the source's content in it would pass every run in this
repository. Whether a fixture may be here at all, what it carries and how it is
stored is read by a person or it is not read at all.

The reason this paragraph gave for that answer is what has stopped being true. It
said no check reads this page. One does. `documented-commands` re-runs every
command a tracked page pastes and compares the answer against the output pasted
under it, on every push and every pull request, and it prints what it refused to
judge beside what it judged:

    tools/documented-commands/run.sh | grep 'Jellyfin.Plugin.Template.Tests/Fixtures/README.md'
    ok    Jellyfin.Plugin.Template.Tests/Fixtures/README.md:11: still prints what is pasted under it.
    ok    Jellyfin.Plugin.Template.Tests/Fixtures/README.md:44: still prints what is pasted under it.
    ok    Jellyfin.Plugin.Template.Tests/Fixtures/README.md:56: still prints what is pasted under it.
    ok    Jellyfin.Plugin.Template.Tests/Fixtures/README.md:68: still prints what is pasted under it.
    skip  Jellyfin.Plugin.Template.Tests/Fixtures/README.md:148: no output is pasted, so the block is a command handed to the reader.
    ok    Jellyfin.Plugin.Template.Tests/Fixtures/README.md:162: still prints what is pasted under it.
    skip  Jellyfin.Plugin.Template.Tests/Fixtures/README.md:171: the block transcribes more than one command.
    skip  Jellyfin.Plugin.Template.Tests/Fixtures/README.md:177: the block transcribes more than one command.

That is a check on the page's searches and not on this directory's contents, so
it moves the answer above by nothing. What it does hold is the instrument two of
this page's own claims were found wrong with: both were commands that had stopped
printing what was pasted under them, and both were caught by somebody running
them rather than by any route. That route exists now for the five blocks it
judges, and for the three it names as refused it does not.

The capture route does not change that sentence and must not be read as
softening it. What it refuses, it refuses to whoever runs it, and a fixture
added by hand meets none of those refusals: it was never captured through the
script, so nothing asked where it came from, whether a credential was in the URL
or whether the response is minimised. The route makes the careless path harder
to take by accident. It is not a gate, and no gate stands behind this directory.
