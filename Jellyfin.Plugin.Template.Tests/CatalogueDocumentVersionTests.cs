using System;
using System.IO;
using Jellyfin.Plugin.Template.Catalogue;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What happens when the catalogue on disk was written by a build whose format
/// is not this one's, in both directions.
/// </summary>
/// <remarks>
/// The documents come from <see cref="CatalogueDocumentsEveryVersionWrote"/>
/// rather than from the store, so what is being tested is a reader against
/// documents it did not write. A version's fixture is added when the version is
/// added and never rewritten.
///
/// The folders sit under the temporary directory and are named after the test
/// that owns them, the same way the store's own tests do it: <c>no-random</c>
/// refuses a drawn name, and two tests sharing a folder are two tests that pass
/// alone.
/// </remarks>
public class CatalogueDocumentVersionTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    /// <summary>
    /// A document from the version this build writes is read, and it did not
    /// have to be written by this run to be read.
    /// </summary>
    /// <remarks>
    /// First, because everything below asserts that a document was refused, and
    /// a reader that refuses every fixture would pass all of them.
    /// </remarks>
    [Fact]
    public void ADocumentFromTheVersionThisBuildWritesIsRead()
    {
        var folder = Folder("version-current");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);
            Place(folder, CatalogueDocumentsEveryVersionWrote.VersionOne);

            Assert.Equal(CatalogueDocumentsEveryVersionWrote.PayloadBytes(), store.Read("shelves"));
            Assert.Empty(log.Lines);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A document a newer build wrote is refused, and what the operator is told
    /// names both versions and what to do.
    /// </summary>
    /// <remarks>
    /// The downgrade and the restored backup. The assertion is on both numbers
    /// rather than on the sentence, because a message holding one of them
    /// cannot be acted on: it does not say whether the document is ahead or
    /// behind.
    /// </remarks>
    [Fact]
    public void ADocumentANewerBuildWroteIsRefusedAndBothVersionsAreNamed()
    {
        var folder = Folder("version-ahead");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);
            var path = Place(folder, CatalogueDocumentsEveryVersionWrote.VersionTwo);

            Assert.Null(store.Read("shelves"));

            var line = Assert.Single(log.Lines);
            Assert.Contains(path, line, StringComparison.Ordinal);
            Assert.Contains("version 2", line, StringComparison.Ordinal);
            Assert.Contains("version 1", line, StringComparison.Ordinal);
            Assert.Contains("Install a build that reads version 2", line, StringComparison.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A document from a format older than this build's is refused with the
    /// same clarity, because nothing here migrates one.
    /// </summary>
    /// <remarks>
    /// The other direction of the same rule. There is no migration to run and
    /// the message says so rather than leaving an operator to read the silence
    /// as a corrupt file.
    /// </remarks>
    [Fact]
    public void ADocumentFromAnOlderFormatIsRefusedAndSaysNothingMigratesIt()
    {
        var folder = Folder("version-behind");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);
            var path = Place(folder, CatalogueDocumentsEveryVersionWrote.VersionZero);

            Assert.Null(store.Read("shelves"));

            var line = Assert.Single(log.Lines);
            Assert.Contains(path, line, StringComparison.Ordinal);
            Assert.Contains("version 0", line, StringComparison.Ordinal);
            Assert.Contains("version 1", line, StringComparison.Ordinal);
            Assert.Contains("Nothing here migrates", line, StringComparison.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A first line in this store's family that names no version is reported as
    /// something this store never wrote, rather than as a version it cannot
    /// read.
    /// </summary>
    /// <remarks>
    /// The near-miss the two answers are separated by. Telling an operator to
    /// install a build that reads version <c>x</c> would send them looking for
    /// something that was never released.
    /// </remarks>
    [Fact]
    public void AFirstLineThatNamesNoVersionIsNotReportedAsAVersion()
    {
        var folder = Folder("version-unreadable");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);
            var path = Place(folder, CatalogueDocumentsEveryVersionWrote.VersionThatIsNotANumber);

            Assert.Null(store.Read("shelves"));

            var line = Assert.Single(log.Lines);
            Assert.Contains(path, line, StringComparison.Ordinal);
            Assert.Contains("names no catalogue format", line, StringComparison.Ordinal);
            Assert.DoesNotContain("Install a build", line, StringComparison.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// The version is judged before the length, so a document from a newer
    /// build is not reported as a truncated one.
    /// </summary>
    /// <remarks>
    /// A version 2 document owes nothing to a version 1 header, so it can be
    /// shorter than one. Reading its length first would tell an operator their
    /// disk cut a file short, which is the wrong thing to go and check.
    /// </remarks>
    [Fact]
    public void AShortDocumentFromANewerBuildIsAVersionRatherThanATruncation()
    {
        var folder = Folder("version-ahead-and-short");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);
            var directory = new CatalogueDirectory(folder);
            directory.EnsureExists();
            File.WriteAllBytes(
                directory.DocumentPath("shelves"),
                CatalogueDocumentsEveryVersionWrote.Bytes(CatalogueDocumentsEveryVersionWrote.VersionTwo).AsSpan(0, 25).ToArray());

            Assert.Null(store.Read("shelves"));

            var line = Assert.Single(log.Lines);
            Assert.Contains("version 2", line, StringComparison.Ordinal);
            Assert.DoesNotContain("shorter than the header", line, StringComparison.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A marker line is read as this build's own format only where it is
    /// exactly that, and the spellings that are nearly it are not.
    /// </summary>
    /// <param name="markerLine">The document's first line.</param>
    /// <remarks>
    /// A sign and surrounding space each parse as part of a number on some
    /// route or other, and each would make a document no build wrote into this
    /// build's own. They are refused as marker lines rather than read as
    /// version 1. The last two are the other end of the same rule: a family
    /// with no version after it, and a first line that only starts the same way.
    /// </remarks>
    [Theory]
    [InlineData("discover-catalogue/+1")]
    [InlineData("discover-catalogue/ 1")]
    [InlineData("discover-catalogue/1 ")]
    [InlineData("discover-catalogue/99999999999")]
    [InlineData("discover-catalogue/")]
    [InlineData("discover-catalogue1")]
    public void AMarkerLineThatIsNearlyThisFormatIsNotReadAsIt(string markerLine)
    {
        Assert.False(CatalogueDocumentFormat.TryReadVersion(markerLine, out _));
    }

    /// <summary>
    /// The marker a version writes is composed from its number, so the two
    /// cannot drift apart.
    /// </summary>
    [Fact]
    public void TheMarkerThisBuildWritesNamesTheVersionItReads()
    {
        Assert.True(CatalogueDocumentFormat.TryReadVersion(CatalogueDocumentFormat.CurrentMarker, out var version));
        Assert.Equal(CatalogueDocumentFormat.CurrentVersion, version);
    }

    /// <summary>
    /// Asking why the version this build reads was refused is a caller that has
    /// lost track of which branch it is on, and it is refused rather than
    /// answered with a sentence naming one version twice.
    /// </summary>
    [Fact]
    public void ThereIsNoReasonToGiveForTheVersionThisBuildReads()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogueDocumentFormat.WhyItCannotBeRead(CatalogueDocumentFormat.CurrentVersion));
    }

    /// <summary>
    /// Puts one of the fixtures in the catalogue directory under the name the
    /// tests read.
    /// </summary>
    /// <param name="folder">The folder standing in for the plugin's data folder.</param>
    /// <param name="document">The fixture to place.</param>
    /// <returns>The full path it was written to.</returns>
    private static string Place(string folder, string document)
    {
        var directory = new CatalogueDirectory(folder);
        directory.EnsureExists();

        var path = directory.DocumentPath("shelves");
        File.WriteAllBytes(path, CatalogueDocumentsEveryVersionWrote.Bytes(document));

        return path;
    }

    /// <summary>
    /// A store over a folder under the temporary directory, with the logger it
    /// writes to.
    /// </summary>
    /// <param name="folder">The folder standing in for the plugin's data folder.</param>
    /// <param name="log">The logger the store was given.</param>
    /// <returns>The store.</returns>
    private static CatalogueDocumentStore Store(string folder, out LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore> log)
    {
        log = new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>();
        return new CatalogueDocumentStore(new CatalogueDirectory(folder), log);
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
