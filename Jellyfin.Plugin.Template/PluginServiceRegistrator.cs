using System;
using Jellyfin.Plugin.Template.Randomness;
using Jellyfin.Plugin.Template.Surface;
using Jellyfin.Plugin.Template.Time;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
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

        // Singleton because the surface is one thing the server holds for as
        // long as it runs and asks repeatedly, and because everything it will
        // later read, the catalogue first, is shared rather than per caller. A
        // transient here would build a second surface per resolution and make
        // "the surface" a thing there are several of.
        serviceCollection.AddSingleton<IDiscoverSurface, DiscoverSurface>();

        // The line that makes this plugin exist to a client. The server builds
        // its surfaces by asking the container for every implementation of the
        // interface below, so a plugin that never adds one loads, logs nothing,
        // and appears nowhere. That failure is silent, which is why it is #53
        // and why the registration is asserted rather than assumed.
        //
        // Registered as the server's interface rather than as the adapter,
        // because a registration under the concrete type is one the server's
        // own collection never sees. This is the only line in the plugin that
        // speaks the server's channel vocabulary outside the adapter, and
        // `no-channel-type-outside-surface` excepts this file for it by name.
        serviceCollection.AddSingleton<IChannel, DiscoverSurfaceAdapter>();
    }
}
