using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Template.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
/// <remarks>
/// There is no setting here yet. The settings this plugin needs arrive with the
/// features that read them. What this class carries from its first commit is the
/// schema version, because a configuration written before there is a version
/// rule is a configuration nobody can migrate afterwards.
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// The schema version this build writes, and the only one it is able to read.
    /// </summary>
    /// <remarks>
    /// Raise this when a change to this class cannot be read by the build before
    /// it, and say in CHANGELOG.md what moved.
    /// </remarks>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        SchemaVersion = CurrentSchemaVersion;
    }

    /// <summary>
    /// Gets or sets the schema version of this configuration document.
    /// </summary>
    public int SchemaVersion { get; set; }
}
