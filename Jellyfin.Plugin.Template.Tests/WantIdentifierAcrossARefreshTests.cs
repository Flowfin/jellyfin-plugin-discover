using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Seam;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A want identifier survives the refresh that rewrote the title it is derived
/// from.
/// </summary>
/// <remarks>
/// #99's fourth condition, and it is a different assertion from the twelve in
/// <see cref="WantIdentifiersTests"/>. Those build an identity twice in memory
/// and derive from both; this one puts a title through the whole of what a
/// refresh does to it - a source answers, the record is written as bytes, the
/// document is replaced by a second run, and the bytes are read back into a
/// record - and derives from what came out.
///
/// The difference is the case #99 is written against. What #60 makes possible
/// to get wrong is a refresh recreating the item, and a refresh is where a
/// record stops being the object a test built and becomes bytes something else
/// wrote. A derivation stable over two constructions in one process says
/// nothing about a derivation stable over a round trip through a disk.
///
/// The second run answers with the same titles listed in another sequence and
/// with each title's identifiers listed in another sequence, because both are
/// things a source does between two responses and neither is a change to the
/// title. Reversed rather than drawn: `no-random` refuses a drawn sequence and
/// a shuffle nobody can reproduce is a failure nobody can repeat.
///
/// The identifier half of that is absorbed before the derivation sees it, and
/// saying so is more useful than letting a reader think it is what these
/// assertions turn on: <see cref="DiscoverTitleIdentity"/> orders its
/// identifiers by precedence in its own constructor, so a source listing them
/// the other way round produces the same record. It is reversed here anyway,
/// because what a fixture asserts is that the absorption happens rather than
/// that somebody remembered it does.
///
/// What the assertions do turn on is the disk. Both compare against the value
/// derived before anything was written, so a document that dropped an
/// identifier on the way out would red them rather than losing the same one
/// twice and looking stable.
/// </remarks>
public class WantIdentifierAcrossARefreshTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _fetchedAt =
        new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid _askingUser = new Guid("7b1f0f6a-0f5f-4a1e-9a9f-2d3c4b5a6978");

    /// <summary>
    /// The want a user would ask for is the same want after a refresh has
    /// rewritten the title.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARefreshThatRecreatedTheTitleDoesNotMoveTheWant()
    {
        var folder = Folder("want-across-a-refresh");
        Remove(folder);
        try
        {
            var shelf = Row();
            var document = CatalogueLayout.DocumentName(shelf);
            var store = Store(folder);

            var listed = new[] { Title("Heat", "949", "tt0113277"), Title("The Wire", "1438", "tt0306414") };
            var reversed = new[] { Flipped(listed[1]), Flipped(listed[0]) };

            var asked = listed
                .Select(title => WantIdentifiers.For(title.Identity, _askingUser))
                .OrderBy(want => want, StringComparer.Ordinal)
                .ToArray();

            await Refresh(shelf, listed, store).RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            var before = WantsFrom(store.Read(document));

            await Refresh(shelf, reversed, store).RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            var after = WantsFrom(store.Read(document));

            Assert.Equal(2, before.Length);
            Assert.Equal(asked, before.OrderBy(want => want, StringComparer.Ordinal), StringComparer.Ordinal);
            Assert.Equal(asked, after.OrderBy(want => want, StringComparer.Ordinal), StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The wants a shelf's titles derive are the wants the seam would carry.
    /// </summary>
    /// <remarks>
    /// The assertion above compares one derivation against another, so it would
    /// pass against a derivation that had stopped meaning anything. This one
    /// says the values are the ones the message takes, which is what makes the
    /// stability above worth having.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task TheWantsDerivedFromAStoredTitleAreTheOnesTheSeamCarries()
    {
        var folder = Folder("want-across-a-refresh-carried");
        Remove(folder);
        try
        {
            var shelf = Row();
            var store = Store(folder);
            var listed = new[] { Title("Heat", "949", "tt0113277") };

            await Refresh(shelf, listed, store).RunAsync(new[] { shelf }, progress: null, CancellationToken.None);

            var stored = CatalogueDocumentBody.Read(store.Read(CatalogueLayout.DocumentName(shelf))!).Single();

            var want = new Want
            {
                Identity = stored.Identity,
                Kind = stored.Kind,
                Name = stored.Name,
                AskingUser = _askingUser,
                WantIdentifier = WantIdentifiers.For(stored.Identity, _askingUser)
            };

            Assert.Equal(WantIdentifiers.For(listed[0].Identity, _askingUser), want.WantIdentifier, StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    private static string[] WantsFrom(byte[]? payload) =>
        CatalogueDocumentBody.Read(payload!)
            .Select(title => WantIdentifiers.For(title.Identity, _askingUser))
            .ToArray();

    private static Shelf Row() => new Shelf
    {
        DisplayName = "A row of films",
        Question = ShelfQuestion.Trending,
        Kind = DiscoverTitleKind.Movie,
        Source = MetadataSource.Tmdb,
        Cap = 5
    };

    private static DiscoverTitle Title(string name, string tmdb, string imdb) => new DiscoverTitle
    {
        Kind = DiscoverTitleKind.Movie,
        Name = name,
        VoteCount = name.Length,
        FetchedAt = _fetchedAt,
        Identity = new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Tmdb, tmdb),
            new ProviderIdentifier(MetadataSource.Imdb, imdb)
        })
    };

    /// <summary>
    /// The same title with its identifiers listed the other way round, which is
    /// a thing a source does between two responses and not a change to the
    /// title.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>The same title, built from a reversed identifier list.</returns>
    private static DiscoverTitle Flipped(DiscoverTitle title) => new DiscoverTitle
    {
        Kind = title.Kind,
        Name = title.Name,
        VoteCount = title.VoteCount,
        FetchedAt = title.FetchedAt,
        Identity = new DiscoverTitleIdentity(title.Identity.Identifiers.Reverse().ToArray())
    };

    private static CatalogueRefresh Refresh(Shelf shelf, DiscoverTitle[] titles, CatalogueDocumentStore store)
    {
        var source = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
        source.Answer(shelf.Ask(), SourceAnswer.Answered(titles, titles.Length));

        return new CatalogueRefresh(
            new[] { source },
            store,
            null,
            new ClockATestAdvances(_fetchedAt),
            new PauseATestWatches(),
            new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>());
    }

    private static CatalogueDocumentStore Store(string folder) =>
        new CatalogueDocumentStore(
            new CatalogueDirectory(folder),
            new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());

    private static string Folder(string name) => Path.Combine(Path.GetTempPath(), TestFolders, name);

    private static void Remove(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
