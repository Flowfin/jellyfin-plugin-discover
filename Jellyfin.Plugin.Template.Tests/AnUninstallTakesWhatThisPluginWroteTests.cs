using System.IO;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The moment the server offers a plugin before it removes it is taken, and what
/// it takes is everything under the folder the server derived for this plugin.
/// </summary>
/// <remarks>
/// <para>
/// #108's second condition asks that whatever cleanup can be triggered from
/// inside the plugin is triggered, reusing the purge from #72. The purge is a
/// type with its own tests; what is asserted here is the other half, that the
/// hook the server calls actually reaches it. Those are two properties, and a
/// tested purge nothing calls leaves a server exactly as dirty as no purge at
/// all.
/// </para>
/// <para>
/// THE NEAR-MISS IS THE OVERRIDE ITSELF, which is one method a change can drop
/// or rename without any other test in this project moving: the purge keeps its
/// own suite green, every start test keeps passing, and the only thing that has
/// changed is that an uninstall leaves the catalogue and the want list behind.
/// Removing the override was watched reddening the first two tests here and
/// nothing else.
/// </para>
/// <para>
/// The paths fake is handed a root of its own rather than the shared one. The
/// data folder is derived from the loaded assembly's file name, so every test
/// constructing a plugin derives the same folder under the shared root, and this
/// is the only one that writes into it and removes it. xunit runs test classes in
/// parallel, so sharing it would put these writes inside the window in which
/// <see cref="AFreshInstallWritesNothingTests"/> asserts that folder is
/// absent-or-empty.
/// </para>
/// <para>
/// Nothing here starts a server or installs anything. The hook is called
/// directly, which is what the server's uninstall does with it, and no claim is
/// made about the rest of that route: which parts of a removal reach this plugin
/// at all is read from the server's source on #108 rather than asserted here.
/// </para>
/// </remarks>
public class AnUninstallTakesWhatThisPluginWroteTests
{
    /// <summary>
    /// Everything this plugin wrote under its data folder is gone after the
    /// server has told it that it is being removed.
    /// </summary>
    /// <remarks>
    /// Two stores are written rather than one, and neither is named by the code
    /// under test. The purge's subject is the folder, so a store added later is
    /// covered without this test being extended; writing two here is what makes
    /// a repair that reached one of them fail, which is the repair the tree
    /// already had to make once.
    /// </remarks>
    [Fact]
    public void AnUninstallRemovesEverythingUnderTheDataFolder()
    {
        var root = ARootOfItsOwn("removes-everything");

        try
        {
            var plugin = new Plugin(
                new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(new CallLog(), root),
                new XmlSerializerThatRefusesEveryCall());

            var folder = plugin.DataFolderPath;

            Directory.CreateDirectory(Path.Combine(folder, "catalogue"));
            Directory.CreateDirectory(Path.Combine(folder, "wants"));
            File.WriteAllText(Path.Combine(folder, "catalogue", "shelves"), "the shelves as they stood");
            File.WriteAllText(Path.Combine(folder, "wants", "wants.json"), "who asked for what");

            plugin.OnUninstalling();

            Assert.False(
                Directory.Exists(folder),
                $"The plugin's data folder is still at {folder} after the server said it was being uninstalled.");
        }
        finally
        {
            Remove(root);
        }
    }

    /// <summary>
    /// The configuration document is not removed, which is deliberate and is one
    /// of the things #108's fifth condition asks to be named rather than
    /// discovered.
    /// </summary>
    /// <remarks>
    /// The document lives under the server's plugin configurations path, which is
    /// a different directory and one <c>no-other-plugin-storage</c> refuses this
    /// plugin to compose a path into at all. So this asserts a boundary rather
    /// than a feature: the file stands here because nothing in this plugin may
    /// reach for it, and a change that made the purge take the whole root would
    /// pass every other test in this file.
    /// <para>
    /// The directory is composed from the root this test handed the fake rather
    /// than read back off the fake, because that rule refuses the member's name
    /// in this file as readily as in the plugin's. The fake composes it under the
    /// same root, which is where the two agree, and the fake is the one file the
    /// rule excepts.
    /// </para>
    /// </remarks>
    [Fact]
    public void AnUninstallLeavesTheConfigurationDocumentWhereTheServerPutIt()
    {
        var root = ARootOfItsOwn("leaves-the-configuration");

        try
        {
            var paths = new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(new CallLog(), root);
            var plugin = new Plugin(paths, new XmlSerializerThatRefusesEveryCall());

            var configurations = Path.Combine(root, "configurations");
            var document = Path.Combine(configurations, "Discover.xml");

            Directory.CreateDirectory(configurations);
            File.WriteAllText(document, "what an operator configured");

            Directory.CreateDirectory(plugin.DataFolderPath);

            plugin.OnUninstalling();

            Assert.True(
                File.Exists(document),
                $"The configuration document at {document} was removed by an uninstall, and nothing in this plugin may reach that directory.");
        }
        finally
        {
            Remove(root);
        }
    }

    /// <summary>
    /// A server that installed this plugin and never configured it is uninstalled
    /// without an error.
    /// </summary>
    /// <remarks>
    /// The data folder has never been created on that server, so this is the
    /// absent case, and an uninstall path that treated it as a failure would stop
    /// partway through cleaning up. It is #72's fourth condition arriving at the
    /// route that actually runs on a removal.
    /// </remarks>
    [Fact]
    public void AnUninstallOfAPluginThatWroteNothingIsNotAnError()
    {
        var root = ARootOfItsOwn("wrote-nothing");

        try
        {
            var plugin = new Plugin(
                new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(new CallLog(), root),
                new XmlSerializerThatRefusesEveryCall());

            Assert.False(Directory.Exists(plugin.DataFolderPath));

            plugin.OnUninstalling();

            Assert.False(Directory.Exists(plugin.DataFolderPath));
        }
        finally
        {
            Remove(root);
        }
    }

    /// <summary>
    /// Being told twice is not an error either.
    /// </summary>
    /// <remarks>
    /// The server marks a plugin whose directory it could not delete and takes
    /// the directory at the next start, so a removal is not always one pass, and
    /// an operator who did not see the first one finish repeats it. The second
    /// call meets the same state a fresh install does, which is why this is the
    /// test above with a write in front of it rather than a different property.
    /// </remarks>
    [Fact]
    public void AnUninstallTwiceIsNotAnError()
    {
        var root = ARootOfItsOwn("twice");

        try
        {
            var plugin = new Plugin(
                new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(new CallLog(), root),
                new XmlSerializerThatRefusesEveryCall());

            Directory.CreateDirectory(plugin.DataFolderPath);
            File.WriteAllText(Path.Combine(plugin.DataFolderPath, "something"), "anything");

            plugin.OnUninstalling();
            plugin.OnUninstalling();

            Assert.False(Directory.Exists(plugin.DataFolderPath));
        }
        finally
        {
            Remove(root);
        }
    }

    /// <summary>
    /// A directory nothing else in this run is looking at.
    /// </summary>
    /// <param name="test">
    /// A name for the test asking, unique within this class. Named rather than
    /// drawn, because <c>no-random</c> refuses a generated one and is right to:
    /// a directory a run cannot predict is one a failing run cannot be pointed
    /// at afterwards.
    /// </param>
    /// <returns>The root, which does not exist yet.</returns>
    private static string ARootOfItsOwn(string test) =>
        Path.Combine(
            Path.GetTempPath(),
            "jellyfin-plugin-discover-tests",
            "uninstall-" + test);

    /// <summary>
    /// Takes the root back, whether or not the test got as far as making it.
    /// </summary>
    /// <param name="root">The root.</param>
    private static void Remove(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
