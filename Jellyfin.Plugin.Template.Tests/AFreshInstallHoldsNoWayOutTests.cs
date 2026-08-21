using System;
using System.Linq;
using Jellyfin.Plugin.Template.Sources;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A server that installed this plugin and was told nothing else holds nothing
/// able to call out.
/// </summary>
/// <remarks>
/// This is the outbound half of the first condition on #104, and it is narrower
/// than that condition's words. It counts no calls. It asks what a start builds
/// and refuses a start that builds anything behind
/// <see cref="IMetadataSource"/>, which is the interface every way out of this
/// plugin is behind.
///
/// Why not the words the condition uses. Counting calls on a fake source after a
/// start is a count of zero over something a start never wired, which is a
/// number about the tree rather than about the property. A test in this project
/// also cannot name the transport types, because
/// `no-network-outside-source-adapter` refuses them in every tracked C# file but
/// an adapter's, and that is why the assertion was left unwritten when
/// <see cref="AFreshInstallWritesNothingTests"/> landed. The interface is a name
/// a test here may use and every way out is behind it, so that is what this
/// counts.
///
/// The near-miss is one line in <see cref="PluginServiceRegistrator"/>
/// registering the adapter. After it a start builds something able to call, and
/// the disk assertion beside it stays green.
///
/// The day a source is registered on purpose this goes red, and that is the
/// moment rather than an accident. From then on a fresh install is held silent
/// by a configuration carrying no key rather than by nothing being registered,
/// and this assertion has to be replaced by one that says so. The failure
/// message carries that rather than leaving somebody to work it out.
///
/// Its bound is the interface. A way out that is not behind it is invisible
/// here, and what keeps that narrow is the rule named above rather than anything
/// in this file.
/// </remarks>
public class AFreshInstallHoldsNoWayOutTests
{
    /// <summary>
    /// A start with nothing configured offers no metadata source.
    /// </summary>
    [Fact]
    public void AStartWithNothingConfiguredOffersNoSource()
    {
        var services = new ServiceCollection();
        new PluginServiceRegistrator().RegisterServices(services, new ServerApplicationHostThatRefusesEveryCall());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var offered = services
            .Where(descriptor => !descriptor.ServiceType.IsGenericTypeDefinition)
            .Select(descriptor => provider.GetRequiredService(descriptor.ServiceType))
            .Where(service => service is IMetadataSource)
            .Select(service => service.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offered.Length == 0,
            $"A start with nothing configured offers {string.Join(", ", offered)}, so it builds something able to "
            + "reach a third party before an operator has configured anything. If that registration is wanted, what "
            + "holds a fresh install silent is now the configuration rather than the absence of a source, and this "
            + "test is replaced by one asserting that a start with no key configured asks nothing: see #104.");
    }
}
