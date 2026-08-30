using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Template.Configuration;
using Jellyfin.Plugin.Template.Seam;
using Jellyfin.Plugin.Template.Shelves;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Template;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Discover";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("8227de33-0101-48a3-951d-2bf921709e48");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// The base class writes whatever it is handed. A document declaring a
    /// schema version this build does not know is refused here instead, so it
    /// never reaches disk and is never read back as if its fields meant what
    /// this build thinks they mean.
    /// <para>
    /// The list of users this plugin may not ask for is refused here as well,
    /// which is #98 meeting #105's shape: an entry that is not a user
    /// identifier is a refusal nothing can apply, and it is caught at the save
    /// rather than at the moment a person makes a gesture. What meets the same
    /// bytes when they reached disk another way is <see cref="WhoMayAsk.From"/>,
    /// which fails closed rather than trusting that this ran.
    /// </para>
    /// <para>
    /// The bounds on what reaches the library database are refused here too,
    /// which is #58's third condition: a pair that contradicts itself, or one
    /// the shipped shelves do not fit inside, is refused at the moment it is
    /// saved rather than truncated later at a refresh nobody is watching. The
    /// count it is checked against is the shipped set's own, because every shelf
    /// that ships is on and nothing reads <c>Shelf.Enabled</c> yet, which is
    /// #86's fourth condition.
    /// </para>
    /// <para>
    /// What a dashboard does with the refusal has not been observed. Nothing
    /// here has been run against a server, so this says the save is refused and
    /// says nothing about how the message is drawn, which is #103's page and
    /// #105's own subject.
    /// </para>
    /// </remarks>
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration is PluginConfiguration pluginConfiguration)
        {
            ConfigurationSchema.ThrowIfUnknown(pluginConfiguration);

            WhoMayAsk.ThrowIfAnEntryIsUnreadable(pluginConfiguration);

            pluginConfiguration
                .Bounds()
                .ThrowIfShelvesDoNotFit(
                    ShippedShelves.Bounded(pluginConfiguration.MaximumTitlesPerShelf).Count);
        }

        base.UpdateConfiguration(configuration);
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }
}
