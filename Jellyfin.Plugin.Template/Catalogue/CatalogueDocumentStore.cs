using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// Reads and writes the documents in the catalogue directory, so that a reader
/// sees the previous document or the new one and never part of either.
/// </summary>
/// <remarks>
/// The failure this exists for is not losing the newest refresh, which the next
/// refresh repeats anyway. It is losing the previous one and starting from
/// nothing, because a source outage on the morning after a power cut is then an
/// empty surface for every user rather than a stale one.
///
/// Two properties hold it up and they are separate. A write goes to a temporary
/// file beside the document and the document is replaced by moving that file
/// over it, so the bytes of a half-finished write are never at the name a reader
/// opens; on Linux that is <c>rename(2)</c> and on Windows it is the replacing
/// form of a move, and both change the directory entry in one operation. And a
/// document carries the checksum of its own payload, so bytes that are
/// truncated or altered after the move are refused on read instead of being
/// handed to whoever asked.
///
/// The header carries no byte count, and that is a decision rather than an
/// omission. A count refuses nothing the checksum does not already refuse:
/// every truncation and every extra byte changes the payload, and therefore
/// changes what it hashes to. A count would be a leg no test could redden on
/// its own, which is a guard shipping without proof that it bites. What such a
/// count would have been good for, telling an operator which failure this was,
/// is in the message instead.
///
/// The checksum is not there to catch this store writing badly. It is there for
/// what happens to a file afterwards: a disk that filled between the write and
/// the flush, a container image copied while the server was running, a restored
/// backup that took the directory and not the file. None of those is this
/// plugin's mistake and all of them arrive as a request from a client.
///
/// What is deliberately not decided here is what goes in a document. This is a
/// container around a payload of bytes, so one file per shelf and one file for
/// all are the same to it, and the layout decision stays with
/// <see cref="CatalogueDirectory"/> and #87 where the refresh that needs it is.
///
/// The bound worth knowing before this is used for something large: a read
/// holds the whole payload in memory to check it against its own checksum, so
/// the size of a document is bounded by what the server can hold rather than by
/// anything here. What bounds the catalogue is #58.
///
/// One case is a failed write rather than a torn document, and it is named so it
/// is not met as a surprise. Where a Windows server has the document open for
/// reading at the moment the move runs, the move fails and the exception reaches
/// the caller; the previous document stands and the reader carries on with it.
/// Linux replaces the entry under the open handle instead. The property both
/// answer is the same one: nobody reads half a document.
/// </remarks>
public sealed class CatalogueDocumentStore
{
    /// <summary>
    /// What is added to a document's name to make the name of the file a write
    /// is still building.
    /// </summary>
    /// <remarks>
    /// A fixed suffix rather than a drawn one, because <c>no-random</c> refuses
    /// a draw outside the one source of randomness and a temporary file is not
    /// a reason to widen it. The cost is that two writes of one document cannot
    /// use two temporary files, which is why the write below is serialised
    /// rather than left to the file system.
    ///
    /// It is public because a listing of the directory sees this file while a
    /// write is in flight, and a caller that lists documents has to be able to
    /// tell one from a document. <see cref="CatalogueDirectory.ListDocuments"/>
    /// does not filter it today.
    /// </remarks>
    public const string TemporaryNameSuffix = ".writing";

    /// <summary>
    /// The first line of every document, naming the format its header is in.
    /// </summary>
    /// <remarks>
    /// It carries a number so a later format is a different first line rather
    /// than a header that parses differently. A document whose marker is not
    /// this one is refused on read, which is what makes a change of format
    /// visible instead of silent.
    /// </remarks>
    public const string FormatMarker = "discover-catalogue/1";

    private const int ChecksumDigits = 64;

    /// <summary>
    /// Derived rather than written down, so a change to the marker moves the
    /// reader with it instead of leaving a length that was right yesterday.
    /// </summary>
    private static readonly int HeaderLength = FormatMarker.Length + 1 + ChecksumDigits + 1 + 1;

    private readonly CatalogueDirectory _directory;
    private readonly ILogger<CatalogueDocumentStore> _logger;
    private readonly object _writing = new object();

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogueDocumentStore"/>
    /// class.
    /// </summary>
    /// <param name="directory">The directory the catalogue lives in.</param>
    /// <param name="logger">Where a document that cannot be read is reported.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either argument is absent.
    /// </exception>
    public CatalogueDocumentStore(CatalogueDirectory directory, ILogger<CatalogueDocumentStore> logger)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(logger);

        _directory = directory;
        _logger = logger;
    }

    /// <summary>
    /// Writes a document, replacing whatever was there, in a way no reader can
    /// see part of.
    /// </summary>
    /// <param name="documentName">
    /// The document's file name, which <see cref="CatalogueDirectory.DocumentPath"/>
    /// refuses if it would leave the directory.
    /// </param>
    /// <param name="content">
    /// The payload, read to its end. A stream that stops part way through is an
    /// interrupted write: the previous document stands and the failure reaches
    /// the caller.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the content is absent.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the name would put the document outside the catalogue
    /// directory.
    /// </exception>
    /// <remarks>
    /// The lock is what stops two refreshes running at once from writing into
    /// one temporary file, which is a document made of both of them and passing
    /// its own checksum because the checksum was computed over the mixture. It
    /// holds for the whole write rather than for the move, because the
    /// interleaving happens while the bytes are being produced.
    ///
    /// It is a lock inside one process and it says nothing about two servers
    /// sharing a directory. Nothing in this plan puts two servers on one
    /// catalogue, and if something ever does, this is the guard that does not
    /// cover it.
    /// </remarks>
    public void Write(string documentName, Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var documentPath = _directory.DocumentPath(documentName);
        var temporaryPath = documentPath + TemporaryNameSuffix;

        lock (_writing)
        {
            _directory.EnsureExists();

            try
            {
                using (var file = new FileStream(temporaryPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    file.Write(HeaderBytes(new string('0', ChecksumDigits)));
                    var checksum = CopyAndMeasure(content, file);

                    file.Position = 0;
                    file.Write(HeaderBytes(Convert.ToHexString(checksum)));
                    file.Flush(true);
                }

                File.Move(temporaryPath, documentPath, true);
            }
            catch
            {
                Discard(temporaryPath);
                throw;
            }
        }
    }

    /// <summary>
    /// Reads a document, treating one that is not there and one that is not
    /// readable the same way.
    /// </summary>
    /// <param name="documentName">The document's file name.</param>
    /// <returns>
    /// The payload, or nothing where the document is absent or its bytes do not
    /// match the header it carries.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the name would put the document outside the catalogue
    /// directory.
    /// </exception>
    /// <remarks>
    /// A corrupt document answers the way an absent one does, and the reason it
    /// is not an exception is where this is called from: a client asking for a
    /// shelf. A throw there is an error on somebody's screen for a file they
    /// cannot see, where an empty shelf is the same thing the next refresh
    /// fixes. The operator is told instead, in the log, with the path.
    ///
    /// Absence is not logged. A first run on a fresh server reads documents
    /// that are not there yet, and a warning per read would put an error in the
    /// log of every new install.
    /// </remarks>
    public byte[]? Read(string documentName)
    {
        var documentPath = _directory.DocumentPath(documentName);

        if (!File.Exists(documentPath))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(documentPath);

        if (bytes.Length < HeaderLength)
        {
            return Unreadable(documentPath, "it is shorter than the header every document carries");
        }

        var header = Encoding.ASCII.GetString(bytes, 0, HeaderLength);
        var declaredChecksum = header.Substring(FormatMarker.Length + 1, ChecksumDigits);

        if (!string.Equals(header, Header(declaredChecksum), StringComparison.Ordinal))
        {
            return Unreadable(documentPath, "its header is not the one this store writes");
        }

        var payload = bytes.AsSpan(HeaderLength);

        if (!string.Equals(Convert.ToHexString(SHA256.HashData(payload)), declaredChecksum, StringComparison.Ordinal))
        {
            return Unreadable(
                documentPath,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the {payload.Length} bytes after its header are not the ones the header was written for"));
        }

        return payload.ToArray();
    }

    private static byte[] CopyAndMeasure(Stream content, Stream file)
    {
        using var running = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[81920];
        int read;

        while ((read = content.Read(buffer, 0, buffer.Length)) > 0)
        {
            file.Write(buffer, 0, read);
            running.AppendData(buffer, 0, read);
        }

        return running.GetHashAndReset();
    }

    private static string Header(string checksum)
    {
        return FormatMarker + "\n" + checksum + "\n\n";
    }

    private static byte[] HeaderBytes(string checksum)
    {
        return Encoding.ASCII.GetBytes(Header(checksum));
    }

    private static void Discard(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
            // The interrupted write already reached the caller, and the next
            // write to this document creates the file again. Losing that
            // exception here would replace the one that says what went wrong.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private byte[]? Unreadable(string documentPath, string reason)
    {
        _logger.LogWarning(
            "The catalogue document at {DocumentPath} cannot be read and is treated as absent, because {Reason}. The next refresh replaces it.",
            documentPath,
            reason);

        return null;
    }
}
