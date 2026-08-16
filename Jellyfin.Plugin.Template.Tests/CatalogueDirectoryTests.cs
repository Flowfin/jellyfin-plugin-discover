using System;
using System.IO;
using Jellyfin.Plugin.Template.Catalogue;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Where the catalogue lives, and what a fresh server sees when nothing has
/// been written yet.
/// </summary>
/// <remarks>
/// No server is started here. The plugin is constructed with the paths fake,
/// which refuses every member of the server's path interface except the two the
/// base class reads, so a location derived from any other path fails here
/// rather than on somebody's server.
///
/// The folders these tests name are under the temporary directory and are named
/// rather than generated, because the invariant <c>no-random</c> refuses a draw
/// anywhere but the one source that supplies randomness, and a test folder is
/// not a reason to make an exception. Each test that writes owns its own name
/// and removes what it made.
/// </remarks>
public class CatalogueDirectoryTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    /// <summary>
    /// The catalogue sits under the folder the server derived for this plugin,
    /// and that folder is derived from the assembly the server loaded.
    /// </summary>
    /// <remarks>
    /// This is the whole of the collision argument, asserted rather than
    /// described. The plugin's data folder is named after the assembly's own
    /// file name, so another plugin writing into the same place would have to
    /// ship an assembly with this plugin's file name, and that is a collision
    /// which breaks the install before it reaches any catalogue. Nothing here
    /// chose a name and hoped it was unusual.
    /// </remarks>
    [Fact]
    public void TheCatalogueSitsUnderTheFolderTheServerDerivedForThisPlugin()
    {
        var plugin = new Plugin(
            new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(),
            new XmlSerializerThatRefusesEveryCall());

        var directory = new CatalogueDirectory(plugin.DataFolderPath);

        Assert.Equal(
            Path.Combine(plugin.DataFolderPath, CatalogueDirectory.Name),
            directory.FullPath,
            StringComparer.Ordinal);

        var assemblyFileName = Path.GetFileNameWithoutExtension(typeof(Plugin).Assembly.Location);
        Assert.StartsWith(
            assemblyFileName,
            Path.GetFileName(plugin.DataFolderPath),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A first run on a fresh server reads a catalogue that is not there and
    /// gets an empty one, without an error and without creating anything.
    /// </summary>
    /// <remarks>
    /// The common case on every new install. A store that treats it as a
    /// failure writes an error into the log of a server where nothing is wrong,
    /// and an operator reading that log learns to ignore it.
    ///
    /// The second half is the part that is easy to lose: reading must not
    /// create. A directory that appears because somebody looked is a trace of a
    /// feature nobody switched on, and it also makes "has this ever been
    /// refreshed" unanswerable from the disk, which is the distinction #63
    /// needs.
    /// </remarks>
    [Fact]
    public void AFreshServerReadsNothingAndIsNotAnError()
    {
        var folder = Folder("fresh-server");
        Remove(folder);

        var directory = new CatalogueDirectory(folder);

        Assert.Empty(directory.ListDocuments());
        Assert.False(Directory.Exists(directory.FullPath));
        Assert.False(Directory.Exists(folder));
    }

    /// <summary>
    /// A write creates the directory, and what was written is what is read
    /// back.
    /// </summary>
    [Fact]
    public void AWriteCreatesTheDirectoryAndTheDocumentIsListed()
    {
        var folder = Folder("a-write");
        Remove(folder);
        try
        {
            var directory = new CatalogueDirectory(folder);

            directory.EnsureExists();
            File.WriteAllText(directory.DocumentPath("shelves"), string.Empty);

            Assert.Equal("shelves", Assert.Single(directory.ListDocuments()));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// One operation removes everything this plugin wrote, and what it removed
    /// is derived from the store rather than from a list here.
    /// </summary>
    /// <remarks>
    /// #72's second condition. The documents are put there by the store rather
    /// than by this test writing files it chose, so what is asserted gone is
    /// what a real write path produces: the store creates the directory on the
    /// way to replacing a document, and the assertion afterwards is over
    /// <see cref="CatalogueDirectory.FullPath"/> rather than over the names
    /// that went in. A purge that missed a file a future writer adds would fail
    /// this without anybody having to remember to extend it.
    ///
    /// The plugin's own folder is asserted to survive. The catalogue is a
    /// directory inside it, and a purge that took the parent would take
    /// whatever else the plugin comes to keep beside the catalogue with it.
    /// </remarks>
    [Fact]
    public void OneOperationRemovesEverythingThisPluginWrote()
    {
        var folder = Folder("a-purge");
        Remove(folder);
        try
        {
            var directory = new CatalogueDirectory(folder);
            var store = new CatalogueDocumentStore(directory, new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());

            store.Write("shelves", new MemoryStream(new byte[] { 1, 2, 3 }));
            store.Write("trending", new MemoryStream(new byte[] { 4, 5, 6 }));

            Assert.Equal(2, directory.ListDocuments().Count);

            directory.RemoveEverything();

            Assert.Empty(directory.ListDocuments());
            Assert.False(Directory.Exists(directory.FullPath));
            Assert.True(Directory.Exists(folder));
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
    /// #72's fourth condition. Both are the ordinary case rather than misuse: a
    /// removal reaches a server that was installed and never configured, and an
    /// operator who did not see the first purge finish does it again. A method
    /// that threw on either would put an error in the log of every such server,
    /// and an uninstall path that treated that as a failure would stop halfway
    /// through cleaning up.
    /// </remarks>
    [Fact]
    public void PurgingAFreshInstallAndPurgingTwiceAreBothQuiet()
    {
        var folder = Folder("a-purge-twice");
        Remove(folder);
        try
        {
            var directory = new CatalogueDirectory(folder);

            directory.RemoveEverything();
            Assert.False(Directory.Exists(directory.FullPath));

            directory.EnsureExists();
            directory.RemoveEverything();
            directory.RemoveEverything();

            Assert.False(Directory.Exists(directory.FullPath));
            Assert.Empty(directory.ListDocuments());
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A write after a purge brings the directory back, which is why gone and
    /// staying gone are two claims.
    /// </summary>
    /// <remarks>
    /// Asserted rather than warned about in prose, because the difference
    /// decides what an uninstall has to do. The store creates the directory on
    /// the way to writing, so a purge that runs while anything is still able to
    /// write leaves a catalogue behind that was made after the operator asked
    /// for it to be gone. Whoever wires a purge to a page or to a removal has
    /// to stop the writers first, and this is the test that says so.
    /// </remarks>
    [Fact]
    public void AWriteAfterAPurgeBringsTheDirectoryBack()
    {
        var folder = Folder("a-write-after-a-purge");
        Remove(folder);
        try
        {
            var directory = new CatalogueDirectory(folder);
            var store = new CatalogueDocumentStore(directory, new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());

            store.Write("shelves", new MemoryStream(new byte[] { 1, 2, 3 }));
            directory.RemoveEverything();
            Assert.False(Directory.Exists(directory.FullPath));

            store.Write("shelves", new MemoryStream(new byte[] { 4, 5, 6 }));

            Assert.True(Directory.Exists(directory.FullPath));
            Assert.Equal("shelves", Assert.Single(directory.ListDocuments()));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Every name that would put a document outside the one directory this
    /// plugin writes to is refused.
    /// </summary>
    /// <remarks>
    /// The names are ones a shelf definition, a source identifier or a restored
    /// configuration could actually carry, rather than invented hostile input. A
    /// path joined from a name nobody checked lands somewhere else and still
    /// writes successfully, which is why this is refused where the path is built
    /// rather than where it is used.
    /// </remarks>
    /// <param name="documentName">The name a caller might have passed.</param>
    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../shelves")]
    [InlineData("..\\shelves")]
    [InlineData("nested/shelves")]
    [InlineData("nested\\shelves")]
    [InlineData("/shelves")]
    [InlineData("C:\\shelves")]
    [InlineData(" ")]
    public void ANameThatWouldLeaveTheDirectoryIsRefused(string documentName)
    {
        var directory = new CatalogueDirectory(Folder("refusals"));

        Assert.Throws<ArgumentException>(() => directory.DocumentPath(documentName));
    }

    /// <summary>
    /// An ordinary file name resolves inside the directory.
    /// </summary>
    /// <remarks>
    /// Beside the refusals on purpose. A guard refusing everything would pass
    /// the test above and be useless, and this is the assertion saying the
    /// refusal is narrower than that.
    /// </remarks>
    [Fact]
    public void AnOrdinaryNameResolvesInsideTheDirectory()
    {
        var directory = new CatalogueDirectory(Folder("resolves"));

        var path = directory.DocumentPath("shelves");

        Assert.Equal(Path.Combine(directory.FullPath, "shelves"), path, StringComparer.Ordinal);
    }

    /// <summary>
    /// A data folder that is not a fully qualified path is refused rather than
    /// resolved against whatever directory the server is running in.
    /// </summary>
    [Fact]
    public void ARelativeDataFolderIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new CatalogueDirectory("plugins/discover"));
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
