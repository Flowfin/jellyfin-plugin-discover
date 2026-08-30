using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Seam;
using Jellyfin.Plugin.Template.Wants;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// That the operator's want list is still there after a restart, and that
/// clearing a row takes it off the disk.
/// </summary>
/// <remarks>
/// A restart is spelled here as a second register built over the same store.
/// Nothing in this suite can stop and start a server, and what a restart does to
/// this type is exactly that: the process that held the rows in memory is gone
/// and a new one reads whatever is on the disk. Where that spelling is weaker
/// than the thing it stands for is said under what none of this covers, on #97.
///
/// The folders sit under the temporary directory and are named after the test
/// that owns them, which is what the catalogue's own tests do: <c>no-random</c>
/// refuses a drawn name, and two tests sharing a folder are two tests that pass
/// alone.
/// </remarks>
public class WantListStoreTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _asked = new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid _asker = new Guid("7bdd08a4-1f0b-4a2f-9b57-4a6a2b4a0f11");

    private static readonly Guid _other = new Guid("2c1f5f2e-3a44-4d0e-8f61-19a7c2f1b8de");

    /// <summary>
    /// A want asked for before a restart is there after it, with every field it
    /// was recorded with.
    /// </summary>
    /// <remarks>
    /// #97's fifth condition, first half. Asserted field by field rather than by
    /// counting rows, because a list that came back with the right number of
    /// wants and the wrong people on them is the failure an operator would act
    /// on.
    /// </remarks>
    [Fact]
    public void AWantSurvivesARestart()
    {
        var folder = Folder("a-want-survives-a-restart");
        Remove(folder);
        try
        {
            var before = new LocalWantRegister(10, Store(folder));
            Assert.Equal(LocalWantOutcome.Recorded, before.Record(Wanted("Heat", "949"), _asked));

            var after = new LocalWantRegister(10, Store(folder));
            var row = Assert.Single(after.Wants());

            Assert.Equal("Heat", row.Name);
            Assert.Equal(_asked, row.AskedAt);
            Assert.Equal(LocalWantState.Asked, row.State);
            Assert.Equal(_asker, row.AskingUser);
            Assert.Equal(DiscoverTitleKind.Movie, row.Kind);
            Assert.Equal(1995, row.ReleaseYear);
            Assert.Equal("949", row.Identity.Primary.Value);
            Assert.Null(row.WithdrawnAt);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A want taken back is taken back after a restart too.
    /// </summary>
    /// <remarks>
    /// The state is the field a format writing an enum's number would lose the
    /// day somebody reorders the members, and a row that came back standing
    /// after its user withdrew it is the direction that matters: it is a want
    /// nobody asked for, shown to an operator as one somebody did.
    /// </remarks>
    [Fact]
    public void AWithdrawalSurvivesARestart()
    {
        var folder = Folder("a-withdrawal-survives-a-restart");
        Remove(folder);
        try
        {
            var before = new LocalWantRegister(10, Store(folder));
            var want = Wanted("Heat", "949");
            before.Record(want, _asked);

            Assert.True(before.Withdraw(want.WantIdentifier, _asked.AddMinutes(5)));

            var row = Assert.Single(new LocalWantRegister(10, Store(folder)).Wants());

            Assert.Equal(LocalWantState.Withdrawn, row.State);
            Assert.Equal(_asked.AddMinutes(5), row.WithdrawnAt);
            Assert.Equal(_asked, row.AskedAt);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Clearing a want removes it from the disk, and leaves the others there.
    /// </summary>
    /// <remarks>
    /// #97's fifth condition, second half. Asserted on the file's own bytes as
    /// well as through a second register, because the two fail differently: a
    /// register that dropped the row in memory and never wrote would pass a
    /// reload assertion on the same process and hand the row back after a
    /// restart.
    /// </remarks>
    [Fact]
    public void ClearingAWantRemovesItFromDisk()
    {
        var folder = Folder("clearing-removes-from-disk");
        Remove(folder);
        try
        {
            var store = Store(folder);
            var register = new LocalWantRegister(10, store);
            var cleared = Wanted("Heat", "949");
            register.Record(cleared, _asked);
            register.Record(Wanted("The Wire", "1438"), _asked.AddMinutes(1));

            Assert.True(register.Clear(cleared.WantIdentifier));

            var written = File.ReadAllText(store.FilePath, Encoding.UTF8);

            Assert.DoesNotContain("Heat", written, StringComparison.Ordinal);
            Assert.DoesNotContain(cleared.WantIdentifier, written, StringComparison.Ordinal);
            Assert.Contains("The Wire", written, StringComparison.Ordinal);

            var row = Assert.Single(new LocalWantRegister(10, Store(folder)).Wants());

            Assert.Equal("The Wire", row.Name);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Forgetting a user takes their rows off the disk and leaves everybody
    /// else's.
    /// </summary>
    /// <remarks>
    /// The sweep is what #70 asks for and what removal with the user means. A
    /// sweep that cleared memory and left the file would put the person back at
    /// the next restart, which is the one outcome a removal may not have.
    /// </remarks>
    [Fact]
    public void ForgettingAUserTakesTheirRowsOffTheDisk()
    {
        var folder = Folder("forgetting-a-user");
        Remove(folder);
        try
        {
            var register = new LocalWantRegister(10, Store(folder));
            register.Record(Wanted("Heat", "949"), _asked);
            register.Record(Wanted("The Wire", "1438", _other), _asked.AddMinutes(1));

            Assert.Equal(1, register.Forget(_asker));

            var row = Assert.Single(new LocalWantRegister(10, Store(folder)).Wants());

            Assert.Equal(_other, row.AskingUser);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The list is not inside the catalogue, so throwing the catalogue away does
    /// not throw it away too.
    /// </summary>
    /// <remarks>
    /// This is the decision the type exists to hold, asserted rather than
    /// written down: #72 removes the catalogue as one directory rather than as a
    /// list of names, so a want list written under it would go with an operation
    /// nobody aimed at it. The assertion is the removal actually running, not a
    /// comparison of two strings, because what has to be true is about a file
    /// after a directory was deleted.
    /// </remarks>
    [Fact]
    public void ThrowingTheCatalogueAwayLeavesTheWantList()
    {
        var folder = Folder("throwing-the-catalogue-away");
        Remove(folder);
        try
        {
            var store = Store(folder);
            new LocalWantRegister(10, store).Record(Wanted("Heat", "949"), _asked);

            var catalogue = new CatalogueDirectory(folder);
            catalogue.EnsureExists();
            catalogue.RemoveEverything();

            Assert.False(Directory.Exists(catalogue.FullPath));
            Assert.True(File.Exists(store.FilePath));
            Assert.Single(new LocalWantRegister(10, Store(folder)).Wants());
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A first start with nothing on the disk is an empty list rather than a
    /// failure.
    /// </summary>
    [Fact]
    public void AFirstStartReadsNothingAndIsNotAnError()
    {
        var folder = Folder("a-first-start");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);

            Assert.Empty(store.Read());
            Assert.Empty(new LocalWantRegister(10, store).Wants());
            Assert.Empty(log.Lines);
            Assert.False(Directory.Exists(store.DirectoryPath));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A list whose first line is not this build's is refused, and the register
    /// starts empty rather than reading it field by field.
    /// </summary>
    /// <param name="marker">What stood where the format line goes.</param>
    /// <remarks>
    /// The same refusal #67 makes for the catalogue, one file over. What is told
    /// to the operator names both spellings, because a message saying the file
    /// is wrong leaves them with nothing to act on.
    /// </remarks>
    [Theory]
    [InlineData("discover-wants/2")]
    [InlineData("discover-wants/0")]
    [InlineData("discover-catalogue/1")]
    [InlineData("[]")]
    public void AListThisBuildDoesNotReadIsRefusedAndTheRegisterStartsEmpty(string marker)
    {
        var folder = Folder("a-list-this-build-does-not-read");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);
            Place(store, marker + "\n[]");

            Assert.Empty(store.Read());
            Assert.Empty(new LocalWantRegister(10, store).Wants());
            Assert.NotEmpty(log.Lines);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A list that is this build's format and not its content is refused whole.
    /// </summary>
    /// <param name="payload">What stood after the format line.</param>
    /// <remarks>
    /// Refused rather than read as far as it goes. Half an operator's list is
    /// one they will act on believing it is all of it, and the absence of a list
    /// is a state the page can say something about.
    /// </remarks>
    [Theory]
    [InlineData("not a list")]
    [InlineData("{}")]
    [InlineData("[{\"wantIdentifier\":\"a\"}]")]
    [InlineData("[{\"wantIdentifier\":\"a\",\"kind\":\"movie\",\"name\":\"Heat\",\"askingUser\":\"not-a-user\",\"askedAt\":\"2026-08-30T09:00:00.0000000Z\",\"state\":\"asked\",\"identifiers\":{\"tmdb\":\"949\"}}]")]
    [InlineData("[{\"wantIdentifier\":\"a\",\"kind\":\"movie\",\"name\":\"Heat\",\"askingUser\":\"7bdd08a4-1f0b-4a2f-9b57-4a6a2b4a0f11\",\"askedAt\":\"2026-08-30T09:00:00.0000000Z\",\"state\":\"pending\",\"identifiers\":{\"tmdb\":\"949\"}}]")]
    public void AListInThisFormatThatIsNotThisContentIsRefusedWhole(string payload)
    {
        var folder = Folder("a-list-that-is-not-this-content");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);
            Place(store, WantListStore.CurrentMarker + "\n" + payload);

            Assert.Empty(store.Read());
            Assert.NotEmpty(log.Lines);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Rows the bound no longer admits are dropped on the way in and counted,
    /// rather than starting a register over its own bound.
    /// </summary>
    /// <remarks>
    /// The case is a bound somebody lowered under a list that was already
    /// longer. A register loaded over its bound refuses every new gesture until
    /// somebody clears rows by hand, which reads to a user as a button that has
    /// stopped working and to an operator as nothing at all.
    /// </remarks>
    [Fact]
    public void RowsTheBoundNoLongerAdmitsAreDroppedOnLoadAndCounted()
    {
        var folder = Folder("rows-the-bound-no-longer-admits");
        Remove(folder);
        try
        {
            var wide = new LocalWantRegister(10, Store(folder));
            wide.Record(Wanted("Heat", "949"), _asked);
            wide.Record(Wanted("The Wire", "1438"), _asked.AddMinutes(1));
            wide.Record(Wanted("Alien", "348"), _asked.AddMinutes(2));

            var narrow = new LocalWantRegister(2, Store(folder));

            Assert.Equal(2, narrow.Count);
            Assert.Equal(1, narrow.DroppedOnLoad);
            Assert.Equal(LocalWantOutcome.Refused, narrow.Record(Wanted("Casino", "524"), _asked.AddMinutes(3)));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A register given nowhere to write still works, and keeps nothing.
    /// </summary>
    /// <remarks>
    /// The shape every test that is not about persistence uses, asserted here so
    /// that it is a property rather than an accident of how those tests are
    /// written.
    /// </remarks>
    [Fact]
    public void ARegisterWithNoStoreHoldsItsRowsAndWritesNothing()
    {
        var folder = Folder("a-register-with-no-store");
        Remove(folder);
        try
        {
            var register = new LocalWantRegister(10);

            Assert.Equal(LocalWantOutcome.Recorded, register.Record(Wanted("Heat", "949"), _asked));
            Assert.Single(register.Wants());
            Assert.Equal(0, register.DroppedOnLoad);
            Assert.False(Directory.Exists(folder));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The store is not handed a null, and its folder has to be one the server
    /// named.
    /// </summary>
    [Fact]
    public void TheStoreIsNotHandedANullOrARelativeFolder()
    {
        Assert.Throws<ArgumentNullException>(() => new WantListStore(Path.GetTempPath(), null!));
        Assert.Throws<ArgumentException>(() => new WantListStore("   ", new LoggerThatRecordsWhatIsWritten<WantListStore>()));
        Assert.Throws<ArgumentException>(() => new WantListStore("wants", new LoggerThatRecordsWhatIsWritten<WantListStore>()));
        Assert.Throws<ArgumentNullException>(() => Store(Folder("null-rows")).Write(null!));
    }

    private static Want Wanted(string name, string identifier) => Wanted(name, identifier, _asker);

    private static Want Wanted(string name, string identifier, Guid user)
    {
        var identity = new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Tmdb, identifier)
        });

        return new Want
        {
            Identity = identity,
            Kind = DiscoverTitleKind.Movie,
            Name = name,
            ReleaseYear = 1995,
            AskingUser = user,
            WantIdentifier = WantIdentifiers.For(identity, user)
        };
    }

    private static string Folder(string named) => Path.Combine(Path.GetTempPath(), TestFolders, "want-list-" + named);

    private static WantListStore Store(string folder) => Store(folder, out _);

    private static WantListStore Store(string folder, out LoggerThatRecordsWhatIsWritten<WantListStore> log)
    {
        log = new LoggerThatRecordsWhatIsWritten<WantListStore>();
        return new WantListStore(folder, log);
    }

    private static void Place(WantListStore store, string content)
    {
        Directory.CreateDirectory(store.DirectoryPath);
        File.WriteAllBytes(store.FilePath, Encoding.UTF8.GetBytes(content));
    }

    private static void Remove(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
