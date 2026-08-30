using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Storage;
using Jellyfin.Plugin.Template.Wants;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// One operation removing everything this plugin persisted, over every store
/// rather than over the first of them.
/// </summary>
/// <remarks>
/// No server is started here and nothing is installed. Both stores are the real
/// ones, constructed on a folder under the temporary directory, because what
/// this asserts is that the purge reaches where they actually write rather than
/// where a test says they do.
///
/// The folders these tests name are named rather than generated, for the reason
/// <c>CatalogueDirectoryTests</c> gives: <c>no-random</c> refuses a draw
/// anywhere but the one source that supplies randomness, and a test folder is
/// not a reason to make an exception.
/// </remarks>
public class PluginDataPurgeTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    /// <summary>
    /// After the purge nothing this plugin wrote is left, asserted over the
    /// folder rather than over the names that went into it.
    /// </summary>
    /// <remarks>
    /// #72's first and second conditions. Both stores write through their own
    /// real write paths first, so the tree the purge meets is the one a server
    /// would have rather than one this test laid out: the catalogue store
    /// creates its directory on the way to replacing a document, and the want
    /// list store creates its own on the way to writing a list.
    ///
    /// The assertion is over the data folder, so a store added later is covered
    /// without anybody having to extend this. That is the difference this test
    /// exists for: the same assertion written as "the catalogue directory is
    /// gone" passed for weeks while a second store wrote a second directory
    /// beside it.
    /// </remarks>
    [Fact]
    public void OneOperationRemovesEverythingThisPluginPersisted()
    {
        var folder = Folder("a-whole-purge");
        Remove(folder);
        try
        {
            var directory = new CatalogueDirectory(folder);
            var catalogue = new CatalogueDocumentStore(
                directory,
                new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());
            var wants = new WantListStore(folder, new LoggerThatRecordsWhatIsWritten<WantListStore>());

            catalogue.Write("shelves", new MemoryStream(new byte[] { 1, 2, 3 }));
            wants.Write(new List<LocalWant>());

            Assert.True(Directory.Exists(directory.FullPath));
            Assert.True(File.Exists(wants.FilePath));

            new PluginDataPurge(folder).RemoveEverything();

            Assert.False(Directory.Exists(folder));
            Assert.Empty(Directory.GetFileSystemEntries(Path.GetDirectoryName(folder)!, Path.GetFileName(folder)));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The catalogue's own removal reaches the catalogue and stops there, which
    /// is why this operation exists.
    /// </summary>
    /// <remarks>
    /// The state this repository was in before <see cref="PluginDataPurge"/>,
    /// pinned so it stays a property of that type rather than of this one. A
    /// purge narrowed back to <see cref="CatalogueDirectory.RemoveEverything"/>
    /// passes this and fails the test above, which is the whole difference
    /// between the two and the reason the assertion up there is over the folder.
    ///
    /// This asserts nothing about what <c>CatalogueDirectory</c> ought to do. Its
    /// subject is its own directory and it says so; leaving the folder beside it
    /// standing is that type being right rather than that type being narrow.
    /// </remarks>
    [Fact]
    public void TheCataloguesOwnRemovalLeavesTheWantListStanding()
    {
        var folder = Folder("a-narrow-purge");
        Remove(folder);
        try
        {
            var directory = new CatalogueDirectory(folder);
            var catalogue = new CatalogueDocumentStore(
                directory,
                new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());
            var wants = new WantListStore(folder, new LoggerThatRecordsWhatIsWritten<WantListStore>());

            catalogue.Write("shelves", new MemoryStream(new byte[] { 1, 2, 3 }));
            wants.Write(new List<LocalWant>());

            directory.RemoveEverything();

            Assert.False(Directory.Exists(directory.FullPath));
            Assert.True(File.Exists(wants.FilePath));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Purging a fresh install is not an error, and purging twice is not an
    /// error.
    /// </summary>
    /// <remarks>
    /// #72's fourth condition, over the whole folder rather than over one store.
    /// A removal reaches a server that was installed and never configured, and
    /// an operator who did not see the first purge finish does it again.
    /// </remarks>
    [Fact]
    public void PurgingAFreshInstallAndPurgingTwiceAreBothQuiet()
    {
        var folder = Folder("a-whole-purge-twice");
        Remove(folder);
        try
        {
            var purge = new PluginDataPurge(folder);

            purge.RemoveEverything();
            Assert.False(Directory.Exists(folder));

            var wants = new WantListStore(folder, new LoggerThatRecordsWhatIsWritten<WantListStore>());
            wants.Write(new List<LocalWant>());

            purge.RemoveEverything();
            purge.RemoveEverything();

            Assert.False(Directory.Exists(folder));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A folder that is not fully qualified is refused, and so is a blank one.
    /// </summary>
    /// <remarks>
    /// The same refusal both stores under this folder already make, and it
    /// matters more here than it does at either of them: this operation deletes
    /// recursively what it is pointed at, so a relative path resolved against
    /// whatever directory the server process happens to be running in is the
    /// difference between removing this plugin's folder and removing a directory
    /// of the same name somewhere nobody chose.
    /// </remarks>
    /// <param name="folder">The path a caller offers, which is refused.</param>
    [Theory]
    [InlineData("plugins/discover")]
    [InlineData("discover")]
    [InlineData(" ")]
    public void AFolderThatIsNotFullyQualifiedIsRefused(string folder)
    {
        Assert.Throws<ArgumentException>(() => new PluginDataPurge(folder));
    }

    /// <summary>
    /// A folder under the temporary directory, named after the test that owns
    /// it.
    /// </summary>
    /// <param name="name">What that test calls its folder.</param>
    /// <returns>The full path of the folder.</returns>
    private static string Folder(string name)
    {
        return Path.Combine(Path.GetTempPath(), TestFolders, name);
    }

    /// <summary>
    /// Removes a folder and everything under it, where it is there at all.
    /// </summary>
    /// <param name="folder">The folder to remove.</param>
    private static void Remove(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
