using System.Reflection;
using Jellyfin.Plugin.Template.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What the plugin asks of the server, counted rather than described.
/// </summary>
/// <remarks>
/// Every assertion here is about a whole run: the calls that happened, in order,
/// and nothing else. A test that only checked the outcome would pass a change
/// that reached the server twice for one answer, and a server pays that cost for
/// every plugin it loads.
///
/// The sequences below are quoted from a run rather than reasoned about. Most of
/// each one belongs to the base plugin class, so what it does is a fact about the
/// server package this tree pins rather than a thing this plugin decides, and the
/// day the pin moves is the day a reader wants to be told the shape changed.
/// </remarks>
public class CallsTheServerSeesTests
{
    /// <summary>
    /// Gets the document the base class writes the configuration into, derived
    /// rather than spelled out, because it is the assembly's own name with a
    /// suffix and #14 renames the assembly.
    /// </summary>
    /// <remarks>
    /// Appended rather than produced by <c>Path.ChangeExtension</c>. That method
    /// replaces everything after the last dot, and this assembly's name is three
    /// dotted parts, so it would name a document that does not exist and the test
    /// would be asserting against its own mistake.
    /// </remarks>
    private static string ConfigurationFileName =>
        typeof(Plugin).GetTypeInfo().Assembly.GetName().Name + ".xml";

    /// <summary>
    /// Registration asks the host for nothing.
    /// </summary>
    /// <remarks>
    /// The server is still building its container while it calls the registrator,
    /// so a registration that reads the host is reading something not yet
    /// answerable. The existing test asserts the graph builds; this one asserts
    /// the host was never consulted while it was built, which is the same rule
    /// stated where a future registration would break it rather than where it
    /// happens to survive.
    /// </remarks>
    [Fact]
    public void RegistrationAsksTheHostForNothing()
    {
        var log = new CallLog();
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, new ServerApplicationHostThatRefusesEveryCall(log));

        Assert.Empty(log.Calls);
    }

    /// <summary>
    /// Constructing the plugin reads one directory and no document.
    /// </summary>
    /// <remarks>
    /// A constructor that read the document would put a disk read on the server's
    /// start-up path for every plugin, and would make this plugin's own tests
    /// depend on what an earlier run left behind. The serialiser here refuses
    /// every call, so that half is held twice: by the sequence naming no call to
    /// it, and by the absence of the throw a call would have raised.
    ///
    /// One directory rather than the two a reader might expect. The configuration
    /// directory is not read until something writes, which is visible in the two
    /// tests below and was not visible at all before the fakes recorded.
    /// </remarks>
    [Fact]
    public void ConstructingThePluginReadsOneDirectoryAndNoDocument()
    {
        var log = new CallLog();

        _ = new Plugin(
            new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(log),
            new XmlSerializerThatRefusesEveryCall(log));

        string[] expected = ["IApplicationPaths.PluginsPath"];

        Assert.Equal(expected, log.Calls);
    }

    /// <summary>
    /// Updating the configuration writes it once, and the write is the last thing
    /// that happens.
    /// </summary>
    /// <remarks>
    /// This is the assertion the issue asks for. A change that writes twice adds
    /// a fifth entry and fails here, where a test asserting only that the
    /// configuration was saved would pass and cost a server two writes for every
    /// setting a user changes.
    ///
    /// The configuration is compared by reference. What survives a round trip
    /// through XML is a different question, and ConfigurationSchemaTests is where
    /// a real serialiser answers it; this fake keeps the object so that answering
    /// one question does not quietly depend on the other.
    /// </remarks>
    [Fact]
    public void UpdatingTheConfigurationWritesItExactlyOnce()
    {
        var log = new CallLog();
        var serialiser = new XmlSerializerThatRecordsWhatIsWritten(log);
        var plugin = new Plugin(
            new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(log),
            serialiser);
        var configuration = new PluginConfiguration();

        plugin.UpdateConfiguration(configuration);

        string[] expected =
        [
            "IApplicationPaths.PluginsPath",
            "IApplicationPaths.PluginConfigurationsPath",
            "IApplicationPaths.PluginConfigurationsPath",
            $"IXmlSerializer.SerializeToFile({ConfigurationFileName})",
        ];

        Assert.Equal(expected, log.Calls);
        Assert.Same(configuration, serialiser.LastWritten);
    }

    /// <summary>
    /// A configuration this build does not know is refused before anything is
    /// written.
    /// </summary>
    /// <remarks>
    /// The refusal itself is already covered. What is asserted here is the part a
    /// test of the exception cannot see: that the sequence stops where the
    /// construction left it, so the document never reached disk and a later read
    /// will not find a version this build cannot interpret sitting where its own
    /// configuration belongs.
    /// </remarks>
    [Fact]
    public void AConfigurationThisBuildDoesNotKnowIsNeverWritten()
    {
        var log = new CallLog();
        var serialiser = new XmlSerializerThatRecordsWhatIsWritten(log);
        var plugin = new Plugin(
            new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(log),
            serialiser);

        Assert.Throws<UnknownConfigurationSchemaException>(
            () => plugin.UpdateConfiguration(new PluginConfiguration { SchemaVersion = 99 }));

        string[] expected = ["IApplicationPaths.PluginsPath"];

        Assert.Equal(expected, log.Calls);
        Assert.Null(serialiser.LastWritten);
    }
}
