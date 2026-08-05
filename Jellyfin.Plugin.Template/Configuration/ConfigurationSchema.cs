using System;

namespace Jellyfin.Plugin.Template.Configuration;

/// <summary>
/// Decides whether a configuration document is one this build can read.
/// </summary>
/// <remarks>
/// Separate from the configuration class so that every route which takes a
/// configuration from outside the plugin, the save from the dashboard today and
/// the read from disk in later work, refuses on the same rule rather than each
/// growing its own.
/// </remarks>
public static class ConfigurationSchema
{
    /// <summary>
    /// Refuses a configuration whose schema version this build does not know.
    /// </summary>
    /// <param name="configuration">The configuration to judge.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <exception cref="UnknownConfigurationSchemaException">
    /// Thrown when the document declares any version other than
    /// <see cref="PluginConfiguration.CurrentSchemaVersion"/>. An older version is
    /// refused rather than migrated because there is no earlier version to
    /// migrate from yet.
    /// </exception>
    public static void ThrowIfUnknown(PluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.SchemaVersion != PluginConfiguration.CurrentSchemaVersion)
        {
            throw new UnknownConfigurationSchemaException(configuration.SchemaVersion);
        }
    }
}
