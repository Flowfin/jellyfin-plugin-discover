using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Randomness;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Seam;
using Jellyfin.Plugin.Template.Server;
using Jellyfin.Plugin.Template.Surface;
using Jellyfin.Plugin.Template.Time;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        // names the server's channel vocabulary outside the surface adapter,
        // and `no-channel-type-outside-surface` excepts this file for it by
        // name. It excepts the file rather than the line, so the registration
        // below names a second server type under the same exception, and both
        // are here because a plugin declares what it offers and what it needs
        // in one place.
        serviceCollection.AddSingleton<IChannel, DiscoverSurfaceAdapter>();

        // Singleton because it holds the receivers the container gave it, and
        // those are fixed for as long as the server runs: the container is built
        // once out of every plugin's registrations, so a transient here would
        // rebuild the same list per gesture and suggest that the set can change
        // between two of them.
        //
        // Nothing registers an IWantReceiver here, and that is the point rather
        // than an omission. A sibling plugin registers one in its own
        // registrator; this plugin asks the container for whatever is there and
        // is complete when the answer is nothing, which is #95.
        //
        // Built by hand rather than by the container's own constructor choice,
        // for the one argument the container cannot supply. Who may ask is
        // #98's list and it is a configured value an operator changes while the
        // server runs, so what is handed over is a read of it rather than an
        // answer: a singleton holding an answer would go on refusing whoever
        // was listed at the moment the container was built. Plugin.Instance is
        // what the run has to go to for a configuration, exactly as
        // DiscoverRefreshTask does, and a null one refuses every want rather
        // than passing them all on.
        serviceCollection.AddSingleton(provider => new WantHandover(
            provider.GetRequiredService<IEnumerable<IWantReceiver>>(),
            provider.GetRequiredService<ILogger<WantHandover>>(),
            WantHandover.DefaultBound,
            () => WhoMayAsk.From(Plugin.Instance?.Configuration)));

        // What the refresh asks about a title before it puts it on a shelf, per
        // #89. Registered under this plugin's own interface rather than under
        // the server's, because the server collects nothing by it: it is this
        // plugin asking a question, not this plugin offering something.
        //
        // Singleton because it holds nothing but the library the container gave
        // it and answers the same way for every caller, so a second instance
        // would cost an allocation and buy no isolation.
        //
        // The library it wraps is the server's own and is resolved by the
        // container. A server that could not supply one is a server this plugin
        // could not have loaded into, which is why the resolution is required
        // rather than optional: a silent null here would be a refresh that
        // filtered nothing and looked exactly like one that found nothing to
        // filter.
        serviceCollection.AddSingleton<IServerLibrary>(provider =>
            new ServerLibraryAdapter(provider.GetRequiredService<ILibraryManager>()));

        // The refresh, under the interface the server's scheduler collects tasks
        // by, so an operator sees it in the dashboard beside every other one.
        // Registered as the server's interface rather than as the class for the
        // same reason the surface above is: a registration under the concrete
        // type is one the server's own collection never sees.
        //
        // Singleton because the server holds a task for as long as it runs and
        // because the task is what refuses a second run while one is going. Two
        // instances would have a gate each, and two gates do not exclude each
        // other, which is #87's third condition lost to a lifetime.
        //
        // What it will ask is whatever implements IMetadataSource in this
        // container, and nothing registers one, so a start with nothing
        // configured builds a task that asks nobody. That is the state
        // AFreshInstallHoldsNoWayOutTests asserts from the other side.
        serviceCollection.AddSingleton<IScheduledTask, DiscoverRefreshTask>();

        // The one way out of this server, named. #45's second condition asks
        // that every outbound call go through one injected handler, and the
        // reading taken on 2026-09-04 is that this is one injection POINT
        // rather than one handler instance: a single named client, whose
        // primary handler is the one thing a test replaces.
        //
        // An unnamed client could not be it. The factory hands that one to
        // every caller in the server, so configuring its handler would
        // configure everybody's, and configuring a client under this plugin's
        // own name while the adapter asked for the unnamed one would configure
        // a client nothing uses. The name is one constant both sides read
        // rather than a string typed twice, and the argument for that is at
        // TmdbHttpClient: a factory answers a name nobody configured with a
        // default client rather than refusing, so two spellings drifting apart
        // is silent in exactly the direction that matters.
        //
        // The primary handler is whatever the container holds, and where it
        // holds none it is a fresh one of exactly the type the factory would
        // have made unasked. Nothing is configured on it: no callback, no
        // validation switch, no proxy and no connection limit, so a real
        // server, which registers no handler, gets the runtime's own defaults
        // and its certificate verification untouched. Constructing it here
        // rather than leaving the line off is what the non-deprecated shape of
        // this call requires, and it is the reason the sentence above is about
        // what is NOT set rather than about the line not existing.
        //
        // What registers one is a test. That is the whole of what the second
        // condition asks for: a substitute reached without a trust store, an
        // environment variable or a global verification switch, and no test
        // needing a real endpoint.
        //
        // This registers no source, so a fresh install still holds no way out.
        // A configured client name that nothing asks for makes no call, which is
        // what AFreshInstallHoldsNoWayOutTests counts one level up.
        //
        // The three types are named in full rather than through a using, so
        // that this block adds no line above itself. Several pages under docs/
        // and the README quote lines of this file by number, and a using added
        // at the top moves every one of those quotations for a change that is
        // about the bottom of the file.
        serviceCollection
            .AddHttpClient(Sources.TmdbHttpClient.Name)
            .ConfigurePrimaryHttpMessageHandler(provider =>
                provider.GetService<System.Net.Http.HttpMessageHandler>() ?? new System.Net.Http.HttpClientHandler());
    }
}
