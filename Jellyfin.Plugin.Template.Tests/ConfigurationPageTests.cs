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
    /// Settings kept off the configuration page on purpose.
    /// </summary>
    /// <remarks>
    /// SchemaVersion is written by the build and read by
    /// ConfigurationSchema.ThrowIfUnknown. A control for it would offer an
    /// operator a number whose only effect is to have their configuration
    /// refused.
    /// </remarks>
    private static readonly string[] HiddenFromThePage = ["SchemaVersion"];

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

    /// <summary>
    /// The page names no identifier that is not this plugin's.
    /// </summary>
    /// <remarks>
    /// The template's page carried the identifier as a literal in its script,
    /// which is how a page copied from somewhere else ends up configuring the
    /// plugin it was copied from and reporting nothing at all. The page ships
    /// with no identifier on it today, so this passes over an empty set; what
    /// it refuses is the day one appears and it is the wrong one.
    /// ConfigurationPageReaderTests is where the reader is shown finding an
    /// identifier, so the empty set is a fact about the page rather than about
    /// the reader.
    /// </remarks>
    [Fact]
    public void ThePageNamesNoIdentifierButThePluginsOwn()
    {
        var plugin = new Plugin(
            new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(),
            new XmlSerializerThatRefusesEveryCall());

        var own = plugin.Id.ToString("D", CultureInfo.InvariantCulture);

        foreach (var identifier in ConfigurationPageReader.IdentifiersIn(ReadPage()))
        {
            Assert.Equal(own, identifier, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Every control the page offers writes a setting the configuration has.
    /// </summary>
    /// <remarks>
    /// A control naming a property nobody reads is a setting an operator can
    /// change with no effect, which is worse than a missing control because it
    /// looks like it worked.
    /// </remarks>
    [Fact]
    public void EveryControlOnThePageNamesAPropertyOnTheConfiguration()
    {
        var properties = ConfigurationPropertyNames();

        foreach (var control in ConfigurationPageReader.NamedControlsIn(ReadPage()))
        {
            Assert.Contains(control, properties, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Every setting the configuration has is either on the page or recorded
    /// here as deliberately kept off it.
    /// </summary>
    /// <remarks>
    /// This is the direction that bites as the plugin grows: a setting added
    /// with no control is a setting only somebody editing a file by hand can
    /// reach. The list below is the deliberate half, and it is short on
    /// purpose.
    /// </remarks>
    [Fact]
    public void EveryConfigurationPropertyHasAControlOrIsRecordedAsHidden()
    {
        var controls = ConfigurationPageReader.NamedControlsIn(ReadPage());

        foreach (var property in ConfigurationPropertyNames())
        {
            if (HiddenFromThePage.Contains(property, StringComparer.Ordinal))
            {
                continue;
            }

            Assert.Contains(property, controls, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The list of deliberately hidden settings names only settings that exist.
    /// </summary>
    /// <remarks>
    /// Without this, a setting removed from the configuration leaves its name
    /// in the list and the list slowly becomes a place where the check above
    /// can be silenced by accident.
    /// </remarks>
    [Fact]
    public void TheHiddenListNamesOnlySettingsThatExist()
    {
        var properties = ConfigurationPropertyNames();

        foreach (var hidden in HiddenFromThePage)
        {
            Assert.Contains(hidden, properties, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The page asks nothing of any host but the server serving it.
    /// </summary>
    /// <remarks>
    /// The page is rendered in an administrator's browser, inside a session
    /// that can change the server. A reference to a host outside the server
    /// puts whoever controls that host inside that session, and it also tells
    /// them which servers run this plugin.
    /// </remarks>
    [Fact]
    public void ThePageRequestsNothingFromAHostOutsideTheServer()
    {
        Assert.Empty(ConfigurationPageReader.HostsOutsideTheServerIn(ReadPage()));
    }

    /// <summary>
    /// The page says that the library's name cannot be changed, and why.
    /// </summary>
    /// <param name="phrase">Wording the sentence has to carry.</param>
    /// <remarks>
    /// This page is where an operator goes looking for a rename, so it is
    /// where the absence of one has to be answered. The server builds the
    /// identity of every item under a library out of the library's name, which
    /// is read out of the server's source in docs/title-identity.md, so a
    /// rename orphans every favourite and every played mark under it. That is
    /// #60.
    ///
    /// The phrases below are what the sentence has to carry rather than the
    /// sentence itself, so the wording can be improved without this turning
    /// red, while dropping the reason or the consequence cannot.
    /// </remarks>
    [Theory]
    [InlineData("cannot be changed")]
    [InlineData("identity")]
    [InlineData("favourite")]
    [InlineData("docs/title-identity.md")]
    public void ThePageSaysTheLibraryNameCannotBeChanged(string phrase)
    {
        Assert.Contains(phrase, ReadPage(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The settings this plugin's configuration declares.
    /// </summary>
    /// <returns>The property names.</returns>
    /// <remarks>
    /// Declared on the configuration class rather than inherited: what the
    /// server's base class carries is the server's business and no page of
    /// this plugin's draws it.
    /// </remarks>
    private static string[] ConfigurationPropertyNames()
    {
        return typeof(PluginConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .ToArray();
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
