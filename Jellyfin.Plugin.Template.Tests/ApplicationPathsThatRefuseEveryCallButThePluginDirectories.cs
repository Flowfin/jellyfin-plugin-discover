using System;
using System.IO;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The server's paths, refusing every member except the two directories the
/// base plugin class reads while it is being constructed.
/// </summary>
/// <remarks>
/// Written the same way as ServerApplicationHostThatRefusesEveryCall and for
/// the same reason: a fake handing back a plausible path for everything would
/// let a plugin read one, pass here, and then behave differently on a server
/// that returns something else. Anything this test project comes to need is
/// added deliberately, one member at a time, with the test that needed it.
///
/// The two exceptions return directories under the temporary directory and
/// nothing writes to them. The base class joins names onto both while the
/// constructor runs, so refusing them would mean no test could construct the
/// plugin at all. Which two it reads is not a guess: the fake refused them by
/// name and the stack said where.
/// </remarks>
internal sealed class ApplicationPathsThatRefuseEveryCallButThePluginDirectories : IApplicationPaths
{
    /// <inheritdoc />
    public string ProgramDataPath => throw Refused();

    /// <inheritdoc />
    public string WebPath => throw Refused();

    /// <inheritdoc />
    public string ProgramSystemPath => throw Refused();

    /// <inheritdoc />
    public string DataPath => throw Refused();

    /// <inheritdoc />
    public string ImageCachePath => throw Refused();

    /// <inheritdoc />
    public string PluginsPath => Path.Combine(Path.GetTempPath(), "jellyfin-plugin-discover-tests", "plugins");

    /// <inheritdoc />
    public string PluginConfigurationsPath => Path.Combine(Path.GetTempPath(), "jellyfin-plugin-discover-tests", "configurations");

    /// <inheritdoc />
    public string LogDirectoryPath => throw Refused();

    /// <inheritdoc />
    public string ConfigurationDirectoryPath => throw Refused();

    /// <inheritdoc />
    public string SystemConfigurationFilePath => throw Refused();

    /// <inheritdoc />
    public string CachePath => throw Refused();

    /// <inheritdoc />
    public string TempDirectory => throw Refused();

    /// <inheritdoc />
    public string TrickplayPath => throw Refused();

    /// <inheritdoc />
    public string VirtualDataPath => throw Refused();

    /// <inheritdoc />
    public string BackupPath => throw Refused();

    /// <inheritdoc />
    public void MakeSanityCheckOrThrow() => throw Refused();

    /// <inheritdoc />
    public void CreateAndCheckMarker(string path, string markerName, bool recursive = false) => throw Refused();

    private static InvalidOperationException Refused()
    {
        return new InvalidOperationException(
            "A test reached a server path this fake refuses. Add the member here, with the test that needs it, rather than making the fake answer everything.");
    }
}
