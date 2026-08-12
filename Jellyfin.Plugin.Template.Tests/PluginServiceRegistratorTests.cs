using System;
using System.Linq;
using Jellyfin.Plugin.Template.Surface;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What the one registrator has to be, and what the graph it builds has to do.
/// </summary>
/// <remarks>
/// No server is started here. The registrator is called the way the server
/// calls it, with a host that refuses every member, and the container it fills
/// is built and validated in this process. What a real server does with the
/// result is not claimed.
/// </remarks>
public class PluginServiceRegistratorTests
{
    /// <summary>
    /// Exactly one registrator exists, and the server can build it the way it
    /// actually builds one.
    /// </summary>
    /// <remarks>
    /// The server collects these by interface and calls every one it finds, so
    /// a second one is a second place services can be added from, which is the
    /// thing this type exists to prevent. It then activates the type with no
    /// arguments, so both halves are one property: the search finds one type
    /// and that type comes out of the activation the server performs.
    /// </remarks>
    [Fact]
    public void ExactlyOneRegistratorExistsAndTheServerCanBuildIt()
    {
        var registratorType = typeof(Plugin).Assembly
            .GetTypes()
            .Single(type => typeof(IPluginServiceRegistrator).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

        var registrator = Activator.CreateInstance(registratorType);

        Assert.IsType<PluginServiceRegistrator>(registrator);
    }

    /// <summary>
    /// Every service the registrator adds can be constructed from what is in
    /// the container, with no server behind it.
    /// </summary>
    /// <remarks>
    /// Validation is on for both scopes and construction, so a registration
    /// asking for something nobody registered fails here rather than on a
    /// user's server at the moment the feature is first used.
    /// </remarks>
    [Fact]
    public void EveryServiceItAddsCanBeConstructed()
    {
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, new ServerApplicationHostThatRefusesEveryCall());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        foreach (var descriptor in services.Where(descriptor => !descriptor.ServiceType.IsGenericTypeDefinition))
        {
            Assert.NotNull(provider.GetRequiredService(descriptor.ServiceType));
        }
    }

    /// <summary>
    /// Exactly one surface is offered to the server, under the interface the
    /// server collects surfaces by.
    /// </summary>
    /// <remarks>
    /// The server asks its container for every implementation it finds, so two
    /// registrations are two libraries in front of every user and a
    /// registration under the adapter's own type is none at all. Both are
    /// silent: nothing fails, nothing is logged, and the first report comes
    /// from somebody looking at a screen.
    ///
    /// The interface is derived from the adapter rather than named here. That
    /// keeps this file on the plugin's side of
    /// `no-channel-type-outside-surface`, and it makes the assertion the
    /// stronger one: what is counted is whatever the adapter implements for the
    /// server, so a change of interface is still counted rather than leaving a
    /// test that passes while looking for a type nothing implements any more.
    /// </remarks>
    [Fact]
    public void ExactlyOneSurfaceIsOfferedToTheServer()
    {
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, new ServerApplicationHostThatRefusesEveryCall());

        var serverInterface = typeof(DiscoverSurfaceAdapter)
            .GetInterfaces()
            .Single(contract => contract.Assembly != typeof(Plugin).Assembly);

        var offered = services.Where(descriptor => descriptor.ServiceType == serverInterface).ToArray();

        Assert.Single(offered);
        Assert.Equal(typeof(DiscoverSurfaceAdapter), offered[0].ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, offered[0].Lifetime);
    }

    /// <summary>
    /// Registering reaches nothing on the server host.
    /// </summary>
    /// <remarks>
    /// The host is handed over while the server is still building its
    /// container, so a registration that reads something off it is reading a
    /// half-built server. The fake throws on every member, so the only way this
    /// passes is by not touching it.
    /// </remarks>
    [Fact]
    public void RegisteringReachesNothingOnTheHost()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(
            () => new PluginServiceRegistrator().RegisterServices(services, new ServerApplicationHostThatRefusesEveryCall()));

        Assert.Null(exception);
    }
}
