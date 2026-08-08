using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Template.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// docs/configuration.md is the page an operator reads when the descriptions on the
/// configuration page are not enough. A reference maintained by hand drifts the first time a
/// setting is added by somebody who did not know the page existed, and a reference that is
/// missing a setting is worse than none, because a reader takes the absence to mean there is
/// nothing there. These tests derive the setting list from the type and refuse a page that
/// does not match it, in both directions.
/// </summary>
public static class ConfigurationReferenceTests
{
    /// <summary>
    /// A setting added to the type with no entry on the page fails here rather than shipping
    /// undocumented. This is the direction the page exists for.
    /// </summary>
    [Fact]
    public static void EverySettingOnTheTypeHasAnEntry()
    {
        var documented = Entries().Select(entry => entry.Setting).ToArray();

        foreach (var setting in Settings())
        {
            Assert.Contains(setting.Name, documented, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The other direction. An entry for a setting that has been renamed or removed is a page
    /// describing a build nobody is running, and it reads exactly like a current one.
    /// </summary>
    [Fact]
    public static void EveryEntryNamesASettingThatExists()
    {
        var onTheType = Settings().Select(setting => setting.Name).ToArray();

        foreach (var entry in Entries())
        {
            Assert.Contains(entry.Setting, onTheType, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The two columns a compiler could have written. Completeness alone leaves a page that
    /// names every setting and states the wrong type or the wrong default, which is the shape a
    /// reader has no way to catch.
    /// </summary>
    [Fact]
    public static void EveryEntryStatesTheTypeAndTheDefaultThisBuildCarries()
    {
        var fresh = new PluginConfiguration();
        var entries = Entries().ToDictionary(entry => entry.Setting, StringComparer.Ordinal);

        foreach (var setting in Settings())
        {
            Assert.True(
                entries.TryGetValue(setting.Name, out var entry),
                $"{setting.Name} has no entry in docs/configuration.md.");

            Assert.Equal(TypeName(setting.PropertyType), entry!.Type, StringComparer.Ordinal);
            Assert.Equal(Literal(setting.GetValue(fresh)), entry.Default, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Every entry names the issue it came from, so a reader finds the reasoning rather than
    /// having it restated on the page. The form is checked and the number is not: whether the
    /// issue named is the right one is a judgement, and nothing here reads the tracker.
    /// </summary>
    [Fact]
    public static void EveryEntryNamesTheIssueThatIntroducedIt()
    {
        foreach (var entry in Entries())
        {
            Assert.Matches(@"#[0-9]+", entry.IntroducedBy);
        }
    }

    /// <summary>
    /// The settings this build carries, which is what the page has to match. Public instance
    /// properties, including any the server's base class contributes, because those are written
    /// into the same document and an operator editing it by hand sees no difference.
    /// </summary>
    /// <returns>The settings, ordered by name so a failure reads the same way twice.</returns>
    private static IEnumerable<PropertyInfo> Settings() =>
        typeof(PluginConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.Name, StringComparer.Ordinal);

    /// <summary>
    /// Reads the table out of the page. The rows are the lines between the header and the first
    /// line that is not a row, so a second table elsewhere on the page is not read as settings.
    /// </summary>
    /// <returns>One entry per row.</returns>
    private static Entry[] Entries()
    {
        var lines = File.ReadAllLines(RepositoryFile(Path.Combine("docs", "configuration.md")));

        var header = Array.FindIndex(
            lines,
            line => Cells(line) is { Length: 4 } cells
                && string.Equals(cells[0], "Setting", StringComparison.Ordinal));

        Assert.True(
            header >= 0,
            "docs/configuration.md carries no table whose first column is Setting, so there is nothing to check the type against.");

        return lines
            .Skip(header + 2)
            .TakeWhile(line => line.TrimStart().StartsWith('|'))
            .Select(Cells)
            .Select(cells => new Entry(cells[0], cells[1], cells[2], cells[3]))
            .ToArray();
    }

    /// <summary>
    /// Splits one table line into its cells, dropping the empty pieces the leading and trailing
    /// pipes produce, and the backticks the page sets code in.
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

    /// <summary>
    /// The name the page uses for a type, which is the name somebody writing the setting into
    /// the document by hand would recognise rather than the runtime's.
    /// </summary>
    /// <param name="type">The setting's type.</param>
    /// <returns>The name the page is expected to carry.</returns>
    private static string TypeName(Type type) => type switch
    {
        _ when type == typeof(int) => "int",
        _ when type == typeof(long) => "long",
        _ when type == typeof(bool) => "bool",
        _ when type == typeof(string) => "string",
        _ => type.Name
    };

    /// <summary>
    /// The default as the page states it, read off a fresh configuration rather than off a
    /// constant, because what a first install writes is what an operator will find on disk.
    /// </summary>
    /// <param name="value">The value a fresh configuration carries.</param>
    /// <returns>The text the page is expected to carry.</returns>
    private static string Literal(object? value) => value switch
    {
        null => "none",
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "none"
    };

    /// <summary>
    /// Walks up from the test assembly to the directory holding the named repository file.
    /// </summary>
    /// <param name="name">Path of the file, relative to the repository root.</param>
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

    /// <summary>
    /// One row of the reference table.
    /// </summary>
    /// <param name="Setting">The setting's name.</param>
    /// <param name="Type">The type the page states.</param>
    /// <param name="Default">The default the page states.</param>
    /// <param name="IntroducedBy">The issue the page names.</param>
    private sealed record Entry(string Setting, string Type, string Default, string IntroducedBy);
}
