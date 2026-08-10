using System;
using System.Collections.Generic;
using System.IO;

namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// The one directory the catalogue lives in, and the only place this plugin
/// writes.
/// </summary>
/// <remarks>
/// The server offers a plugin four paths of its own and gives it a fifth
/// without being asked: a folder derived from the plugin's assembly file name
/// under the plugins path, which the base plugin class computes in its
/// constructor and hands back as
/// <c>MediaBrowser.Common.Plugins.BasePlugin.DataFolderPath</c>. That fifth one
/// is what this type builds on, and the choice is argued in
/// <c>docs/decisions/0003-the-catalogue-lives-in-the-plugins-own-data-folder.md</c>
/// rather than here.
///
/// Two properties come out of that choice rather than out of a name anybody
/// picked. The directory sits under a folder whose name the server derived from
/// the assembly it loaded, so a collision with another plugin's data needs
/// another plugin shipping an assembly with this plugin's file name, which is a
/// collision that would already have broken the install. And an operator who
/// clears the server's cache path does not take the catalogue with it, which
/// matters because the server already writes what a channel returned into that
/// path on its own.
///
/// The path this type is given is not re-derived here. The base class owns the
/// derivation, including the branch that appends the version to the folder name
/// when the unsuffixed one does not exist, and a second copy of that rule in
/// this file would be a copy that drifts. What that branch costs an upgrade is
/// #107's, and it is named in the decision note so it is not discovered on a
/// user's server.
///
/// What is deliberately not here is the layout inside the directory. One file
/// per shelf against one file for all is decided against the concurrency the
/// refresh in #87 needs, and #87 does not exist, so choosing now would be
/// choosing by preference. This type is where that decision lands when it is
/// taken, and until then it answers where things go rather than what goes
/// there.
/// </remarks>
public sealed class CatalogueDirectory
{
    /// <summary>
    /// The name of the directory, under the folder the server derived for this
    /// plugin.
    /// </summary>
    /// <remarks>
    /// A directory rather than files beside whatever else the plugin comes to
    /// keep in its folder, so that throwing the catalogue away on demand and on
    /// removal, which is #72, is a directory to remove rather than a list of
    /// names to keep in step with the writers.
    /// </remarks>
    public const string Name = "catalogue";

    private readonly string _fullPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogueDirectory"/> class.
    /// </summary>
    /// <param name="pluginDataFolderPath">
    /// The folder the server derived for this plugin, which is what the base
    /// plugin class exposes as its data folder path.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the folder is absent, blank, or not fully qualified. A
    /// relative path here would resolve against whatever directory the server
    /// process happens to be running in, which is a different directory on a
    /// container than on a service and is nobody's decision.
    /// </exception>
    public CatalogueDirectory(string pluginDataFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDataFolderPath);

        if (!Path.IsPathFullyQualified(pluginDataFolderPath))
        {
            throw new ArgumentException(
                "The plugin's data folder has to be a fully qualified path. A relative one resolves against the server process's working directory, which nothing here chooses.",
                nameof(pluginDataFolderPath));
        }

        _fullPath = Path.Combine(pluginDataFolderPath, Name);
    }

    /// <summary>
    /// Gets the full path of the directory the catalogue lives in.
    /// </summary>
    /// <remarks>
    /// Answering this does not touch the disk and does not create anything. A
    /// fresh server has no such directory and that is the ordinary state rather
    /// than an error, so the path exists as an answer long before anything is
    /// written under it.
    /// </remarks>
    public string FullPath => _fullPath;

    /// <summary>
    /// Lists the documents the catalogue directory holds, by file name.
    /// </summary>
    /// <returns>
    /// The file names in the directory, ordered so two runs on one tree answer
    /// the same way, or nothing at all when the directory is not there.
    /// </returns>
    /// <remarks>
    /// A first run on a fresh server is the common case, and a store treating
    /// it as a failure puts an error in the log of every new install. So an
    /// absent directory answers with nothing rather than throwing, and reading
    /// never creates: the directory appears when something is written, which is
    /// <see cref="EnsureExists"/> and nowhere else.
    ///
    /// The ordering is by name and ordinal, which is a property of this answer
    /// rather than of the file system. Directory enumeration order is not
    /// specified, so a caller comparing two listings would otherwise be
    /// comparing the order a file system happened to return.
    /// </remarks>
    public IReadOnlyList<string> ListDocuments()
    {
        if (!Directory.Exists(_fullPath))
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        foreach (var path in Directory.EnumerateFiles(_fullPath))
        {
            names.Add(Path.GetFileName(path));
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Where one document goes, refusing a name that would put it anywhere but
    /// in this directory.
    /// </summary>
    /// <param name="documentName">The file name, and only a file name.</param>
    /// <returns>The full path that name resolves to.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the name is blank, carries a directory separator, is rooted,
    /// or is one of the relative names that walk out of the directory.
    /// </exception>
    /// <remarks>
    /// The refusal is here rather than at each caller because a name that
    /// escapes this directory is the one way the location decision stops being
    /// true, and it stops being true silently: a path built by joining a name
    /// nobody checked lands somewhere else and still writes successfully. A
    /// name arriving from a shelf definition an operator typed is the case this
    /// exists for, and #105 refuses such a definition on save rather than
    /// leaving this as the only guard.
    /// </remarks>
    public string DocumentPath(string documentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        if (!string.Equals(Path.GetFileName(documentName), documentName, StringComparison.Ordinal)
            || Path.IsPathRooted(documentName)
            || string.Equals(documentName, ".", StringComparison.Ordinal)
            || string.Equals(documentName, "..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A catalogue document is named by a file name and nothing else. A name carrying a separator, a root or a relative step would put the document outside the one directory this plugin writes to.",
                nameof(documentName));
        }

        return Path.Combine(_fullPath, documentName);
    }

    /// <summary>
    /// Creates the directory if it is not there.
    /// </summary>
    /// <remarks>
    /// The only thing in this type that creates anything, and it is called by a
    /// write rather than by a read or by construction. A store that created its
    /// directory when it was built would leave an empty directory on every
    /// server that installed the plugin and never configured it, which is a
    /// trace of a feature nobody switched on.
    /// </remarks>
    public void EnsureExists()
    {
        Directory.CreateDirectory(_fullPath);
    }
}
