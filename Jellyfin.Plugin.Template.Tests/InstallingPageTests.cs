using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// docs/installing.md tells an operator which paths to delete by hand after an uninstall,
/// because the server removes the directory it unpacked the package into and not the one this
/// plugin writes to. Both of those paths are named after the assembly the server loaded, so
/// neither is a constant: they move the day the assembly is renamed, which is #14. A manual
/// removal step is the worst place in the documentation for a path that has quietly gone
/// stale, because an operator follows it to the letter and deletes nothing, or deletes
/// something else. These tests derive the paths from the assembly and refuse a page naming a
/// different set, in both directions.
/// </summary>
public static class InstallingPageTests
{
    /// <summary>
    /// The page an operator reads, relative to the repository root.
    /// </summary>
    private const string Page = "docs/installing.md";

    /// <summary>
    /// The direction the check exists for. A rename that does not reach the page leaves a step
    /// naming a directory that no longer exists, and nothing else in the tree says so.
    /// </summary>
    [Fact]
    public static void EveryPathTheServerDerivesFromTheAssemblyHasARow()
    {
        var paths = ManualRemovalPaths();

        foreach (var derived in DerivedFromTheAssembly())
        {
            Assert.Contains(derived, paths, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The other direction. A row naming a path this build does not produce is a step that
    /// deletes somebody else's directory or nothing at all, and it reads exactly like a current
    /// one.
    /// </summary>
    [Fact]
    public static void EveryRowNamesAPathThisBuildProduces()
    {
        var derived = DerivedFromTheAssembly();

        foreach (var path in ManualRemovalPaths())
        {
            Assert.Contains(path, derived, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The table has to be found before it can be compared, and a page that lost it would
    /// otherwise pass both checks above with nothing in either set.
    /// </summary>
    [Fact]
    public static void ThePageCarriesTheTableThoseChecksRead()
    {
        Assert.NotEmpty(ManualRemovalPaths());
    }

    /// <summary>
    /// The two paths the server derives from the assembly this build produces. The data folder
    /// is the assembly's file name without its extension, directly under the plugins path; the
    /// configuration is that name with an XML extension, under the plugins path's
    /// <c>configurations</c> directory. Both are read off the assembly rather than written here,
    /// so this set moves with a rename and the page has to move with it.
    /// </summary>
    /// <returns>The paths, relative to the server's plugins directory, as the page writes them.</returns>
    private static string[] DerivedFromTheAssembly()
    {
        var assembly = typeof(Plugin).Assembly.GetName().Name;

        return
        [
            assembly!,
            "configurations/" + assembly + ".xml"
        ];
    }

    /// <summary>
    /// Reads the second column out of the table under the manual removal steps. The rows are the
    /// lines between the header and the first line that is not a row, so a table elsewhere on the
    /// page is not read as removal steps.
    /// </summary>
    /// <returns>One path per row.</returns>
    private static string[] ManualRemovalPaths()
    {
        var lines = File.ReadAllLines(PackagedPluginIdentityTests.RepositoryFile(
            Path.Combine("docs", "installing.md")));

        var header = Array.FindIndex(
            lines,
            line => Cells(line) is { Length: 3 } cells
                && cells[1].StartsWith("Path", StringComparison.Ordinal));

        Assert.True(
            header >= 0,
            $"{Page} carries no table whose second column is a path, so there is nothing to check the assembly name against.");

        return lines
            .Skip(header + 2)
            .TakeWhile(line => line.TrimStart().StartsWith('|'))
            .Select(Cells)
            .Where(cells => cells.Length == 3)
            .Select(cells => cells[1])
            .ToArray();
    }

    /// <summary>
    /// Splits one table line into its cells, dropping the empty pieces the leading and trailing
    /// pipes produce, and the backticks the page sets a path in.
    /// </summary>
    /// <param name="line">A line of the page.</param>
    /// <returns>The cells, or an empty array for a line that is not a row.</returns>
    private static string[] Cells(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('|'))
        {
            return [];
        }

        return trimmed
            .Trim('|')
            .Split('|')
            .Select(cell => cell.Trim().Trim('`').Trim())
            .ToArray();
    }
}
