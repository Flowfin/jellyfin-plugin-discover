using System;
using System.IO;
using System.Runtime.CompilerServices;
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
///
/// Both answers are recorded, and so is every refusal. How many times the base
/// class asks for a directory is behaviour a later change can move without
/// moving what any test asserts, and the log is where that becomes visible.
/// </remarks>
internal sealed class ApplicationPathsThatRefuseEveryCallButThePluginDirectories : IApplicationPaths
{
    private readonly CallLog _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationPathsThatRefuseEveryCallButThePluginDirectories"/> class,
    /// recording into a log of its own.
    /// </summary>
    public ApplicationPathsThatRefuseEveryCallButThePluginDirectories()
        : this(new CallLog())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationPathsThatRefuseEveryCallButThePluginDirectories"/> class.
    /// </summary>
    /// <param name="log">The log this fake records into, shared with the other fakes in the run.</param>
    public ApplicationPathsThatRefuseEveryCallButThePluginDirectories(CallLog log)
    {
        _log = log;
    }

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
    public string PluginsPath => Answered(Path.Combine(Path.GetTempPath(), "jellyfin-plugin-discover-tests", "plugins"));

    /// <inheritdoc />
    public string PluginConfigurationsPath => Answered(Path.Combine(Path.GetTempPath(), "jellyfin-plugin-discover-tests", "configurations"));

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

    private string Answered(string path, [CallerMemberName] string member = "")
    {
        _log.Record($"IApplicationPaths.{member}");
        return path;
    }

    private InvalidOperationException Refused([CallerMemberName] string member = "")
    {
        _log.Record($"IApplicationPaths.{member}");
        return new InvalidOperationException(
            "A test reached a server path this fake refuses. Add the member here, with the test that needs it, rather than making the fake answer everything.");
    }
}
