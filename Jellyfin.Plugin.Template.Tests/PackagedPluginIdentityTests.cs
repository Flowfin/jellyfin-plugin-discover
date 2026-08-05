using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Asserts the two things about the plugin that a compiler cannot: that the packaging metadata
/// names the assembly the build actually produces, and that the type the server loads has the
/// constructor the server calls. Both fail at install time rather than at build time, which is
/// the class of failure #19 exists for and the reason these are the first tests here.
/// </summary>
public static class PackagedPluginIdentityTests
{
    /// <summary>
    /// build.yaml lists what goes into the package. A rename of the plugin project that does not
    /// reach that list produces a package with no plugin assembly in it, and nothing in the build
    /// says so.
    /// </summary>
    [Fact]
    public static void BuildYamlNamesTheAssemblyTheBuildProduces()
    {
        var assemblyFileName = typeof(Plugin).Assembly.GetName().Name + ".dll";
        var buildYaml = File.ReadAllLines(RepositoryFile("build.yaml"));

        var artifacts = buildYaml
            .SkipWhile(line => !line.StartsWith("artifacts:", StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(line => line.StartsWith('-'))
            .Select(line => line.TrimStart('-').Trim().Trim('"'))
            .ToArray();

        Assert.Contains(assemblyFileName, artifacts, StringComparer.Ordinal);
    }

    /// <summary>
    /// The server constructs a plugin through this exact signature. A change to it compiles and
    /// then fails on a running server, where the only symptom is the plugin missing from the
    /// dashboard.
    /// </summary>
    [Fact]
    public static void ThePluginHasTheConstructorTheServerCalls()
    {
        var constructor = typeof(Plugin).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            [typeof(IApplicationPaths), typeof(IXmlSerializer)],
            modifiers: null);

        Assert.NotNull(constructor);
    }

    /// <summary>
    /// Walks up from the test assembly to the directory holding the named repository file.
    /// </summary>
    /// <param name="name">File name to find, relative to the repository root.</param>
    /// <returns>The full path to that file.</returns>
    private static string RepositoryFile(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Walked up from {AppContext.BaseDirectory} without finding {name}. The test reads it out of the repository root.",
            name);
    }
}
