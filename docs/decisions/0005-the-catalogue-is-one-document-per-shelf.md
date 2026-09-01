# 0005 The catalogue is one document per shelf

Decided. Raised by
[#65](https://github.com/Flowfin/jellyfin-plugin-discover/issues/65), whose third
condition is this and nothing else.

## What was decided

Each shelf's titles are kept in a document of its own inside the catalogue
directory, named after the question the shelf asks and the kind of title it
holds, and after nothing else.

    git grep -n 'public static string DocumentName' origin/master -- Jellyfin.Plugin.Template/Catalogue/CatalogueLayout.cs
    origin/master:Jellyfin.Plugin.Template/Catalogue/CatalogueLayout.cs:51:    public static string DocumentName(ShelfQuestion question, DiscoverTitleKind kind) =>
    origin/master:Jellyfin.Plugin.Template/Catalogue/CatalogueLayout.cs:71:    public static string DocumentName(Shelf shelf)

Six shelves ship, so six documents. Nothing about the number six is load-bearing
here; what is load-bearing is that a shelf is the unit a refresh succeeds or
fails at.

## Why this is not a preference

[0003](0003-the-catalogue-lives-in-the-plugins-own-data-folder.md) left this
open, and its own words were that the layout is decided against the concurrency
the refresh in
[#87](https://github.com/Flowfin/jellyfin-plugin-discover/issues/87) needs, so
deciding without that refresh would be deciding by preference. That reasoning
assumed the constraint would arrive with the refresh. It did not, and it has
arrived from three other places since, all of them settled:

**Partial success is already defined, and it defines the unit.** #79's second
condition is that a refresh which got three shelves of four keeps the fourth's
previous contents rather than dropping it. That is a statement about what a
failure leaves behind, and it fixes the granularity of a write before any
refresh exists to perform one. With one document for all, keeping the fourth
shelf means reading the previous document, merging three new shelves into it and
rewriting all four, so every partial success rewrites bytes for shelves nothing
asked about, and a defect in that merge loses a shelf that was never touched.
With one document per shelf, the fourth shelf is kept by not writing to it.

**Retention is enforced per record, and a deletion's granularity is the
layout.** #68 landed at ninety days with the ceiling read off whichever sources
are active:

    git grep -n 'public static CatalogueRetention Of' origin/master -- Jellyfin.Plugin.Template/Catalogue/CatalogueRetention.cs
    origin/master:Jellyfin.Plugin.Template/Catalogue/CatalogueRetention.cs:62:    public static CatalogueRetention Of(TimeSpan duration, IReadOnlyCollection<IMetadataSource> activeSources)

Removing what a retention refuses to serve is a refresh's own step now, and the
layout is what makes it cheap: a run rewrites the shelf whose records expired,
or removes that shelf's document where none of them survived, and every other
shelf is untouched because it is a different file. One document for all would
make the smallest such removal a rewrite of every shelf.

**The store states its own bounds and they point one way.** A read holds the
whole payload in memory to check it against its own checksum, so a document's
size is bounded by what the server can hold; one document for all multiplies
that peak by the number of shelves. And the store serialises writes and appends
one fixed suffix to a name while a write is in flight, so two shelves finishing
at the same moment contend for one document under the other layout and do not
under this one.

    git grep -n 'public const string TemporaryNameSuffix' origin/master -- Jellyfin.Plugin.Template/Catalogue/CatalogueDocumentStore.cs
    origin/master:Jellyfin.Plugin.Template/Catalogue/CatalogueDocumentStore.cs:88:    public const string TemporaryNameSuffix = ".writing";

None of the three is the refresh, and none of them is taste. A decision waiting
on an issue that is itself waiting on a decision nobody holds is a queue rather
than a dependency, and #65's own record says so in those words.

## What it was decided against

**One document for everything.** The argument for it is that a reader wanting the
catalogue opens one file and gets one consistent answer, and that a single write
either lands or does not. The consistency is real and it is the wrong property to
buy: a refresh that partly failed is not one moment, and a document presenting it
as one has to invent contents for the shelves that did not answer. The rest of
the argument is the three readings above pointing the other way.

It also costs nothing at uninstall, which is the place the single file would have
helped. The directory is removed whole rather than by name:

    git grep -n 'public void RemoveEverything' origin/master -- Jellyfin.Plugin.Template/Catalogue/CatalogueDirectory.cs
    origin/master:Jellyfin.Plugin.Template/Catalogue/CatalogueDirectory.cs:228:    public void RemoveEverything()

**One document per shelf per source.** Rejected as a distinction with no subject.
A shelf names exactly one source, so the source is a property of the shelf rather
than a second axis, and a name carrying it would move every document on the day a
shelf's source changed while the titles under it were still the titles that
shelf holds.

**A name derived from the shelf's display name.** Rejected, and this is the one
that would have been easy to write. A display name is text an operator may come
to set, so deriving a file name from it puts their characters on their disk, and
the two server platforms disagree about which of those characters is a
separator. That failure was already met once on this directory's own refusal,
where a name carrying a backslash was accepted on Linux and refused on Windows,
and the gate's Linux run is where it showed. The question and the kind are closed
sets, so a name built from them cannot carry a character nobody chose.

## What the choice buys that was not argued for

A shelf keeps its document when the things a name does not read change. Renaming
a row, moving its cap, or turning it off leaves the titles already fetched for it
where they are, so none of those is a refetch. That falls out of the name being
derived from what identifies a shelf rather than from what describes it, and it
is worth knowing before somebody adds a field to the name.

## What a reader of a mixed catalogue sees

Two shelves refreshed at different moments, because that is what partial success
means. Each record carries the instant its source answered:

    git grep -n 'public required DateTimeOffset FetchedAt' origin/master -- Jellyfin.Plugin.Template/Catalogue/DiscoverTitle.cs
    origin/master:Jellyfin.Plugin.Template/Catalogue/DiscoverTitle.cs:175:    public required DateTimeOffset FetchedAt

so the mixture is legible rather than hidden, and a shelf that is older than the
one beside it says so in its own records. A single document would have made the
same mixture invisible by giving the whole catalogue one apparent age.

## What is not decided here

What goes inside a document. This note fixes which document a shelf's titles are
in and what it is called; the payload's shape, and whether a shelf's document
carries anything besides its titles, is
[#67](https://github.com/Flowfin/jellyfin-plugin-discover/issues/67)'s and the
refresh's.

Nothing writes a catalogue document. The layout is a derivation and a set of
names, reached by the suite and by nothing else, so no server holds a document
under any of these names and the choice costs no migration today. It stops being
free at the first release, which is
[#107](https://github.com/Flowfin/jellyfin-plugin-discover/issues/107)'s
ordering rather than this note's.

## What would reverse this

A refresh whose smallest unit of success turns out not to be a shelf. If #87
lands something that can only half-fill a shelf and has to record which half,
the unit moves below a shelf and this note is superseded rather than edited.

A retention decision under #68 that expires a whole catalogue at once rather than
per record would remove the second of the three arguments, though not the first
or the third.

A shelf set large enough that a directory listing becomes the cost. Six is not
that, and operator-defined shelves are out of 1.0 by question 5's answer on
[#2](https://github.com/Flowfin/jellyfin-plugin-discover/issues/2), so the number
of documents is bounded by the shipped set until that changes.
