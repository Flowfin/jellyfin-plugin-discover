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
