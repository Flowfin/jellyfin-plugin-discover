using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Configuration;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
/// A start is no longer the whole of what an unconfigured install does. #87
/// registered a scheduled task, so the server runs something of this plugin's
/// once a day whether or not anybody configured it, and a run is the one route
/// in this plugin that reaches a write at all. The second assertion below is
/// that route, driven through the same seam <see cref="DiscoverRefreshTaskTests"/>
/// uses rather than through the plugin instance, because that instance is a
/// static every test in this suite shares.
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
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _now =
        new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

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

        // The server registers the logging abstractions before it calls a
        // plugin's registrator, so a container built without them is a poorer
        // model of the server than of this plugin. Named here since #95, which
        // added the first registration that takes one.
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // The library is there before a plugin's registrator runs for the same
        // reason, and it is named here since #89. It refuses every member: what
        // this asserts is that the graph can be built, and a construction that
        // asked the server anything while the container was still being built
        // would be reading a half-built server.
        services.AddSingleton(ServerLibraryAdapterStandIn.RefusingEveryCall());

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

    /// <summary>
    /// The scheduled run a fresh install gets for free writes nothing either.
    /// </summary>
    /// <remarks>
    /// The other half of the same property, on the route that arrived after the
    /// assertion above was written. A server collects this plugin's task and
    /// runs it on its own schedule with nothing configured, so "an install
    /// nobody touched" now includes a run rather than only a start, and a run is
    /// where every write in this plugin is reached from.
    ///
    /// What holds it green is that the catalogue directory is created by a write
    /// and by nothing else, and that a run with no source registered asks
    /// nobody, writes no document and reads a listing from a directory that is
    /// not there. The shelves are the shipped set at the default bound, which is
    /// what a server with no configuration would hand the task.
    ///
    /// The near-miss is the constructor named in the remark above, one route
    /// later: a store that created its directory when it was built so a later
    /// write would not have to. The assertion above cannot see it, because a
    /// start builds no store; this one reddens.
    ///
    /// The run is driven through <see cref="DiscoverRefreshTask.Over"/> rather
    /// than through a task the container built, for the reason
    /// <see cref="DiscoverRefreshTaskTests"/> already gives: a run composed from
    /// the plugin instance reads a static this whole suite shares, so it would
    /// pass or fail on which test was running beside it.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ARunWithNothingConfiguredWritesNothing()
    {
        var folder = Path.Combine(Path.GetTempPath(), TestFolders, "fresh-install-run");

        Remove(folder);

        try
        {
            await DiscoverRefreshTask.Over(
                new CatalogueRefresh(
                    Array.Empty<IMetadataSource>(),
                    new CatalogueDocumentStore(
                        new CatalogueDirectory(folder),
                        new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>()),
                    null,
                    new ClockATestAdvances(_now),
                    new PauseATestWatches(),
                    new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>()),
                ShippedShelves.Bounded(CatalogueBounds.DefaultTitlesPerShelf),
                new LoggerThatRecordsWhatIsWritten<DiscoverRefreshTask>())
                .ExecuteAsync(new Progress<double>(), CancellationToken.None)
                .ConfigureAwait(true);

            var catalogue = new CatalogueDirectory(folder);

            Assert.False(
                Directory.Exists(catalogue.FullPath),
                $"A run with nothing configured created {catalogue.FullPath}.");

            Assert.True(
                !Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any(),
                $"A run with nothing configured left something in {folder}.");
        }
        finally
        {
            Remove(folder);
        }
    }

    private static void Remove(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
