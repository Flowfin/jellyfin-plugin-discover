# Sources that need a user's own authorisation

Deferred, with the reason and the revisit condition below. Raised in
[#84](https://github.com/Flowfin/jellyfin-plugin-discover/issues/84).

Some sources are interesting precisely because they know about a person: a
watchlist, a follow graph, a set of ratings. Reaching one is not a matter of a
better key. It means each user on the server authorising this plugin against
that service, this plugin holding something on that user's behalf afterwards,
and keeping it working.

The deferral is a decision somebody can argue with, and not an obvious next
step somebody adds in a hurry between two other changes.

## What a per-user token would require

**Storage.** A token per user per service, a refresh token beside it, and the
moment each expires. Not in the plugin configuration: the configuration page
reads the configuration through the server's API, so everything on that object
is served to whoever can open the page, which is the shape
[#80](https://github.com/Flowfin/jellyfin-plugin-discover/issues/80) is about
for a single server-wide key. A per-user token has the same problem multiplied
by the user count, and it is somebody's credential at a third party rather than
this project's.

**Refresh.** A token that expires needs refreshing on a schedule and at use, and
a refresh that fails needs to be told apart from a service that is down. The
first leaves a user disconnected until they act; the second resolves itself.
Treating them the same means either nagging every user during an outage or
leaving a user silently disconnected for good.

**Revocation, in both directions.** A user revoking access at the service leaves
this plugin holding a token that no longer works, which it discovers on the next
call. A user revoking here has to be revoked at the service too, or the grant
outlives the button that was supposed to remove it, and this plugin has told
them otherwise.

**Deletion.** A user removed from the server takes what this plugin held about
them with them, which is the third condition on
[#70](https://github.com/Flowfin/jellyfin-plugin-discover/issues/70). For a
token, removing the row is not the whole of it: the grant at the service
survives a deleted account here, so deletion has to reach the service or say
plainly that it does not.

**What [#70](https://github.com/Flowfin/jellyfin-plugin-discover/issues/70)
would have to say.** Its list is everything this plugin persists that refers to
a user, with why the feature cannot work without it and how long it is kept. A
per-user token is the largest entry that list could ever carry and the only one
whose loss harms the user at a third party rather than on their own server. It
also changes the shape of that list: everything else in this plan is either
shared by every user or is a record of something a user did on this server, and
a token is neither.

## Why it waits

The security surface is different in kind from a server-wide read-only key, and
it lands on a plugin whose whole job is drawing rows. A key that fetches a list
of popular films is a rate budget; a set of user grants is a credential store,
and a credential store is a thing to build deliberately or not at all.

The benefit is also the smallest at the moment it is most expensive. The shelves
this plan ships are the ones that work on a server whose key was entered a
minute ago, with no history, which is every first run. A shelf built from one
person's watchlist is worth nothing on that server and worth something on a
server that has been running for a year, so the feature that costs the most is
the one whose value arrives last.

Nothing in this plan assumes it. The shelf record carries no user, and shelves
that depend on what a user has watched are their own question, question 7 on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), held by
[#90](https://github.com/Flowfin/jellyfin-plugin-discover/issues/90). That
question is about sending a signal to a third party; this page is about holding
a credential for one. They are separate and answering one does not answer the
other.

## What would revisit it

- Operators asking for a shelf that only a per-user source can build, rather
  than this plan imagining they would. One request is not the condition; the
  condition is that it is the thing being asked for.
- The server growing a per-user secret store the server itself owns, with the
  storage, the encryption at rest and the deletion-on-user-removal handled
  there. That removes most of the first list above, and with it most of the
  argument on this page.
- A source this plan already talks to requiring a per-user grant for what it
  supplies today. Then the choice is not whether to build this but whether to
  keep that source.

## Whether the interface could take one later

It could, and the reason is one sentence: the only thing a per-user source needs
that a server-wide one does not is which user is asking, and that arrives as a
field on the question without changing a signature or a caller.

The interface asks a source two things:

    git grep -n 'MetadataSource Source { get; }\|Task<SourceAnswer> FetchAsync' origin/master -- Jellyfin.Plugin.Template/Sources/IMetadataSource.cs
    origin/master:Jellyfin.Plugin.Template/Sources/IMetadataSource.cs:46:    MetadataSource Source { get; }
    origin/master:Jellyfin.Plugin.Template/Sources/IMetadataSource.cs:66:    Task<SourceAnswer> FetchAsync(SourceQuery query, CancellationToken cancellationToken);

Neither of those moves. The question does, and it moves by gaining a field
rather than by being rebuilt:

    git grep -n -A 5 'public readonly record struct SourceQuery' origin/master -- Jellyfin.Plugin.Template/Sources/SourceQuery.cs
    origin/master:Jellyfin.Plugin.Template/Sources/SourceQuery.cs:44:public readonly record struct SourceQuery(
    origin/master:Jellyfin.Plugin.Template/Sources/SourceQuery.cs-45-    string Name,
    origin/master:Jellyfin.Plugin.Template/Sources/SourceQuery.cs-46-    DiscoverTitleKind Kind,
    origin/master:Jellyfin.Plugin.Template/Sources/SourceQuery.cs-47-    int? StartIndex,
    origin/master:Jellyfin.Plugin.Template/Sources/SourceQuery.cs-48-    int? Limit)

Those four are positional, so a fifth positional parameter would be a change to
every place a query is built. An init-only property with no value by default is
not, and it is the shape that fits: a query carrying nobody is the
server-wide case that every shelf asks today, and a query carrying a user is the
per-user case, so the same adapter can serve both and the shelves that ask for
neither are untouched.

Three things a per-user source would otherwise strain against are already where
they belong.

Authentication is the adapter's, not the interface's, which is what the
interface's own remarks say it is for. A token store, a refresh and a revocation
path all sit inside one adapter and none of them appears in a signature, so
nothing about them reaches a caller.

The state a per-user source spends most of its life in already has an answer.
A user who has not authorised anything is a source that was asked and should not
have been, which is neither an error nor an empty shelf:

    git grep -n 'NotConfigured = 2' origin/master -- Jellyfin.Plugin.Template/Sources/SourceOutcome.cs
    origin/master:Jellyfin.Plugin.Template/Sources/SourceOutcome.cs:43:    NotConfigured = 2,

That case exists because a server-wide key can be missing. It reads the same way
per user, and without it every unauthorised user would look to a refresh like a
source that had nothing to say.

Which body answered is named rather than inferred, so a service that only ever
speaks per user is a member added to the body list and a position in the
precedence beside it, which is the change point that already exists for adding
any source.

What this does not settle is the thing that is not the interface's business:
whether an answer fetched for one user may be kept where another user's request
could reach it. That is the catalogue rather than the source, and it is the list
[#70](https://github.com/Flowfin/jellyfin-plugin-discover/issues/70) asks for.
None of this has been exercised, because no per-user adapter exists and none is
planned; it is a reading of the four files above rather than a thing that has
been built against them.

## What this page does not settle

Nothing on this page is refused by anything. Three invariant rules carry this
vocabulary and none of them has a stored grant as its subject:

    git grep -li 'token\|credential\|secret' -- tools/invariants/rules/
    tools/invariants/rules/no-random.rule
    tools/invariants/rules/no-secret-in-log.rule
    tools/invariants/rules/no-server-provider-key.rule

`no-secret-in-log` refuses a log statement that interpolates one,
`no-server-provider-key` refuses reaching for the server's own metadata key, and
`no-random` names a token only in the prose explaining why its generator is not
a cryptographic one. A change that started building a per-user credential store
would build, package and pass every route in this tree. This page is read by a
person or not at all.
