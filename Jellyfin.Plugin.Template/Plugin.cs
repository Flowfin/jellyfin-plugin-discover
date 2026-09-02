using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Template.Configuration;
using Jellyfin.Plugin.Template.Seam;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Storage;
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
    /// that ships is on and no setting moves <c>Shelf.Enabled</c>, which is
    /// #86's fourth condition. This said nothing read that field yet, and the
    /// refresh has read it since #87 landed on 2026-08-30. The count is
    /// unmoved, because what it rests on is the other half: no configured
    /// value turns a shipped shelf off, so the set a save can be asked to fit
    /// inside the bounds is the whole of it.
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
    /// <remarks>
    /// <para>
    /// The server invites a plugin to clean up before it removes it, and #108's
    /// first condition established that from the server's own source on both
    /// targeted lines rather than from an assumption. This override is that
    /// invitation accepted, and it accepts it by calling the one operation
    /// #72 built rather than by removing anything itself: everything under the
    /// folder the server derived for this plugin belongs to this plugin, so a
    /// store added tomorrow goes with it and nobody has to remember to extend a
    /// list here.
    /// </para>
    /// <para>
    /// WITHOUT THIS THE SERVER TAKES A DIFFERENT DIRECTORY AND LEAVES THE DATA
    /// STANDING. The uninstall deletes the folder it unpacked the package into,
    /// which is named from the manifest name with the version appended, and the
    /// data folder is named from the loaded assembly's file name. They sit side
    /// by side under the plugins path and neither is inside the other, which
    /// <c>docs/installing.md</c> reads out of the server's source. So the
    /// catalogue and the want list survived a removal entirely, and this hook is
    /// the only thing on that route that takes them.
    /// </para>
    /// <para>
    /// IT IS ONE ROUTE RATHER THAN A GUARANTEE, and the reading on #108 says
    /// which one. An operator who deletes the plugin folder by hand runs none of
    /// this, and this hook cannot be the place a promise about removal is made.
    /// Two other things it does not reach are named on that issue rather than
    /// silently absent: the configuration document, which the base class writes
    /// under the server's plugin configurations path and which
    /// <c>no-other-plugin-storage</c> refuses this plugin to compose a path
    /// into, and the rows this plugin puts in the library database, which the
    /// server removes on its own schedule and nothing here touches.
    /// </para>
    /// <para>
    /// A failure is not swallowed here. The purge treats an absent folder as
    /// done in both directions, which is the case a fresh install and a second
    /// removal both are, so what is left to throw is a folder that is there and
    /// cannot be taken. The server calls this before it removes anything, so an
    /// exception reaching it is the operator learning that data was left behind,
    /// which is the honest outcome and the one <see cref="PluginDataPurge"/>'s
    /// own remark argues for.
    /// </para>
    /// </remarks>
    public override void OnUninstalling()
    {
        new PluginDataPurge(DataFolderPath).RemoveEverything();
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
