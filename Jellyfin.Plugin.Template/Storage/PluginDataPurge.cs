using System;
using System.IO;

namespace Jellyfin.Plugin.Template.Storage;

/// <summary>
/// The one operation that removes everything this plugin persisted, over the
/// whole folder the server derived for it rather than over one store inside it.
/// </summary>
/// <remarks>
/// <para>
/// #72's first condition asks for one operation removing everything this plugin
/// persisted, and its body gives the reason: a purge that misses a directory is
/// how a plugin leaves data behind after it has been removed.
/// <see cref="Catalogue.CatalogueDirectory.RemoveEverything"/> is not that
/// operation and says so about itself - it reaches its own
/// <c>FullPath</c> and nothing a caller passes can aim it anywhere else - so it
/// removes the catalogue and leaves whatever else sits beside it.
/// </para>
/// <para>
/// Something does sit beside it. <see cref="Wants.WantListStore"/> owns a second
/// directory under the same folder and has no removal of its own, so before this
/// type the whole of the purge reached one of the two and the list of who asked
/// for what survived it. That list is keyed by the asking user, which is what
/// makes the miss a privacy question rather than an untidy one, and it is the
/// half that survives an uninstall as well: the server's removal takes the
/// directory it unpacked the package into and not the data folder beside it,
/// which is read on #108.
/// </para>
/// <para>
/// THE SUBJECT IS THE FOLDER RATHER THAN A LIST OF STORE NAMES, and that is the
/// whole design. A purge naming <c>catalogue</c> and <c>wants</c> would be a
/// list somebody has to extend, and the failure of forgetting to extend it is
/// exactly the one standing today: the second store arrived and the purge did
/// not move. Everything under the folder the server derived for this plugin
/// belongs to this plugin, because the server derives it from the assembly it
/// loaded, so a store added tomorrow is inside this without anybody remembering.
/// </para>
/// <para>
/// WHAT IT DOES NOT REACH IS THE CONFIGURATION, and that is not an oversight.
/// The base plugin class writes the configuration document under the server's
/// plugin configurations path, which is a different directory and one this
/// plugin may not compose a path into: <c>no-other-plugin-storage</c> refuses
/// the member by name, because composing a path from it is also how a plugin
/// reads a neighbour's files. What an uninstall leaves behind there is named on
/// #108 rather than removed here.
/// </para>
/// <para>
/// What it does not promise is that the folder stays gone, which is the bound
/// <see cref="Catalogue.CatalogueDirectory.RemoveEverything"/> already carries
/// for its own directory. Every writer creates what it needs on the way to a
/// write, so anything running after this brings back the part of the tree it
/// uses. Stopping the writers is the caller's, not this type's, and on a server
/// with a refresh on a schedule gone and staying gone are two claims.
/// </para>
/// </remarks>
public sealed class PluginDataPurge
{
    private readonly string _dataFolderPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDataPurge"/> class.
    /// </summary>
    /// <param name="pluginDataFolderPath">
    /// The folder the server derived for this plugin, which is what the base
    /// plugin class exposes as its data folder path.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the folder is absent, blank, or not fully qualified. The same
    /// refusal the two stores under it already make, for the same reason: a
    /// relative path resolves against whatever directory the server process
    /// happens to be running in, and on an operation that deletes recursively
    /// that is the difference between removing this plugin's folder and removing
    /// a directory of the same name somewhere nobody chose.
    /// </exception>
    public PluginDataPurge(string pluginDataFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDataFolderPath);

        if (!Path.IsPathFullyQualified(pluginDataFolderPath))
        {
            throw new ArgumentException(
                "The plugin's data folder has to be a fully qualified path. A relative one resolves against the server process's working directory, which nothing here chooses, and this operation deletes what it is pointed at.",
                nameof(pluginDataFolderPath));
        }

        _dataFolderPath = pluginDataFolderPath;
    }

    /// <summary>
    /// Gets the folder this operation removes.
    /// </summary>
    public string DataFolderPath => _dataFolderPath;

    /// <summary>
    /// Removes the folder and everything this plugin wrote under it.
    /// </summary>
    /// <remarks>
    /// Absent is done rather than an error, in both directions, which is #72's
    /// fourth condition. A removal reaches a server that was installed and never
    /// configured, and an operator who did not see the first purge finish does it
    /// again. Neither is a case worth putting an error in a log for, and an
    /// uninstall path treating either as a failure would stop halfway through
    /// cleaning up.
    /// </remarks>
    public void RemoveEverything()
    {
        if (!Directory.Exists(_dataFolderPath))
        {
            return;
        }

        Directory.Delete(_dataFolderPath, true);
    }
}
