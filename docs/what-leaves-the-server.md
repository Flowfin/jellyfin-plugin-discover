# What leaves the server, and to whom

An operator who runs a server so that nothing leaves their network deserves to
know what this plugin sends out, where it goes, and what a third party can work
out from it. This page is that disclosure. It is written from this tree rather
than from a running server, and every claim on it carries the command behind it.

Read it against the commit it ships with. Where a sentence here and the tree
disagree, the tree is right and the sentence is a defect.

## Today, an installed plugin sends nothing anywhere

This is the first thing to say and the easiest to read too widely. The plugin
that installs today can speak to the source, and nothing in it does.

The type that speaks is `TmdbSourceAdapter`. No other file in the plugin names
it:

    git grep -n 'TmdbSourceAdapter' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ':!Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs' ; echo "exit=$?"
    exit=1

so nothing constructs one, and the container the server builds from this plugin
holds seven registrations, none of which is a source:

    git show origin/master:Jellyfin.Plugin.Template/PluginServiceRegistrator.cs | grep -n 'AddSingleton'
    45:        serviceCollection.AddSingleton<IClock, SystemClock>();
    50:        serviceCollection.AddSingleton<IRandomSource, SystemRandomSource>();
    57:        serviceCollection.AddSingleton<IDiscoverSurface, DiscoverSurface>();
    74:        serviceCollection.AddSingleton<IChannel, DiscoverSurfaceAdapter>();
    96:        serviceCollection.AddSingleton(provider => new WantHandover(
    117:        serviceCollection.AddSingleton<IServerLibrary>(provider =>
    135:        serviceCollection.AddSingleton<IScheduledTask, DiscoverRefreshTask>();

The seventh arrived with the refresh in #87 and is the one on this list nearest to
being an exception, so it is worth reading closely rather than dismissing. It is
the scheduled task the server runs, and a run of it is the moment this plugin
would ask a source. What it asks is whatever the same container holds under the
source interface, and nothing puts anything there, so what it holds is the empty
set and a run reports every shelf as its source not being set up. It opens no
connection of its own: the type that could is the adapter above, and the command
above this one is what says nothing constructs it.

That is a narrower guarantee than the one the paragraph above it makes, and the
difference is worth stating. Before the refresh there was no code path from a
schedule to a source at all; now there is one and it ends in an empty set. What
holds it empty is a registration nobody has written rather than the absence of a
caller.

The sixth arrived with the owned-title filter in #89 and it is the only one on
this list that asks the server a question of its own. It is handed a title's
identifiers and whether the title is a film or a series, and it answers with how
many parts of that title this server holds. What crosses it is fixed by the
signature rather than by a sentence beside the signature, so no title text
crosses it at all:

    git grep -n 'int PartsHeld' origin/master -- Jellyfin.Plugin.Template/Server/IServerLibrary.cs
    origin/master:Jellyfin.Plugin.Template/Server/IServerLibrary.cs:55:    int PartsHeld(DiscoverTitleIdentity identity, DiscoverTitleKind kind);

The question and the answer both stay inside the server. The answer decides
whether a title is put on a shelf, it is not a field on anything the catalogue
stores, and nothing carries it outward. What it asks about is what the server
holds rather than what a given user may see, so it is one answer for everybody
on the server; whether it should be is #89's fifth condition and is open.

The fifth arrived with the seam in #95 and it is the one on this list a reader
should be able to dismiss for a stated reason rather than by its name. It offers
a want to whatever implements `IWantReceiver` in the same server's container,
which is another plugin in the same process, so it opens no connection and has
no address. Nothing in this plugin implements that interface, and on a server
with no requests plugin the container answers it with nothing:

    git grep -rn ': IWantReceiver' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ; echo "exit=$?"
    exit=1

What crosses it, on a server that does install a sibling, is fixed in
[0004](decisions/0004-what-crosses-the-seam-to-a-requests-plugin.md) and is a
handover between two plugins rather than traffic. Where that data goes after a
sibling has it is that sibling's disclosure and not this one's.

Browsing does not reach one either. Every level of the surface answers with no
entries, the top level included, and the two answers it gives differ in the
total rather than in what they hold:

    git grep -n 'IsRoot ? SurfaceListing.EmptyLevel' origin/master -- Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs:166:            asked.Parent.IsRoot ? SurfaceListing.EmptyLevel : SurfaceListing.NoSuchLevel);

So an operator installing this build gets no outbound traffic at all, from an
install, from a browse, or from a schedule. The rest of this page describes what
the adapter sends when something finally asks it to, because that shape is fixed
in the tree now and an operator deciding whether to install is deciding about it
rather than about the silence above.

## Two hosts, and no third

Both are declared on the source's own terms page and both are literals in the
adapter:

    git grep -n 'https://' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:68:    private static readonly Uri _baseAddress = new("https://api.themoviedb.org/3/", UriKind.Absolute);
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:70:    private static readonly Uri _artworkBase = new("https://image.tmdb.org/t/p/w500/", UriKind.Absolute);

The first is where the server asks its questions. The second is where artwork
sits, and the server is not what fetches it, which is the section below.

Nothing else in the plugin can open a connection. The one outbound client in the
project is inside the adapter and there is no second one:

    git grep -nE 'HttpClient|IHttpClientFactory|HttpRequestMessage|Socket|Dns\.' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ':!Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs' ; echo "exit=$?"
    exit=1

That absence is held by a rule rather than by anybody remembering it:

    git grep -n '^Id:\|^Subject:\|^Except:' origin/master -- tools/invariants/rules/no-network-outside-source-adapter.rule
    origin/master:tools/invariants/rules/no-network-outside-source-adapter.rule:1:Id: no-network-outside-source-adapter
    origin/master:tools/invariants/rules/no-network-outside-source-adapter.rule:3:Subject: *.cs
    origin/master:tools/invariants/rules/no-network-outside-source-adapter.rule:4:Except: :!Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs

## What one request to the source carries

A request is a `GET`, and the whole of what it says is a path, up to three query
parameters and three headers.

The path is one of six literals chosen by a switch, and no value a caller
supplied reaches it as text:

    git grep -n '"trending" =>\|"popular" =>\|"top-rated" =>' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:617:            "trending" => series ? "trending/tv/week" : "trending/movie/week",
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:618:            "popular" => series ? "tv/popular" : "movie/popular",
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:619:            "top-rated" => series ? "tv/top_rated" : "movie/top_rated",

The query is a page number, and a language and a region where this plugin was
told them:

    git grep -n 'parameters +=\|var parameters =' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:628:        var parameters = FormattableString.Invariant($"page={page}");
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:632:            parameters += FormattableString.Invariant($"&language={language}");
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:637:            parameters += FormattableString.Invariant($"&region={region}");

Neither of the two is composed here and neither is free text. `SourceLocale`
admits a language only as two lower-case letters, optionally a hyphen and two
upper-case letters, and a region only as two upper-case letters, so what reaches
a query string is a value every character of which this plugin has vouched for.
Where the pair comes from is
[#81](https://github.com/Flowfin/jellyfin-plugin-discover/issues/81) and nothing
on a running server supplies one today, so every request this tree would make
carries the page number alone.

What the two disclose is what an operator chose, not who is asking. A language
and a region are a property of the server's settings and are the same on every
request it makes, so they say nothing about which person is looking at a shelf,
and nothing about a person could reach them: the section below is what holds
that.

The headers are three:

    git grep -n 'TryAddWithoutValidation' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:713:        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:714:        request.Headers.TryAddWithoutValidation("Accept", "application/json");
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:715:        request.Headers.TryAddWithoutValidation("User-Agent", Identity());

The first is a credential, and whose it is and where it is stored is
[#77](https://github.com/Flowfin/jellyfin-plugin-discover/issues/77). The third
names this plugin and its version and nothing about the server or the operator,
which the source's terms require and which is derived rather than typed:

    git grep -n -A 5 'private static string Identity()' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:736:    private static string Identity()
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs-737-    {
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs-738-        var assembly = typeof(TmdbSourceAdapter).Assembly.GetName();
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs-739-
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs-740-        return FormattableString.Invariant($"{assembly.Name}/{assembly.Version}");
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs-741-    }

## What a request does not carry

No user. Nothing about a person can reach a request, because the question the
adapter is handed has four fields and none of them is one:

    git show origin/master:Jellyfin.Plugin.Template/Sources/SourceQuery.cs | sed -n '44,48p'
    public readonly record struct SourceQuery(
        string Name,
        DiscoverTitleKind Kind,
        int? StartIndex,
        int? Limit)

No library. The adapter asks for a shelf of titles and sends nothing about what
the server holds, what anybody watched, or how many accounts exist. It cannot:
the four fields above are its whole vocabulary.

No server identity beyond what a connection discloses on its own. The plugin
sends no server name, no installation identifier and no version of the server.

## What the source can work out anyway

An absence of fields in a request is not an absence of information, and this is
the part of the disclosure a reader is owed rather than reassured about.

The source sees the connection, so it sees the network address the server calls
from and the time of every call. Over a schedule that is a pattern: which shelves
an operator has enabled, in which language and region where this plugin was told
them, how often the server refreshes, and when it is switched off. The credential
ties all of that to whoever registered it.

None of that is avoidable while the plugin is used at all. It is the cost of
asking somebody else a question.

## Artwork, and who actually fetches it

The plugin never fetches an image. What it stores is a location at the source's
image host, turned from the path the source gave:

    git grep -n 'return new Uri(_artworkBase' origin/master -- Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Sources/TmdbSourceAdapter.cs:565:        return new Uri(_artworkBase, path.AsSpan(1).ToString());

and it hands that location to the server as the item's picture:

    git grep -n 'ImageUrl = title.ArtworkLocation' origin/master -- Jellyfin.Plugin.Template/Surface/DiscoverSurfaceAdapter.cs
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurfaceAdapter.cs:297:            ImageUrl = title.ArtworkLocation?.AbsoluteUri

Whether the image is then fetched by the server or by each client, and therefore
whether a user's own device contacts `image.tmdb.org` directly, is not
established here. It is read from this tree rather than watched happening, no
adapter is wired to anything that would produce a location, and
[#62](https://github.com/Flowfin/jellyfin-plugin-discover/issues/62) is where
that observation is owed. An operator reading this page today should assume the
question is open rather than that the answer is the comfortable one.

## What is held about a person

Nothing, and the reason moved with the refresh in #87. The one thing in this
plugin that writes to disk is now reached, by the scheduled task:

    git grep -n 'new CatalogueDocumentStore(' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ; echo "exit=$?"
    origin/master:Jellyfin.Plugin.Template/Refresh/DiscoverRefreshTask.cs:346:                new CatalogueDocumentStore(new CatalogueDirectory(dataFolderPath), _storeLogger),
    exit=0

What that store can be handed is a shelf's titles and nothing else, so what a
run could write is what a metadata source answered about films and series. It
holds no field about a person: `DiscoverTitle` is what a document carries and
the section above is where its shape is read. And a run today writes nothing at
all, because the container holds no source, which is the paragraph under
`## Today, an installed plugin sends nothing anywhere`.

So the sentence this section leads with is unchanged and its support is
narrower: nothing about a person is held because nothing in the one writer's
reach is about a person, rather than because the writer is unreachable.

A user identifier reaches the plugin when somebody browses, and it is answered
with rather than kept. Every site it appears at is a per-user answer being asked
for:

    git grep -n 'UserId\|userId' origin/master -- 'Jellyfin.Plugin.Template/*.cs'
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurface.cs:133:    public bool IsAvailableTo(Guid userId) => true;
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurfaceAdapter.cs:175:    public bool IsEnabledFor(string userId) =>
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurfaceAdapter.cs:176:        Guid.TryParse(userId, out var parsed) && _surface.IsAvailableTo(parsed);
    origin/master:Jellyfin.Plugin.Template/Surface/DiscoverSurfaceAdapter.cs:197:            query.UserId,
    origin/master:Jellyfin.Plugin.Template/Surface/IDiscoverSurface.cs:52:    /// <param name="userId">Who is asking.</param>
    origin/master:Jellyfin.Plugin.Template/Surface/IDiscoverSurface.cs:60:    bool IsAvailableTo(Guid userId);
    origin/master:Jellyfin.Plugin.Template/Surface/SurfaceLevelRequest.cs:19:/// <param name="UserId">
    origin/master:Jellyfin.Plugin.Template/Surface/SurfaceLevelRequest.cs:33:    Guid UserId,

The register this list is kept in is
[#70](https://github.com/Flowfin/jellyfin-plugin-discover/issues/70), and it is
the input to this page rather than something restated here. One thing in the plan
takes it off zero when it lands: recording what a user asked for, which is
[#97](https://github.com/Flowfin/jellyfin-plugin-discover/issues/97).

Personalised shelves were the second and are no longer coming. This page said
they were question 7 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) and that the
question was not answered; it was answered on 2026-08-24, and the answer is
neither at 1.0. Nothing about viewing behaviour leaves the server, and the weaker
variant that would have kept the personalisation on the server is not built
either. Both halves belong here rather than only the first: the outbound half is
what a source would have been told about a named account, and the half that never
left would still have been a record about a person for the register above to
carry. A release that wants either is re-opening that question rather than
extending this page.

Sources that would need a user's own authorisation, which are the largest thing
that could ever leave this server about a named person, are deferred, and the
argument is on its own page rather than summarised here:
[`per-user-sources.md`](per-user-sources.md).

### What is not held yet, and what it will be

Everything above is what is true today. This page states both halves everywhere
else, because a sentence that is true now and stops being true later is the one a
reader keeps, and the section above owes the same second half. THIS SAID WHAT
ARRIVES IS NOT A STORED RECORD BUT A LOG LINE, and it is both. The stored record
is the want list below, which is in the tree and reaches no server yet; the log
line is the handover's, and it is the half that survives after the record is
removed.

The gesture that means a user wants a title is
[#96](https://github.com/Flowfin/jellyfin-plugin-discover/issues/96) and is not
built, so nothing in this plugin reaches the handover the seam registers:

    git grep -n 'OfferAsync' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ':!Jellyfin.Plugin.Template/Seam/*' ; echo "exit=$?"
    exit=1

The list a want would be written to is in the tree and is constructed by nothing:

    git grep -n 'new LocalWantRegister(' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ; echo "exit=$?"
    exit=1

THAT LIST IS NOT ONLY HELD IN MEMORY, AND THIS PARAGRAPH SAID IT WAS AND THAT
THE TYPE SAID SO ABOUT ITSELF. The type says the opposite, in its own words and
in capitals, naming the change:

    git grep -n 'THIS SAID IT DOES NOT SURVIVE A RESTART' origin/master -- Jellyfin.Plugin.Template/Wants/LocalWantRegister.cs
    origin/master:Jellyfin.Plugin.Template/Wants/LocalWantRegister.cs:31:/// THIS SAID IT DOES NOT SURVIVE A RESTART, and it does where it is given a

A register given a store writes its rows through to a file of its own, beside the
catalogue's directory rather than inside it:

    git grep -n 'const string DirectoryName\|const string FileName' origin/master -- Jellyfin.Plugin.Template/Wants/WantListStore.cs
    origin/master:Jellyfin.Plugin.Template/Wants/WantListStore.cs:58:    public const string DirectoryName = "wants";
    origin/master:Jellyfin.Plugin.Template/Wants/WantListStore.cs:63:    public const string FileName = "wants.json";

and the row it writes carries the account that asked, beside the title and the
moment:

    git grep -n 'writer.WriteString(AskingUserField' origin/master -- Jellyfin.Plugin.Template/Wants/WantListDocument.cs
    origin/master:Jellyfin.Plugin.Template/Wants/WantListDocument.cs:141:        writer.WriteString(AskingUserField, want.AskingUser);

So what an operator would have to weigh is a file on their disk naming who asked
for what, and this page is where that belongs. Nothing on the server carries one
today, and the reason is the one the block above gives rather than the sentence
this replaces: no register is constructed at all, with or without a store.

    git grep -n 'new WantListStore(' origin/master -- 'Jellyfin.Plugin.Template/*.cs' ; echo "exit=$?"
    exit=1

HOW LONG SUCH A FILE WOULD BE KEPT IS NOT DECIDED ANYWHERE. Nothing in the want
code expires a row, ages one out, or caps how old the list may be:

    git grep -nE 'Retention|Expire|MaxAge' origin/master -- Jellyfin.Plugin.Template/Wants/ ; echo "exit=$?"
    exit=1

The ninety-day catalogue retention does not reach it: that cap exists because a
source's terms impose one on fetched records, and nothing a user asked for is a
fetched record. What removes the file is the operator's purge and the uninstall,
both of which take this plugin's whole data folder rather than one person's rows:

    git grep -n 'public void RemoveEverything' origin/master -- Jellyfin.Plugin.Template/Storage/PluginDataPurge.cs
    origin/master:Jellyfin.Plugin.Template/Storage/PluginDataPurge.cs:106:    public void RemoveEverything()

An unbounded retention on a list an operator acts on is defensible and is not
defensible while it is unstated, which is why it is stated here. Choosing a bound
is [#70](https://github.com/Flowfin/jellyfin-plugin-discover/issues/70)'s and is
not taken on this page.

THE SENTENCE THIS REPLACES WAS TRUE ON THE DAY IT WAS WRITTEN AND WAS FALSE THE
NEXT DAY. It landed on 2026-08-29 under this page's own issue, and the store
landed on 2026-08-30 under #97, in the commit that flipped the type's remark:

    git log --format='%h %aI %s' --diff-filter=A origin/master -- Jellyfin.Plugin.Template/Wants/WantListStore.cs
    4561ab4 2026-08-30T14:12:08+02:00 Keep the want list where a restart does not take it #97

Nothing in this tree reads a page for a claim about behaviour, so a disclosure
one commit out of date stays on the page until somebody runs its commands, and
that is how this one was found rather than by any route.

The handover beside the list writes five lines into the server's log, and each of
them names the want it is talking about:

    git grep -c 'The want {WantIdentifier}' origin/master -- Jellyfin.Plugin.Template/Seam/WantHandover.cs
    origin/master:Jellyfin.Plugin.Template/Seam/WantHandover.cs:5

THE FIFTH ARRIVED WITH THE PERMISSION IN #98 AND IT IS THE ONE THAT NAMES A
PERSON WITHOUT NEEDING THE PARAGRAPH BELOW. It is the refusal written when this
plugin will not pass a want on, and it carries the asking user as a placeholder
of its own beside the want. So one line in this file states, in the plainest
form on the page, that a named account asked for a title and was refused; the
count above is where that is read from, and a reader who takes the four other
lines as the whole of it is reading a sentence this one replaced.

The placeholder the other four carry is not opaque either. A want identifier is
derived rather than drawn, and the asking user's identifier is one of its three
parts in plain text:

    git grep -n 'source&gt;:&lt;user&gt;' origin/master -- Jellyfin.Plugin.Template/Seam/WantIdentifiers.cs
    origin/master:Jellyfin.Plugin.Template/Seam/WantIdentifiers.cs:32:/// <c>&lt;source&gt;:&lt;user&gt;:&lt;identifier&gt;</c>, in that order. The

So one of those lines states which account asked for which title at which source,
at a moment the logging framework stamps, and none of that is visible in the
message a reviewer reads.

How long such a line lasts is the server's answer rather than this plugin's, and
it is bounded rather than indefinite. The shipped logging configuration writes the
rendered message to a file, rolls it daily, keeps three of them, and rolls again
at a hundred megabytes:

    git grep -n 'rollingInterval\|retainedFileCountLimit\|fileSizeLimitBytes' v10.11.11 v12.0-rc4 -- Jellyfin.Server/Resources/Configuration/logging.json
    v10.11.11:Jellyfin.Server/Resources/Configuration/logging.json:25:                                "rollingInterval": "Day",
    v10.11.11:Jellyfin.Server/Resources/Configuration/logging.json:26:                                "retainedFileCountLimit": 3,
    v10.11.11:Jellyfin.Server/Resources/Configuration/logging.json:28:                                "fileSizeLimitBytes": 100000000,
    v12.0-rc4:Jellyfin.Server/Resources/Configuration/logging.json:25:                                "rollingInterval": "Day",
    v12.0-rc4:Jellyfin.Server/Resources/Configuration/logging.json:26:                                "retainedFileCountLimit": 3,
    v12.0-rc4:Jellyfin.Server/Resources/Configuration/logging.json:28:                                "fileSizeLimitBytes": 100000000,

read from a checkout of the server with both targeted lines fetched, so the three
values are the same at each. Nothing in that configuration redacts a placeholder,
and there is no destructuring policy anywhere in the server's logging setup, so
the identifier is written as it renders. On a default install the line therefore
survives about three days, and less on a busy server.

Two things that answer does not cover, and an operator who wants a shorter one has
to look at both. The operator's own copy is `logging.user.json`, which the server
initialises for them and this plugin neither reads nor sets, so a raised retained
count or a redirected sink is a different answer that no reading of this tree can
see. And anything in front of the server that copies the console stream - a
container runtime, a service manager, a log shipper - keeps it on its own schedule.

Nothing this plugin has reaches any of that. The register's own removal takes a
person's rows out of the list and, where the register was given a store, rewrites
the file without them:

    git grep -n 'public int Forget' origin/master -- Jellyfin.Plugin.Template/Wants/LocalWantRegister.cs
    origin/master:Jellyfin.Plugin.Template/Wants/LocalWantRegister.cs:324:    public int Forget(Guid user)

    git grep -n -A 2 'private void Keep()' origin/master -- Jellyfin.Plugin.Template/Wants/LocalWantRegister.cs
    origin/master:Jellyfin.Plugin.Template/Wants/LocalWantRegister.cs:399:    private void Keep() => _store?.Write(InOrder());
    origin/master:Jellyfin.Plugin.Template/Wants/LocalWantRegister.cs-400-}

THIS PARAGRAPH SAID THE REMOVAL REACHED MEMORY AND NOTHING ELSE, which is the
same staleness as the one repaired above and arrived in the same commit. What it
still does not reach is a log line, which is what the sentence was about and what
stays true: nothing removes a rendered message from a file the server rolls. And
nothing calls the removal at all, which is #70's third condition rather than this
page's.

What an uninstall does and does not take is
[`installing.md`](installing.md) rather than a second answer here.

Which way out is taken is not decided on this page. Logging an opaque per-run
token, logging the receiver and the outcome without the want, and leaving the
lines as they stand with the retention disclosed here are three answers with
different costs to an operator debugging a handover. The choice is recorded on
[#70](https://github.com/Flowfin/jellyfin-plugin-discover/issues/70), which is
the register this page imports rather than writes, and until it is taken this
paragraph is what an operator has instead of an answer.

## The configuration page

An administrator opening the plugin's page sends nothing outside the server, and
a test refuses the change rather than anybody remembering it:

    git grep -n 'ThePageRequestsNothingFromAHostOutsideTheServer' origin/master -- Jellyfin.Plugin.Template.Tests/ConfigurationPageTests.cs
    origin/master:Jellyfin.Plugin.Template.Tests/ConfigurationPageTests.cs:200:    public void ThePageRequestsNothingFromAHostOutsideTheServer()

## What a user can turn off for themselves

Nothing, and the heading is here rather than left out so the answer is readable.
Everything this plugin can be told is one server-wide record:

    git grep -n 'public .* { get' origin/master -- Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs
    origin/master:Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:46:    public int SchemaVersion { get; set; }
    origin/master:Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:58:    public int MaximumTitlesPerShelf { get; set; }
    origin/master:Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:69:    public int MaximumTitlesAcrossAllShelves { get; set; }
    origin/master:Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:96:    public Collection<string> UsersRefusedTheAsk { get; } = new Collection<string>();

THIS SENTENCE SAID EVERY FIELD ON THAT RECORD WAS SERVER-WIDE, AND THE SEARCH IT
RESTED ON WOULD HAVE GONE ON AGREEING WITH IT. The pattern was
`public .* { get; set; }`, which asks about a spelling, and the field that
arrived with
[#98](https://github.com/Flowfin/jellyfin-plugin-discover/issues/98) is written
`{ get; }` because a collection an XML deserialiser fills carries no setter. So
the claim was about what the record holds and the command was about how three of
its lines are typed, and the two came apart the moment a fourth line was typed
differently. The pattern above is the widened one, and it is what found this.

The fourth field is keyed by a user. It is the list of accounts this plugin may
not pass a want on for, so it holds the server's own identifier for a person,
one entry per account an operator has restricted. That is a record about named
people on the plugin's own disk, and it is what
[#70](https://github.com/Flowfin/jellyfin-plugin-discover/issues/70) collects
rather than something this page decides.

The answer to this section's own question is still nothing, and it is now for a
narrower reason. The field is a control an operator sets over a user, not one a
user sets over themselves: nothing lets the person named in it read it, change
it or turn anything off. What the sentence rested on before, that no field here
is a user's, is no longer available, and what holds the answer up instead is that
no field here is settable BY a user.

The record held one field when this section was written and holds four now. The
two that arrived with
[#58](https://github.com/Flowfin/jellyfin-plugin-discover/issues/58) bound how
many titles this plugin may write into the library database, and neither is keyed
by a user or readable by one.

The page an administrator opens is not a page a user opens, and the neighbouring
control on the server, which decides who sees a surface at all, is set by an
administrator too. Whether a user ever gets a switch of their own is
[#57](https://github.com/Flowfin/jellyfin-plugin-discover/issues/57), and that is
the whole of it now. Question 7 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) stood beside
it here until 2026-08-24, and its answer takes a setting away rather than
deciding who holds it: with no personalisation built at 1.0 there is nothing for
a user to turn off, so this heading loses a candidate instead of gaining one.

## What an operator cannot avoid

Today, nothing, because nothing is sent.

Once a source is wired up and a key is entered, three things come with using the
plugin at all and no setting removes them. The source learns the server's network
address and the times it calls. The source learns which shelves are being asked
for. And the credential ties both to the person who registered it. An operator
who cannot accept those should not enter a key, and the plugin with no key
entered asks nothing, which is what
[#104](https://github.com/Flowfin/jellyfin-plugin-discover/issues/104) holds.

## What this page is not

It is read rather than watched. No server was booted, no request was made to the
source, and no client was pointed at an image host. Booting a server is
[#38](https://github.com/Flowfin/jellyfin-plugin-discover/issues/38) and putting
a client in front of it is
[#115](https://github.com/Flowfin/jellyfin-plugin-discover/issues/115), and the
one claim on this page that needs a running client, whether an image fetch
reaches the source's host directly, is marked above as unestablished rather than
guessed at.

A route in this tree reads part of this page, and this paragraph said none did.
`documented-commands` re-runs every command a tracked page pastes and compares
the answer against the output pasted under it, on every push and every pull
request, so every quotation above is one of its subjects.

When that comparison is made is narrower than the fact that it runs, and taking
the second for the first is the mistake to avoid here. All but one quotation
above quotes `origin/master`, and such a block used to be judged only where the
checkout stood on the mainline, which put the only reading of it after the only
moment anybody would act on it. It is read against both commits now: the
mainline the block describes, and the tree being pushed. A paste that still
agrees with the mainline and no longer agrees with the tree is a line this
change moved, and it is refused on the pull request:

    git grep -n 'FAIL  %s:%s: this change moves a line the page quotes' -- tools/documented-commands/run.sh
    tools/documented-commands/run.sh:398:    printf 'FAIL  %s:%s: this change moves a line the page quotes.\n' "$file" "$line"

The reason such a block reads `origin/master` at all survives that, which is
what the second run buys: a page changed together with the file it quotes agrees
with the tree it is about to land in, so it is read as a repair rather than
refused for being right.

The one exception is the retention block under "What is held about a person",
which reads a checkout of the server at two tags. That is not this repository, so
the reader refuses it wherever the run happens and no route here ever compares it.
It is in the weaker half deliberately: what the server's shipped logging keeps is
a fact about the server rather than about this tree, and there is nothing here to
read it from. What catches it going stale is somebody with such a checkout running
the command, the same as for the pages that quote the server's source elsewhere in
this repository.

That covers the quotations and nothing else on the page. A sentence with no
command under it is not a subject there, so every conclusion drawn above from a
quotation is unread, and so is the whole of "What an operator cannot avoid",
which is the section an operator deciding about this plugin reads first. A block
that agrees says the command still prints what is pasted under it, never that the
sentence over it is the right thing to conclude from those bytes. The reader's
own header is where that bound and the rest of what it cannot see are written.

What catches the unread half is still somebody running the commands. What catches
the quoted half is the reader, and this page has now been repaired both ways.
Six of the quotations above were re-derived by hand after the adapter moved under
[#68](https://github.com/Flowfin/jellyfin-plugin-discover/issues/68) and
[#251](https://github.com/Flowfin/jellyfin-plugin-discover/issues/251), and the
sentences they support did not move with them. Six were re-derived a second time
after three later changes moved the same file again, and that time nobody looked:
the run on the mainline refused them by name and the repair followed the refusal.
That the count is six on both occasions is a coincidence of which quotations the
adapter carries rather than a pattern.

The difference between the two rounds is the thing to keep. Before
[#273](https://github.com/Flowfin/jellyfin-plugin-discover/issues/273) the only
catch available to this page was somebody deciding to look, which is a catch that
does not arrive on a schedule. After it, a quotation that stops reproducing reds
the mainline. The sentences over the quotations are still in the first category,
and so is every claim on this page with no command under it.

One of those six failed in a way the others could not, and it is the form to
avoid here rather than a detail of that repair. Five were commands that find
their subject by content, so a moved subject changes the line number beside an
answer that is still the right answer. The sixth asked for a range of lines by
number, and a range that no longer holds what it was written for prints
different code with nothing to say it has: it had come to print the header block
quoted above it.

The quotations here therefore address their subject by content, with one
exception left standing on purpose: the four fields of a query, under "What a
request does not carry", are still asked for as a range of lines. They agree
with the tree today, and the range is kept because the surrounding form is what
makes those four readable as a whole.

That exception is a claim rather than a measurement, and deliberately so. A
command searching this page for that form matches the paragraph naming it, and
each quotation of the result adds another match, so the number a reader would be
handed counts this warning rather than the page's quotations. Counting them is
a person reading the page.
