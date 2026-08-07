using System;
using Jellyfin.Plugin.Template.Randomness;
using Jellyfin.Plugin.Template.Time;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Template;

/// <summary>
/// The one place this plugin adds services to the server's container.
/// </summary>
/// <remarks>
/// The server finds one of these per plugin and calls it once, while it is
/// building its container. Everything this plugin later offers the server
/// arrives from here, including the surfaces the server collects by interface,
/// so a feature that reaches for a static instance of its own instead is a
/// lifetime nobody chose.
///
/// Every registration added here states its lifetime in the call that adds it,
/// and a singleton carries a comment saying why it is one. A server runs for
/// months, so a singleton is a decision about shared state rather than a
/// default to fall into.
/// </remarks>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        // Singleton because it holds nothing and answers the same way for every
        // caller, so a second instance would cost an allocation and buy no
        // isolation. It is also what makes the substitution in a test total: one
        // registration replaced is every reader of the clock replaced.
        serviceCollection.AddSingleton<IClock, SystemClock>();

        // Singleton for the same reason, and for one more. The shared generator
        // behind it is process-wide already, so a scoped registration would
        // suggest an isolation that does not exist.
        serviceCollection.AddSingleton<IRandomSource, SystemRandomSource>();
    }
}
