using System;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Jellyfin.Plugin.Template.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What this build makes of the configuration document the first released
/// version wrote.
/// </summary>
/// <remarks>
/// #106's third condition. Every test beside this one reads a document some
/// test composed, which answers what this build makes of a shape somebody
/// chose. This one reads what a released build actually writes, which is the
/// only document an upgrade ever meets.
///
/// The fixture is in <see cref="ConfigurationDocumentsEveryVersionWrote"/> and
/// the argument for holding it as bytes is written there.
///
/// One setting has been added to this type since that release, so this file is
/// not the round trip it would have been on the day of the release: the
/// document is short of an element that a document this build writes carries,
/// and the last test below is about exactly that gap. It is the reason the
/// fixture is worth having before there is a second release rather than after.
/// </remarks>
public class AConfigurationTheFirstReleaseWroteTests
{
    /// <summary>
    /// The first release's document is one this build's version rule accepts,
    /// so an upgrade from it is a read rather than a refusal.
    /// </summary>
    [Fact]
    public void TheFirstReleasesDocumentIsNotForeignToThisBuild()
    {
        var configuration = Read(ConfigurationDocumentsEveryVersionWrote.Version0100Stable);

        // Asserted before the rule is asked, on purpose: what the rule is about
        // is the version in the document rather than this build's own constant.
        Assert.Equal(1, configuration.SchemaVersion);
        Assert.Equal(PluginConfiguration.CurrentSchemaVersion, configuration.SchemaVersion);

        ConfigurationSchema.ThrowIfUnknown(configuration);
    }

    /// <summary>
    /// Every setting the first release wrote arrives with the value it wrote,
    /// rather than being replaced by this build's own default.
    /// </summary>
    /// <remarks>
    /// The values are written out here rather than compared against
    /// <c>CatalogueBounds</c>'s constants or against a fresh
    /// <see cref="PluginConfiguration"/>. A comparison against either asserts
    /// that today's default equals itself and goes on passing on the day a
    /// default moves, which is the day this test is the one that should fail.
    /// </remarks>
    [Fact]
    public void EverySettingTheFirstReleaseWroteSurvivesTheRead()
    {
        var configuration = Read(ConfigurationDocumentsEveryVersionWrote.Version0100Stable);

        Assert.Equal(20, configuration.MaximumTitlesPerShelf);
        Assert.Equal(120, configuration.MaximumTitlesAcrossAllShelves);
        Assert.True(configuration.Enabled);
        Assert.Empty(configuration.UsersRefusedTheAsk);

        // The pair is read as one value by the type itself, so a document that
        // survived element by element and produces a refused pair would still
        // be a document this build cannot run on.
        var bounds = configuration.Bounds();

        Assert.Equal(20, bounds.TitlesPerShelf);
        Assert.Equal(120, bounds.TitlesAcrossAllShelves);
    }

    /// <summary>
    /// A setting added after the first release arrives at the value its
    /// initialiser gives it, because the document has no element for it.
    /// </summary>
    /// <remarks>
    /// <see cref="PluginConfiguration.IncludeAdultTitles"/>'s own remark states
    /// this about documents written before it existed. Until this fixture there
    /// was no such document in the tree for it to be true of, so the sentence
    /// rested on what a deserialiser is understood to do rather than on a
    /// reading. The absence is asserted first: were the fixture ever replaced
    /// by a document this build wrote, the element would be in it and the
    /// assertion under it would be about nothing.
    /// </remarks>
    [Fact]
    public void ASettingAddedAfterTheFirstReleaseArrivesAtItsInitialiser()
    {
        var document = ConfigurationDocumentsEveryVersionWrote.Text(
            ConfigurationDocumentsEveryVersionWrote.Version0100Stable);

        Assert.DoesNotContain(
            nameof(PluginConfiguration.IncludeAdultTitles),
            document,
            StringComparison.Ordinal);

        Assert.False(Read(ConfigurationDocumentsEveryVersionWrote.Version0100Stable).IncludeAdultTitles);

        // And the gap is this build's rather than the fixture's: what this build
        // writes carries the element the released document does not.
        Assert.Contains(
            nameof(PluginConfiguration.IncludeAdultTitles),
            Write(new PluginConfiguration()),
            StringComparison.Ordinal);
    }

    private static PluginConfiguration Read(string fixture)
    {
        var serializer = new XmlSerializer(typeof(PluginConfiguration));
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var bytes = new MemoryStream(ConfigurationDocumentsEveryVersionWrote.Bytes(fixture));
        using var reader = XmlReader.Create(bytes, settings);
        return (PluginConfiguration)serializer.Deserialize(reader)!;
    }

    private static string Write(PluginConfiguration configuration)
    {
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        using var text = new StringWriter(CultureInfo.InvariantCulture);
        serializer.Serialize(text, configuration);
        return text.ToString();
    }
}
