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
/// Nothing is registered yet. The type exists first so the first service has a
/// place to go, and so the test that builds this graph is already there when
/// there is a graph to get wrong.
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
    }
}
