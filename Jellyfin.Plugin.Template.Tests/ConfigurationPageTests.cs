using System;
using System.IO;
using Jellyfin.Plugin.Template.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What the shipped configuration page offers.
/// </summary>
/// <remarks>
/// Read out of the assembly rather than off disk, so this also proves the page
/// is embedded at all. Nothing here drives a browser: what a browser would do
/// with the page is refused as a test route by #43, and the claim made here is
/// only about the bytes that ship.
/// </remarks>
public class ConfigurationPageTests
{
    /// <summary>
    /// The page is embedded in the assembly under the name the plugin asks the
    /// server for.
    /// </summary>
    [Fact]
    public void ThePageShipsEmbeddedInTheAssembly()
    {
        Assert.NotEmpty(ReadPage());
    }

    /// <summary>
    /// The page carries no control a user could set.
    /// </summary>
    /// <param name="markup">Markup that would mean a setting is on the page.</param>
    [Theory]
    [InlineData("<input")]
    [InlineData("<select")]
    [InlineData("<textarea")]
    [InlineData("type=\"submit\"")]
    public void ThePageOffersNoSetting(string markup)
    {
        Assert.DoesNotContain(markup, ReadPage(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// None of the template's four example settings is left on the page.
    /// </summary>
    /// <param name="setting">The name of a template example setting.</param>
    [Theory]
    [InlineData("AnInteger")]
    [InlineData("AString")]
    [InlineData("TrueFalseSetting")]
    [InlineData("SomeOptions")]
    public void TheTemplateExampleSettingsAreGone(string setting)
    {
        Assert.DoesNotContain(setting, ReadPage(), StringComparison.Ordinal);
    }

    private static string ReadPage()
    {
        // Derived from the namespace rather than written out, so renaming the
        // plugin moves this with it instead of breaking it.
        var resourceName = typeof(PluginConfiguration).Namespace + ".configPage.html";

        using var stream = typeof(PluginConfiguration).Assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
