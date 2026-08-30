using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Jellyfin.Plugin.Template.Catalogue;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Template.Wants;

/// <summary>
/// Where the operator's want list is kept, so that it is still there after a
/// restart.
/// </summary>
/// <remarks>
/// <para>
/// A directory of its own beside the catalogue's, and that is the decision this
/// type exists to take rather than fall into. #72 throws the catalogue away as
/// one directory rather than as a list of names, so a want list written inside
/// it would be removed by an operation nobody aimed at it. The retention is the
/// second reason and points the same way: #68 caps how long a fetched record may
/// be kept because a source's terms say so, and nothing a user asked for is a
/// fetched record.
/// </para>
/// <para>
/// The name is the plugin's data folder, per
/// <c>docs/decisions/0003</c>, and no other place. What an operator clearing
/// this folder by hand loses is the list of who asked for what; nothing else
/// depends on it and the plugin starts with an empty one.
/// </para>
/// <para>
/// The write goes to a temporary name and is moved into place, so a process that
/// dies mid-write leaves the previous list readable rather than a half file. That
/// is the discipline <see cref="CatalogueDocumentStore"/> uses and the reason is
/// the same.
/// </para>
/// <para>
/// WHAT THIS DOES NOT CARRY, AND THE CATALOGUE'S STORE DOES, IS A CHECKSUM. A
/// list damaged in place after it was written is caught here only where the
/// damage stops it being the JSON this build writes; damage that leaves valid
/// JSON is read back as rows. The catalogue store hashes its payload and would
/// catch both. Copying that discipline into a second store would be a second
/// implementation of it, and the honest repair is one store serving both, which
/// is neither this issue's nor #65's as that issue closed. The residual is a want
/// list that can come back subtly wrong rather than absent, and nothing here
/// detects it.
/// </para>
/// <para>
/// The file names its format on its first line, for the reason #67 gives about
/// the catalogue: a build that reads a list a newer build wrote, field by field,
/// as though the two shapes agreed is the failure a version refuses.
/// </para>
/// </remarks>
public sealed class WantListStore
{
    /// <summary>
    /// The folder this store owns, under the plugin's data folder.
    /// </summary>
    public const string DirectoryName = "wants";

    /// <summary>
    /// The file the list is kept in.
    /// </summary>
    public const string FileName = "wants.json";

    /// <summary>
    /// The suffix a write in flight carries.
    /// </summary>
    public const string TemporaryNameSuffix = ".writing";

    /// <summary>
    /// The family every version of this file's format line starts with.
    /// </summary>
    public const string FormatFamily = "discover-wants/";

    /// <summary>
    /// The version this build writes and is the only one it reads.
    /// </summary>
    public const int FormatVersion = 1;

    private readonly string _directoryPath;
    private readonly string _filePath;
    private readonly ILogger<WantListStore> _logger;
    private readonly object _writing = new object();

    /// <summary>
    /// Initializes a new instance of the <see cref="WantListStore"/> class.
    /// </summary>
    /// <param name="pluginDataFolderPath">The plugin's own data folder, fully qualified.</param>
    /// <param name="logger">Where a list that cannot be read is reported.</param>
    /// <exception cref="ArgumentNullException">Thrown when the logger is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the folder is blank or is not a fully qualified path. A
    /// relative one resolves against the server process's working directory,
    /// which nothing here chooses.
    /// </exception>
    public WantListStore(string pluginDataFolderPath, ILogger<WantListStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDataFolderPath);
        ArgumentNullException.ThrowIfNull(logger);

        if (!Path.IsPathFullyQualified(pluginDataFolderPath))
        {
            throw new ArgumentException(
                "The plugin's data folder has to be a fully qualified path. A relative one resolves against the server process's working directory, which nothing here chooses.",
                nameof(pluginDataFolderPath));
        }

        _directoryPath = Path.Combine(pluginDataFolderPath, DirectoryName);
        _filePath = Path.Combine(_directoryPath, FileName);
        _logger = logger;
    }

    /// <summary>
    /// Gets the folder this store writes into.
    /// </summary>
    public string DirectoryPath => _directoryPath;

    /// <summary>
    /// Gets the file the list is kept in.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Gets the line this build writes at the top of the file.
    /// </summary>
    public static string CurrentMarker => FormatFamily + FormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes the list, replacing whatever was there.
    /// </summary>
    /// <param name="wants">The rows to keep.</param>
    /// <exception cref="ArgumentNullException">Thrown when the list is null.</exception>
    /// <remarks>
    /// An empty list is written rather than the file being removed. A server
    /// where every want has been cleared and a server that has never held one
    /// are different states, and the operator's page says different things about
    /// them.
    /// </remarks>
    public void Write(IReadOnlyList<LocalWant> wants)
    {
        ArgumentNullException.ThrowIfNull(wants);

        var temporaryPath = _filePath + TemporaryNameSuffix;

        lock (_writing)
        {
            Directory.CreateDirectory(_directoryPath);

            try
            {
                using (var file = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    file.Write(Encoding.ASCII.GetBytes(CurrentMarker + "\n"));
                    WantListDocument.Write(file, wants);
                    file.Flush(true);
                }

                File.Move(temporaryPath, _filePath, true);
            }
            catch
            {
                Discard(temporaryPath);
                throw;
            }
        }
    }

    /// <summary>
    /// Reads back what was kept.
    /// </summary>
    /// <returns>
    /// The rows, or an empty list where there is no file or where the one there
    /// is cannot be read.
    /// </returns>
    /// <remarks>
    /// A list that cannot be read is reported and treated as absent rather than
    /// throwing into a server's start-up. What that costs is the wants it held;
    /// what refusing to start would cost is the surface, the catalogue and every
    /// other thing this plugin does, for a file that holds none of them.
    /// </remarks>
    public IReadOnlyList<LocalWant> Read()
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<LocalWant>();
        }

        var bytes = File.ReadAllBytes(_filePath);
        var scanned = Math.Min(bytes.Length, FormatFamily.Length + 11);
        var markerLength = bytes.AsSpan(0, scanned).IndexOf((byte)'\n');

        if (markerLength < 0)
        {
            return Unreadable("it does not begin with the line every want list names its format on");
        }

        var marker = Encoding.ASCII.GetString(bytes, 0, markerLength);

        if (!string.Equals(marker, CurrentMarker, StringComparison.Ordinal))
        {
            return Unreadable(
                FormattableString.Invariant(
                    $"its first line says '{marker}' and this build writes '{CurrentMarker}'. Nothing here migrates one shape onto the other, so it is refused rather than read as though the two agreed."));
        }

        try
        {
            return WantListDocument.Read(bytes.AsSpan(markerLength + 1).ToArray());
        }
        catch (InvalidDataException reason)
        {
            return Unreadable(reason.Message);
        }
    }

    private void Discard(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
            // The interrupted write already reached the caller, and the next
            // write to this list creates the file again. Letting this out would
            // replace the exception that says what actually went wrong.
        }
        catch (UnauthorizedAccessException)
        {
            // The same case, reached instead when the file is read-only or this
            // process may not remove it. The file left behind is truncated by
            // the next write, which opens the same path with FileMode.Create.
        }
    }

    private LocalWant[] Unreadable(string reason)
    {
        _logger.LogWarning(
            "The want list at {FilePath} cannot be read and is treated as empty, because {Reason}. What it held is lost and nothing here recreates it.",
            _filePath,
            reason);

        return Array.Empty<LocalWant>();
    }
}
