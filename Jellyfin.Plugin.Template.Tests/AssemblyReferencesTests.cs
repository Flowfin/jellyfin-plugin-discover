using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Holds the plugin assembly to the set of assemblies it is allowed to reference, so that a
/// reference to a sibling plugin cannot arrive unnoticed.
/// </summary>
/// <remarks>
/// The failure this prevents is gradual and each step of it looks reasonable: a shared type,
/// then a helper, then a version constraint, and then neither plugin installs without the
/// other. Nothing else in this tree would refuse the first of them. The compiler is happy, the
/// package still carries one assembly, and the server loads it, so the day the dependency is
/// noticed is the day somebody installs one plugin without the other. This is #102.
/// </remarks>
public static class AssemblyReferencesTests
{
    /// <summary>
    /// The allow-list, as a path relative to the repository root. It is named in every failure
    /// message, because the file a reader has to edit is the whole of the repair.
    /// </summary>
    private const string AllowListPath = "Jellyfin.Plugin.Template.Tests/allowed-assembly-references.txt";

    /// <summary>
    /// The plugin references nothing the allow-list does not name.
    /// </summary>
    /// <remarks>
    /// Read out of the built assembly rather than out of the project file, because a project
    /// file says which packages were restored and an assembly says which of them the code
    /// actually reached. A sibling plugin referenced through a project reference and a sibling
    /// plugin referenced through a package look the same here, which is the point.
    /// </remarks>
    [Fact]
    public static void ThePluginReferencesNothingOutsideTheAllowedSet()
    {
        var referenced = ReferencedAssemblyNames();
        var allowed = AllowedAssemblyNames();

        var unexpected = referenced.Except(allowed, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            unexpected.Length == 0,
            $"The plugin assembly references {string.Join(", ", unexpected)}, which {AllowListPath} does not name. "
            + "Add the name there with a line saying what it is, or remove the reference. A sibling plugin belongs in "
            + "neither: see #102.");
    }

    /// <summary>
    /// The allow-list names nothing the plugin no longer references.
    /// </summary>
    /// <remarks>
    /// The other direction, and it is not symmetry for its own sake. A name left on the list
    /// after the reference is gone is a name a later reader takes as still allowed, and the
    /// list is the only record of what was decided deliberately.
    /// </remarks>
    [Fact]
    public static void TheAllowedSetNamesNothingThePluginNoLongerReferences()
    {
        var referenced = ReferencedAssemblyNames();
        var allowed = AllowedAssemblyNames();

        var stale = allowed.Except(referenced, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            stale.Length == 0,
            $"{AllowListPath} names {string.Join(", ", stale)}, which the plugin assembly does not reference. "
            + "Remove the line, so the list stays a record of what is allowed rather than of what once was.");
    }

    /// <summary>
    /// The simple names of every assembly the plugin assembly references.
    /// </summary>
    /// <returns>The referenced simple names.</returns>
    private static HashSet<string> ReferencedAssemblyNames()
        => typeof(Plugin).Assembly
            .GetReferencedAssemblies()
            .Select(name => name.Name)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The simple names the allow-list holds, with its comments and blank lines dropped.
    /// </summary>
    /// <returns>The allowed simple names.</returns>
    private static HashSet<string> AllowedAssemblyNames()
        => File.ReadAllLines(PackagedPluginIdentityTests.RepositoryFile(AllowListPath))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
}
