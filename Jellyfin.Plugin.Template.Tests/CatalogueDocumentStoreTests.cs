using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Jellyfin.Plugin.Template.Catalogue;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What a reader sees while a write is happening, after one was interrupted,
/// and when the bytes on disk are not the ones that were written.
/// </summary>
/// <remarks>
/// These tests use the real file system rather than an abstraction over it, on
/// purpose. The property being tested is a property of the file system: that
/// replacing a name is one operation and writing bytes is not. An interface in
/// front of it would let the tests pass against a fake that replaces a name the
/// same way whether the code does or not, which is the assertion this issue's
/// own text refuses.
///
/// The folders are under the temporary directory and are named after the test
/// that owns them, because <c>no-random</c> refuses a drawn name and two tests
/// sharing a folder is two tests that pass alone. Each removes what it made.
/// </remarks>
public class CatalogueDocumentStoreTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    /// <summary>
    /// What is written is what comes back.
    /// </summary>
    /// <remarks>
    /// First because everything below asserts that this did not happen, and a
    /// suite where the only passing case is a refusal proves a store that
    /// refuses everything.
    /// </remarks>
    [Fact]
    public void WhatIsWrittenIsWhatIsRead()
    {
        var folder = Folder("round-trip");
        Remove(folder);
        try
        {
            var store = Store(folder, out _);
            var payload = Encoding.UTF8.GetBytes("the shelves as they stood");

            store.Write("shelves", new MemoryStream(payload));

            Assert.Equal(payload, store.Read("shelves"));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A document nobody has written reads as absent, and nothing is logged for
    /// it.
    /// </summary>
    /// <remarks>
    /// The state of every fresh install. The second half is the part that is
    /// easy to lose: a store warning on a read that found nothing puts a line
    /// in the log of every server where nothing is wrong, and an operator who
    /// sees it on day one stops reading the log by day three.
    /// </remarks>
    [Fact]
    public void ADocumentNobodyHasWrittenIsAbsentAndSilent()
    {
        var folder = Folder("absent");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);

            Assert.Null(store.Read("shelves"));
            Assert.Empty(log.Lines);
            Assert.False(Directory.Exists(Path.Combine(folder, CatalogueDirectory.Name)));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A write interrupted part way through leaves the previous document
    /// standing, and leaves nothing half-written behind.
    /// </summary>
    /// <remarks>
    /// This is the failure the issue is about, and it is driven by stopping the
    /// bytes rather than by asserting that a move was called. The content
    /// stream hands over some of its payload and then fails, which is a disk
    /// that filled or a source that died mid-refresh, and the assertion is what
    /// a reader gets afterwards.
    ///
    /// The near-miss is one line: a store writing at the document's own path
    /// instead of beside it passes every other test here and fails this one,
    /// because the previous document is then already truncated when the
    /// interruption arrives.
    /// </remarks>
    [Fact]
    public void AnInterruptedWriteLeavesThePreviousDocumentStanding()
    {
        var folder = Folder("interrupted");
        Remove(folder);
        try
        {
            var store = Store(folder, out _);
            var previous = Encoding.UTF8.GetBytes("the shelves as they stood");
            store.Write("shelves", new MemoryStream(previous));

            var replacement = Encoding.UTF8.GetBytes("a refresh that never finished arriving");
            var interrupted = new ContentThatArrivesInPieces(replacement, 4, 12);

            Assert.Throws<IOException>(() => store.Write("shelves", interrupted));

            Assert.Equal(previous, store.Read("shelves"));

            var directory = new CatalogueDirectory(folder);
            Assert.Equal("shelves", Assert.Single(directory.ListDocuments()));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A file left behind by a write that never reached its move is not a
    /// document, and the previous one still reads.
    /// </summary>
    /// <remarks>
    /// The case the test above cannot produce, because there the process lived
    /// long enough to clean up. A server killed between the write and the move
    /// leaves the file where it fell, and the question is what the next read
    /// does with it.
    ///
    /// The listing is asserted as it is rather than as it should be: the
    /// leftover is in it. It is the directory's own listing rather than the
    /// store's answer: <see cref="CatalogueDocumentStore.DocumentNames"/> is
    /// what reads it, and it drops the suffix there, so a filter added here
    /// would be a second place that knows what a temporary file is called.
    /// What matters for this issue is that a reader asking for the document
    /// gets the previous one whole.
    /// </remarks>
    [Fact]
    public void AFileLeftBehindByAKilledWriteIsNotADocument()
    {
        var folder = Folder("killed-mid-write");
        Remove(folder);
        try
        {
            var store = Store(folder, out _);
            var previous = Encoding.UTF8.GetBytes("the shelves as they stood");
            store.Write("shelves", new MemoryStream(previous));

            var directory = new CatalogueDirectory(folder);
            File.WriteAllBytes(
                directory.DocumentPath("shelves") + CatalogueDocumentStore.TemporaryNameSuffix,
                Encoding.UTF8.GetBytes("half of a refresh"));

            Assert.Equal(previous, store.Read("shelves"));
            Assert.Contains(
                "shelves" + CatalogueDocumentStore.TemporaryNameSuffix,
                directory.ListDocuments(),
                StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A document whose bytes were cut short after it was written is refused on
    /// read, and the log names the file.
    /// </summary>
    [Fact]
    public void ATruncatedDocumentIsTreatedAsAbsentAndTheLogNamesIt()
    {
        var folder = Folder("truncated");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);
            var directory = new CatalogueDirectory(folder);
            store.Write("shelves", new MemoryStream(Encoding.UTF8.GetBytes("the shelves as they stood")));

            var path = directory.DocumentPath("shelves");
            var written = File.ReadAllBytes(path);
            File.WriteAllBytes(path, written.AsSpan(0, written.Length - 6).ToArray());

            Assert.Null(store.Read("shelves"));
            Assert.Contains(path, Assert.Single(log.Lines), StringComparison.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A document whose bytes were altered after it was written is refused on
    /// read, and the log names the file.
    /// </summary>
    /// <remarks>
    /// Beside the truncation on purpose. Both are refused by the same leg, and
    /// this is the one that says the leg is a checksum rather than a size: the
    /// file is exactly as long as it was and one byte of it is different, which
    /// is what a backup restored from a bad disk looks like and what nothing
    /// about the length can see.
    /// </remarks>
    [Fact]
    public void AnAlteredDocumentIsTreatedAsAbsentAndTheLogNamesIt()
    {
        var folder = Folder("altered");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);
            var directory = new CatalogueDirectory(folder);
            store.Write("shelves", new MemoryStream(Encoding.UTF8.GetBytes("the shelves as they stood")));

            var path = directory.DocumentPath("shelves");
            var written = File.ReadAllBytes(path);
            written[^4] ^= 0x01;
            File.WriteAllBytes(path, written);

            Assert.Null(store.Read("shelves"));
            Assert.Contains(path, Assert.Single(log.Lines), StringComparison.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A file at a document's name that this store never wrote is refused
    /// rather than parsed.
    /// </summary>
    /// <remarks>
    /// Two lengths, because the two take different routes through the reader:
    /// a file shorter than the header cannot be parsed at all, and one long
    /// enough to have a header has to be refused on what the header says.
    /// </remarks>
    /// <param name="contents">What the file at the document's name holds.</param>
    [Theory]
    [InlineData("shelves")]
    [InlineData("--------------------------------------------------------------------------------------------------------------------------")]
    public void AFileThisStoreNeverWroteIsRefused(string contents)
    {
        var folder = Folder("not-a-document");
        Remove(folder);
        try
        {
            var store = Store(folder, out var log);
            var directory = new CatalogueDirectory(folder);
            directory.EnsureExists();
            var path = directory.DocumentPath("shelves");
            File.WriteAllText(path, contents);

            Assert.Null(store.Read("shelves"));
            Assert.Contains(path, Assert.Single(log.Lines), StringComparison.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Two refreshes writing one document at the same time produce one of them
    /// and never a mixture.
    /// </summary>
    /// <remarks>
    /// The writers are released together and hand their payloads over a byte at
    /// a time, so an unguarded store has every chance to interleave rather than
    /// only the theoretical possibility of it. Every payload is the same length
    /// and differs in every byte, so a mixture is not a document that happens to
    /// look like one of them.
    ///
    /// What makes this a proof rather than an observation is the near-miss:
    /// removing the lock from the write leaves the writers sharing one
    /// temporary file, and the document that results is either a mixture of two
    /// payloads or a write that failed because the file was already open. Both
    /// redden this test and neither reddens any other test here.
    /// </remarks>
    [Fact]
    public void TwoOverlappingWritesProduceOneOfThemAndNotAMixture()
    {
        var folder = Folder("overlapping");
        Remove(folder);
        try
        {
            var store = Store(folder, out _);

            var payloads = new List<byte[]>();
            for (var writer = 0; writer < 4; writer++)
            {
                var payload = new byte[8192];
                Array.Fill(payload, (byte)('a' + writer));
                payloads.Add(payload);
            }

            var failures = new List<Exception>();
            var failureLock = new object();
            using var released = new ManualResetEventSlim(false);

            var threads = new List<Thread>();
            foreach (var payload in payloads)
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        released.Wait();
                        store.Write("shelves", new ContentThatArrivesInPieces(payload, 1, -1));
                    }
                    catch (IOException failure)
                    {
                        lock (failureLock)
                        {
                            failures.Add(failure);
                        }
                    }
                });

                thread.IsBackground = true;
                threads.Add(thread);
                thread.Start();
            }

            released.Set();
            foreach (var thread in threads)
            {
                thread.Join();
            }

            Assert.Empty(failures);

            var read = store.Read("shelves");
            Assert.NotNull(read);
            Assert.Contains(payloads, payload => payload.AsSpan().SequenceEqual(read));
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// A name that would put a document outside the catalogue directory is
    /// refused by the store as well as by the directory.
    /// </summary>
    /// <remarks>
    /// Here so that the refusal is known to be on the path a write actually
    /// takes. A store resolving its own path beside the directory's would pass
    /// the directory's own tests and write wherever it liked.
    /// </remarks>
    [Fact]
    public void ANameThatWouldLeaveTheDirectoryIsRefusedByTheStore()
    {
        var folder = Folder("refusals");
        var store = Store(folder, out _);

        Assert.Throws<ArgumentException>(() => store.Write("../shelves", new MemoryStream()));
        Assert.Throws<ArgumentException>(() => store.Read("../shelves"));
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
