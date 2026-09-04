# TheTVDB

Source: thetvdb

Terms read: 2026-08-07

This page carries no `Host:` line, and that is deliberate. A host line says
where the source is reached, this plugin reaches TheTVDB nowhere, and the
hostname its API answers on was not read for this page. Writing one down would
be a claim made from memory rather than from the source.

Nothing on this page is legal advice; the reasoning behind that sentence is in
[the directory's README](README.md).

## This source is not taken, decided on 2026-09-04

No adapter exists and no request is made, and that is now a decision rather than
a question nobody had answered. TheTVDB is not a source of this plugin, neither
now nor as a later one, until somebody reopens
[#83](https://github.com/Flowfin/jellyfin-plugin-discover/issues/83) with a
reason the terms below do not answer.

The reason is what the rest of this page reads. A licence with a cost and a set
of obligations is worth carrying for a source that answers for titles the first
one cannot, and nothing read here shows that it does: the catalogue this plugin
fills is already filled from TMDb. What a second source adds for certain is the
obligation, which outlives the feature; what it adds beyond that is unmeasured.

The page stays as the record of what was read, so that the next person deciding
this reads a reading rather than the terms again. A page in this directory means
the terms have been read, not that the source has been taken, and this one now
means they were read and the answer was no.

What is not decided here is what the `Tvdb` member of `MetadataSource` is for.
That member is an identifier a title can carry, which reaches this plugin from
TMDb's own answer and from the server's library, and carrying an identifier is
not asking a source for anything. Nothing in this decision removes it.

## What was read, and where

Two pages, both on 2026-08-07:

- <https://thetvdb.com/api-information>, which states the licensing fees and the
  attribution requirement.
- <https://support.thetvdb.com/kb/faq.php?id=62>, which is the FAQ answer
  distinguishing the two key models.

TheTVDB's own terms of service were not read for this page. Whatever obligation
lives only in that document is absent here rather than absent from the source,
and the same applies to anything the source states in its documentation or in a
response header.

## The two models

A v4 key is per project rather than per user, and the FAQ says a project's key
must carry one of two things: a commercial licence, or subscriptions enabled for
its end users. Which one a project applies for is chosen at the moment it asks
for the key.

### Licensed

The project holds a contract with TheTVDB and a key of its own. The FAQ gives
the basis of the fee as usage, company size and how the data is used. The
licensing page states the fee by the revenue of the company holding the key,
and the first row is the one this project would be in:

| Company revenue         | Licensing fee              |
| ----------------------- | -------------------------- |
| Less than $50k per year | free, requires attribution |
| $50k to $250k per year  | $1,000 / year              |
| $250k to $1M per year   | $10,000 / year             |
| $1M+ or custom terms    | Contact Us                 |

The attribution the free row requires is stated on the same page, in a paragraph
of two sentences, of which this page quoted only the first until 2026-09-02:

    Unless approved by TheTVDB, attribution with a direct link to TheTVDB.com
    must be displayed to end users viewing metadata from our API.  Command line
    products or development libraries may display attribution on your about or
    readme pages.

The second sentence is a carve-out and it is the half that decides where an
attribution has to appear rather than whether one does. It names two kinds of
project that may attribute somewhere other than in front of the person looking
at the metadata, and a plugin for a media server is neither of them on any
reading this page is willing to make. So quoting the first sentence alone
understated nothing about the obligation and hid the one clause somebody would
reach for to argue the obligation is met by a readme. Both sentences are here
now, and the two spaces between them are the page's own.

### User-supported

The project ships no licence of its own and its key works only for users who
hold a TheTVDB subscription. The FAQ describes it as shifting the cost to the
end user:

    This would require all end users of a project to sign up for a user
    subscription to access your project's API.

and says an individual reaching a project this way supplies a subscriber PIN
alongside the project's key.

## What each model would require of this plugin

| Model          | What this plugin would have to hold                                                                                                                                                                                    | What an operator would have to do                                                                                                                        | Where it would land                                                                                                                                                                                                                                                   |
| -------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Licensed       | One key, held the way any other source key is held, and no per-user secret at all.                                                                                                                                     | Nothing beyond what the key answer to question 4 on [#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2) already asks of them.             | The adapter interface in [#73](https://github.com/Flowfin/jellyfin-plugin-discover/issues/73), with the key handled by [#77](https://github.com/Flowfin/jellyfin-plugin-discover/issues/77) and [#80](https://github.com/Flowfin/jellyfin-plugin-discover/issues/80). |
| Licensed       | Attribution with a direct link to TheTVDB.com, displayed to the end user who is looking at the metadata, not only on a settings page.                                                                                  | Nothing.                                                                                                                                                 | [#76](https://github.com/Flowfin/jellyfin-plugin-discover/issues/76), which already renders one source's notice and would render a second.                                                                                                                            |
| User-supported | A secret per user, stored per user, and a shelf that answers differently for two people on one server.                                                                                                                 | Tell every user who wants series shelves to buy a subscription and paste a PIN, and explain why some users see shelves and others do not.                | Nowhere. This is the feature [#83](https://github.com/Flowfin/jellyfin-plugin-discover/issues/83) says would be a milestone of its own, and it is named below.                                                                                                        |
| User-supported | Everything in [#70](https://github.com/Flowfin/jellyfin-plugin-discover/issues/70) reopened, because a per-user secret is data about a person that the feature now needs and the plugin holds until that user is gone. | Nothing extra, and that is the point: the operator carries a store of their users' paid credentials whether or not they wanted to be responsible for it. | [#70](https://github.com/Flowfin/jellyfin-plugin-discover/issues/70) and [#108](https://github.com/Flowfin/jellyfin-plugin-discover/issues/108).                                                                                                                      |

## What a per-user secret milestone would contain

Named here so that the cost of the second model is a list rather than a
sentence, and named without being planned, because nothing about this source is
built before the decision in
[#83](https://github.com/Flowfin/jellyfin-plugin-discover/issues/83).

- Where a per-user secret is stored, given that the configuration document this
  plugin already has is one document for the whole server.
- How a user supplies one at all, on a television client that has no way to
  type a secret into a plugin.
- What a shelf does for a user who has no PIN: absent, empty, or present and
  built from another source.
- What happens to the secret when the user is deleted, and how that is proven.
- What the operator sees, since a shelf that is empty for one user and full for
  another is the support request this plugin would generate most often.

## The finding that matters for the decision

[#83](https://github.com/Flowfin/jellyfin-plugin-discover/issues/83) is written
as a choice between a negotiated commercial licence and a per-user subscription
model, and reads the second as the likely one. The licensing page says the
first is free below $50k of company revenue, requiring attribution and nothing
else. This project has no revenue and already renders a source notice for its
first source, so the model that shapes the plugin is the one that would not have
to be taken.

That does not decide whether the source is wanted. It removes the cost the issue
assumed the decision would be made against, which is a different thing and the
reason this page exists before an answer does.

## What this page does not cover

It does not say whether the free row applies to a project that takes no revenue
at all rather than a company below a threshold, which is a question for
TheTVDB rather than a reading of the page.

It does not say what the licence obliges once held. Both readings above are of a
licensing summary and an FAQ, and the terms of service they sit under were not
read.

It says nothing about whether either reading is correct in law. It is an
engineering reading, made so a decision has something to be made from.
