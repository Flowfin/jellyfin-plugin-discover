using System;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Jellyfin.Plugin.Template.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What happens to a configuration document this build did not write.
/// </summary>
public class ConfigurationSchemaTests
{
    private const string DocumentHead =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<PluginConfiguration xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\n"
        + "  <SchemaVersion>";

    private const string DocumentTail = "</SchemaVersion>\n</PluginConfiguration>";

    /// <summary>
    /// A document from a version this build does not know is refused, with a
    /// message naming both versions.
    /// </summary>
    [Fact]
    public void ADocumentFromAVersionThisBuildDoesNotKnowIsRefused()
    {
        var configuration = Read(99);

        // Asserted before the refusal, on purpose. The point is that the document
        // was read well enough to see it is foreign, rather than having its values
        // quietly replaced by this build's defaults.
        Assert.Equal(99, configuration.SchemaVersion);

        var refusal = Assert.Throws<UnknownConfigurationSchemaException>(
            () => ConfigurationSchema.ThrowIfUnknown(configuration));

        Assert.Equal(99, refusal.FoundSchemaVersion);
        Assert.Contains("99", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(
            PluginConfiguration.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture),
            refusal.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// An older version is unknown in the same way a newer one is, because there
    /// is no earlier version to migrate from yet.
    /// </summary>
    [Fact]
    public void ADocumentFromAnOlderVersionIsRefusedToo()
    {
        var refusal = Assert.Throws<UnknownConfigurationSchemaException>(
            () => ConfigurationSchema.ThrowIfUnknown(Read(0)));

        Assert.Equal(0, refusal.FoundSchemaVersion);
    }

    /// <summary>
    /// A document carrying the current version is accepted unchanged, so the
    /// refusal above is about the version and not about every document.
    /// </summary>
    [Fact]
    public void ADocumentThisBuildWroteIsAccepted()
    {
        var configuration = Read(PluginConfiguration.CurrentSchemaVersion);

        ConfigurationSchema.ThrowIfUnknown(configuration);

        Assert.Equal(PluginConfiguration.CurrentSchemaVersion, configuration.SchemaVersion);
    }

    /// <summary>
    /// A fresh configuration, which is what a first install writes, carries the
    /// version this build reads.
    /// </summary>
    [Fact]
    public void ANewConfigurationCarriesTheCurrentSchemaVersion()
    {
        Assert.Equal(PluginConfiguration.CurrentSchemaVersion, new PluginConfiguration().SchemaVersion);
    }

    private static PluginConfiguration Read(int schemaVersion)
    {
        var document = DocumentHead
            + schemaVersion.ToString(CultureInfo.InvariantCulture)
            + DocumentTail;

        var serializer = new XmlSerializer(typeof(PluginConfiguration));
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var text = new StringReader(document);
        using var reader = XmlReader.Create(text, settings);
        return (PluginConfiguration)serializer.Deserialize(reader)!;
    }
}
