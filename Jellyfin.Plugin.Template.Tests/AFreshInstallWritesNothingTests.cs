using System.IO;
using System.Linq;
using Jellyfin.Plugin.Template.Catalogue;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A server that installed this plugin and was told nothing else has nothing of
/// this plugin's on its disk.
/// </summary>
/// <remarks>
/// The whole of a start is here rather than one half of it: the plugin is
/// constructed the way the server constructs it, the registrator is called the
/// way the server calls it, and every service the registrator added is resolved,
/// because a registration that writes does it when something asks for it rather
/// than when it is added. What a real server does after that is not claimed; no
/// server is started here.
///
/// The property is about the disk rather than about the calls. What the plugin
/// asks the server for is already counted in CallsTheServerSeesTests, and a
/// count of calls says nothing about a type that reaches the file system
/// directly, which <see cref="CatalogueDocumentStore"/> does.
///
/// This is one half of the first condition on #104. The other half is that no
/// outbound call is made, and it is asserted in
/// <see cref="AFreshInstallHoldsNoWayOutTests"/> rather than here.
///
/// The reason written here when this file landed was that nothing in the plugin
/// could make one. That stopped being true on the commit adding the first
/// adapter, which merged after this file and was not read back against it. What
/// holds the property now is narrower, and the narrow form is the one to keep:
/// the adapter is the only type in this plugin that can reach a host, and
/// nothing constructs it, so a start resolves no service holding a way out.
///
/// The near-miss moved with it. Adding a way out to the plugin is no longer the
/// mistake to watch for, because it has happened. The mistake is a line in
/// <see cref="PluginServiceRegistrator"/> registering the adapter, after which
/// a start builds something able to call and this file's assertion stays green.
/// That line is what the test beside this one refuses, so the mistake is now
/// caught rather than only described.
///
/// This remark said the assertion could not be written, because
/// `no-network-outside-source-adapter` refuses the names of the transport types
/// in every tracked C# file but an adapter's, and a test counting what a start
/// could reach cannot name what it counts. The half of that which is true is
/// still true and is why the assertion counts what it counts: it names
/// <see cref="Jellyfin.Plugin.Template.Sources.IMetadataSource"/>, which that
/// rule does not refuse, rather than a transport type. The seam this remark
/// offered instead, <see cref="ATransportThatRefusesWhatNoTestSetUp"/>, counts
/// what a test handed an adapter, and a start hands it nothing, which is why it
/// is not the one used.
///
/// The issue is named as a number rather than as a link because a link puts a
/// hostname in the plugin's C#, and `source-terms` reads every hostname there as
/// a metadata source owing a terms page. It refused the first spelling of this
/// file for exactly that.
///
/// The folder these assertions read is the one the base plugin class derived
/// from the paths fake, under the temporary directory. Nothing here creates it
/// and nothing here removes it, so a failure names what was found rather than
/// what a clean-up left behind.
/// </remarks>
public class AFreshInstallWritesNothingTests
{
    /// <summary>
    /// Starting with nothing configured leaves the catalogue directory absent
    /// and the plugin's own folder empty.
    /// </summary>
    /// <remarks>
    /// The catalogue directory is the one place this plugin writes, and
    /// <see cref="CatalogueDirectory"/> creates it from a write and from nothing
    /// else. That is a property of one type today. This asserts it of a start,
    /// so the day something is registered that reads the configuration and acts
    /// on it, an eager write shows up here rather than on the disk of a server
    /// whose operator had not finished configuring.
    ///
    /// The near-miss is a constructor. A store built at registration that
    /// created its directory so that a later write would not have to is one
    /// line, is invisible to every other test in this project, and leaves an
    /// empty catalogue on every install that never switched the feature on.
    ///
    /// The assertion is written as absent-or-empty rather than absent, because
    /// what makes the folder appear is a question about the base plugin class
    /// rather than about this plugin, and pinning the answer here would make
    /// this test fail on the day the pinned server package changes its mind for
    /// a reason this test is not about.
    /// </remarks>
    [Fact]
    public void AStartWithNothingConfiguredWritesNothing()
    {
        var plugin = new Plugin(
            new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(),
            new XmlSerializerThatRefusesEveryCall());

        var services = new ServiceCollection();
        new PluginServiceRegistrator().RegisterServices(services, new ServerApplicationHostThatRefusesEveryCall());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        foreach (var descriptor in services.Where(descriptor => !descriptor.ServiceType.IsGenericTypeDefinition))
        {
            _ = provider.GetRequiredService(descriptor.ServiceType);
        }

        var catalogue = new CatalogueDirectory(plugin.DataFolderPath);

        Assert.False(
            Directory.Exists(catalogue.FullPath),
            $"A start with nothing configured created {catalogue.FullPath}.");

        Assert.True(
            !Directory.Exists(plugin.DataFolderPath)
                || !Directory.EnumerateFileSystemEntries(plugin.DataFolderPath).Any(),
            $"A start with nothing configured left something in {plugin.DataFolderPath}.");
    }
}
