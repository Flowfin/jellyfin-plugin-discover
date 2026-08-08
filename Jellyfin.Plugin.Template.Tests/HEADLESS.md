# The headless rule

Every test in this repository runs without a display, without elevation, and
without touching a machine trust store.

This is a birth requirement rather than a policy adopted later. A test that
breaks one of the three passes on the machine that wrote it, so nobody removes
it; it becomes the reason the gate needs a special runner, and by then the code
it covers is only covered by that test.

This page is the one home for the rule. Every test issue in the plan points here
instead of restating a prohibition, because a rule written down twice becomes two
rules the day one copy is edited.

## The three prohibitions

### No display

A test that needs a display needs either a runner that has one or a browser
runtime fetched at test time, and both are things to keep working that have
nothing to do with the plugin. It is also the test that goes flaky first, and a
flaky test gets quarantined rather than fixed, which leaves its subject covered
on paper and covered by nothing in fact.

It is a prohibition and not a preference because the cost arrives later than the
convenience. The suite that needs a display is cheap to write on a laptop and
expensive on every machine afterwards.

What refuses it in this tree is narrow, and its narrowness is the point:
`no-browser-automation` reads the project files rather than the tests, because
the package arrives before the test does.

    git grep -n '^Subject:' -- tools/invariants/rules/no-browser-automation.rule
    tools/invariants/rules/no-browser-automation.rule:3:Subject: *.csproj *.props *.sln

Nothing refuses a test that reaches a display by some other means. See
[What none of this covers](#what-none-of-this-covers).

### No elevation

The machine running the suite belongs to somebody. An administrative action
outlives the test run, changes the machine for everything else on it, and on a
desktop it interrupts a person with a consent prompt in the middle of a run they
did not know would ask for one.

It is a prohibition and not a preference because an elevated step cannot be
undone by the test that took it. A test can clean up a temporary directory. It
cannot un-install a service, and it cannot give back the attention it took.

The concrete refusal in the plan is installing the built plugin into a
machine-wide server and restarting the service, which is
[#44](https://github.com/Flowfin/jellyfin-plugin-discover/issues/44).

### No machine trust store

The plugin talks to a metadata source over HTTPS, so the shortest route to
testing that path against a local endpoint is a certificate the machine trusts,
and the shortest route to that is installing one into the machine's own store.

Refused in both spellings, installing a certificate and disabling verification
globally to avoid installing one. The second is worse than the first: it removes
the property the test was supposed to be about and leaves a test that still
passes.

It is a prohibition and not a preference for the same reason as elevation. The
change outlives the run and it weakens the machine for every other program on
it, and the machine is not the suite's to weaken.

`no-machine-trust-store` holds the spellings that can be written as a line
pattern, and its subject reaches the workflow files as well as the source,
because a step that trusts a certificate before the suite runs leaves no trace in
any test.

    git grep -n '^Subject:' -- tools/invariants/rules/no-machine-trust-store.rule
    tools/invariants/rules/no-machine-trust-store.rule:3:Subject: *.cs *.csproj *.props *.sln *.sh *.ps1 *.yml *.yaml

## The refused tests, and what covers the same risk

A refusal is not a gap. Each row below names a test that is real, a means that is
wrong, and the replacement that carries the same risk. A row with no replacement
would be a hole with a rule in front of it.

| Refused                                                                                             | Why the means is wrong                                                                                                      | What carries the same risk instead                                                                                                                                                                                                  | Issue                                                                        |
| --------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| Driving a browser at the configuration page on a running server                                     | Needs a display or a fetched browser runtime and a server to point at, and it is the first test to be quarantined.          | The page is read out of the built assembly's embedded resources and asserted against the bytes that ship: `ConfigurationPageReader.cs`, `ConfigurationPageTests.cs`, `ConfigurationPageReaderTests.cs`, `PluginIdentifierTests.cs`. | [#43](https://github.com/Flowfin/jellyfin-plugin-discover/issues/43), closed |
| Copying the plugin into a machine-wide server's plugin directory and restarting the service         | Administrative on any real machine, leaves state behind, and makes the suite depend on a server somebody installed by hand. | The package layout is a property of the zip and is asserted with no server. That a server loads it is a container the check starts itself, in `.github/workflows/plugin-loads.yml`.                                                 | [#44](https://github.com/Flowfin/jellyfin-plugin-discover/issues/44)         |
| Installing a certificate into the machine trust store, or disabling certificate validation globally | Outlives the run and weakens the machine, or removes the property under test while leaving the test green.                  | One injected handler in front of every outbound call, supplied by the test, so no test needs a real endpoint. The handler does not exist yet; nothing in this tree makes an outbound call.                                          | [#45](https://github.com/Flowfin/jellyfin-plugin-discover/issues/45)         |

The third replacement is named and not yet built. That is stated rather than
elided, because a table where every row reads as done is the shape that makes a
plan look like a suite.

## What a container counts as

Two things in the plan start a server in a container: the load check that is
already here, and the end-to-end run in
[#38](https://github.com/Flowfin/jellyfin-plugin-discover/issues/38).

Whether that is elevation is a property of the machine and not of the test, so it
is stated rather than assumed.

On the gate's Ubuntu runner it is not. The runner user already reaches the daemon
and the server runs as that user rather than as root:

    git grep -n 'docker run -d\|--user' -- .github/workflows/plugin-loads.yml
    .github/workflows/plugin-loads.yml:195:            docker run -d --rm --name "$1" \
    .github/workflows/plugin-loads.yml:196:              --user "$(id -u):$(id -g)" \

On a developer machine it may be. Reaching a container daemon there can need
group membership, a privileged daemon, or a prompt, and none of that is
something a test can decide for the person running it.

The consequence is that a container run is not part of the suite. `dotnet test`
today starts no container at all:

    git grep -lEi 'Testcontainers|Docker\.DotNet|docker run|Process\.Start' -- 'Jellyfin.Plugin.Template.Tests/*' ; echo "exit=$?"
    exit=1

`git grep` exits 1 when it matches nothing, so that is the whole answer rather
than an empty output somebody has to trust. The container work lives in a
workflow instead, and a default `dotnet test` run on a machine with no container
runtime is green rather than skipped with warnings. Keeping that true as the
suite grows is #44's third condition.

## What none of this covers

- Elevation itself is refused by nothing in this tree. No rule in
  `tools/invariants/rules/` has it as a subject, and no reading of the tracked
  text could: a step that shells out to something the trust-store pattern does not
  name, or a test that asks for a privilege at run time, is invisible.
  The three prohibitions are held by review and by this page. Only the
  trust-store spellings and the browser packages are held by a machine.
- Whether the configuration page looks right and is usable is not a property any
  test here holds. That is
  [#115](https://github.com/Flowfin/jellyfin-plugin-discover/issues/115), a
  manual matrix rather than a test.
- Whether a real metadata endpoint presents a certificate this runtime accepts is
  outside the injected handler by construction. The handler exists so that no
  test needs a real endpoint, which means no test learns anything about one.
- Whether the plugin loads on a server somebody installed by hand is not checked
  by the container run either. The container is a server the check started, with
  its own data directory, and a hand-installed server differs from it in every
  way a person has configured.
- The invariant runner reads tracked text one line at a time. A violation spread
  over two lines, a name reached indirectly, or a call assembled at run time are
  all invisible to it, and that bound belongs to every rule quoted above rather
  than to any one of them.

## Where the rest of the suite's limits are written

This page is about the tests that were refused. What a green suite proves and
what it does not is the neighbouring question, and it is
[#50](https://github.com/Flowfin/jellyfin-plugin-discover/issues/50).
