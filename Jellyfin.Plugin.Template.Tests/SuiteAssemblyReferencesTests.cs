using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Holds the test assembly to the set of assemblies it is allowed to reference, so that a suite
/// which needs a container runtime or a process on the machine cannot arrive unnoticed.
/// </summary>
/// <remarks>
/// This is the third condition on #44, which says the default run needs no container runtime and
/// that a test needing a server belongs in a named group outside it. Until now that was true
/// because nobody had written such a test, and nothing refused the first one. The failure it
/// prevents is the one the issue is about: a container client or a process launch reaches the
/// suite, the suite still passes on the machine that added it, and the day it is noticed is the
/// day the gate needs a runner somebody has to configure. The shape is the one
/// <see cref="AssemblyReferencesTests"/> already uses on the plugin assembly, for #102, and the
/// argument for an allow-list rather than a list of forbidden names is written there and in the
/// file this reads.
/// </remarks>
public static class SuiteAssemblyReferencesTests
{
    /// <summary>
    /// The allow-list, as a path relative to the repository root. It is named in every failure
    /// message, because the file a reader has to edit is the whole of the repair.
    /// </summary>
    private const string AllowListPath = "Jellyfin.Plugin.Template.Tests/allowed-test-assembly-references.txt";

    /// <summary>
    /// The suite references nothing the allow-list does not name.
    /// </summary>
    /// <remarks>
    /// Read out of the built test assembly rather than out of the project file. A package
    /// restored and never used emits no reference, and a container client the suite actually
    /// drives emits one, so this asks what the tests reached for rather than what was on the
    /// shelf. A process launch is the same reference question: <c>Process.Start</c> is in
    /// System.Diagnostics.Process, which the compiler writes into the assembly the moment a test
    /// names it.
    /// </remarks>
    [Fact]
    public static void TheSuiteReferencesNothingOutsideTheAllowedSet()
    {
        var referenced = ReferencedAssemblyNames();
        var allowed = AllowedAssemblyNames();

        var unexpected = referenced.Except(allowed, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            unexpected.Length == 0,
            $"The test assembly references {string.Join(", ", unexpected)}, which {AllowListPath} does not name. "
            + "Add the name there with a line saying what it is, or remove the reference. A container runtime "
            + "client and a process launch belong in neither: see #44.");
    }

    /// <summary>
    /// The allow-list names nothing the suite no longer references.
    /// </summary>
    /// <remarks>
    /// The other direction, for the reason the plugin's list gives: a name left on the list after
    /// the reference is gone is a name a later reader takes as still allowed, and the list is the
    /// only record of what was decided deliberately.
    /// </remarks>
    [Fact]
    public static void TheAllowedSetNamesNothingTheSuiteNoLongerReferences()
    {
        var referenced = ReferencedAssemblyNames();
        var allowed = AllowedAssemblyNames();

        var stale = allowed.Except(referenced, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            stale.Length == 0,
            $"{AllowListPath} names {string.Join(", ", stale)}, which the test assembly does not reference. "
            + "Remove the line, so the list stays a record of what is allowed rather than of what once was.");
    }

    /// <summary>
    /// The simple names of every assembly the test assembly references.
    /// </summary>
    /// <returns>The referenced simple names.</returns>
    private static HashSet<string> ReferencedAssemblyNames()
        => typeof(SuiteAssemblyReferencesTests).Assembly
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
