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
    /// A document this build's version rule accepts, carrying one element it does
    /// not declare. This is what a hand edit, a restored backup from a build with
    /// more settings, or a partially applied downgrade looks like on disk.
    /// </summary>
    private const string WithSettingFromAnotherBuild =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<PluginConfiguration xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\n"
        + "  <SchemaVersion>1</SchemaVersion>\n"
        + "  <ShelfRefreshHours>6</ShelfRefreshHours>\n"
        + "</PluginConfiguration>";

    /// <summary>
    /// The same addition written as an attribute rather than as an element.
    /// </summary>
    private const string WithAttributeFromAnotherBuild =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<PluginConfiguration xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" shelfRefreshHours=\"6\">\n"
        + "  <SchemaVersion>1</SchemaVersion>\n"
        + "</PluginConfiguration>";

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

    /// <summary>
    /// An element this build does not declare is read past and dropped, and
    /// nothing refuses the document for carrying it.
    /// </summary>
    /// <remarks>
    /// Written down rather than left to be discovered, because this is the answer
    /// the build already gives and nobody had recorded which of the two possible
    /// answers it was. The reading half is benign. The dropping half is not: the
    /// value does not survive the next save, so a document a later build wrote
    /// and this one rewrites comes back smaller than it went in.
    /// </remarks>
    [Fact]
    public void AnElementThisBuildDoesNotKnowIsDroppedRatherThanRefused()
    {
        var configuration = ReadDocument(WithSettingFromAnotherBuild);

        // Read past rather than tripped over: the version next to the unknown
        // element arrived intact.
        Assert.Equal(PluginConfiguration.CurrentSchemaVersion, configuration.SchemaVersion);

        // And not refused. The rule this build has is about the version, not
        // about the shape of the rest of the document.
        ConfigurationSchema.ThrowIfUnknown(configuration);

        // What was dropped stays dropped. Writing the configuration back is what
        // a save does, and the element is not in what comes out.
        var written = Write(configuration);

        Assert.Contains("<SchemaVersion>", written, StringComparison.Ordinal);
        Assert.DoesNotContain("ShelfRefreshHours", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// An attribute this build does not declare is treated the same way as an
    /// element, so the answer above is about the document rather than about one
    /// spelling of an addition.
    /// </summary>
    [Fact]
    public void AnAttributeThisBuildDoesNotKnowIsDroppedTheSameWay()
    {
        var configuration = ReadDocument(WithAttributeFromAnotherBuild);

        Assert.Equal(PluginConfiguration.CurrentSchemaVersion, configuration.SchemaVersion);

        ConfigurationSchema.ThrowIfUnknown(configuration);

        Assert.DoesNotContain("shelfRefreshHours", Write(configuration), StringComparison.Ordinal);
    }

    /// <summary>
    /// An unknown element does not carry a document past the version rule, so the
    /// tolerance above cannot be used to smuggle a foreign document in.
    /// </summary>
    [Fact]
    public void AnUnknownElementDoesNotExcuseAnUnknownVersion()
    {
        var configuration = ReadDocument(
            WithSettingFromAnotherBuild.Replace(
                "<SchemaVersion>1</SchemaVersion>",
                "<SchemaVersion>99</SchemaVersion>",
                StringComparison.Ordinal));

        var refusal = Assert.Throws<UnknownConfigurationSchemaException>(
            () => ConfigurationSchema.ThrowIfUnknown(configuration));

        Assert.Equal(99, refusal.FoundSchemaVersion);
    }

    /// <summary>
    /// #105's fourth condition, one step past the edge in the direction the
    /// tests above do not take. 99 is a version this build does not know, and
    /// so is the very next one; a refusal that let the neighbour through would
    /// pass the test at 99. The refusal names the setting as the document
    /// spells it, which is the rule stated at <c>PluginConfiguration.Bounds</c>.
    /// </summary>
    [Fact]
    public void ADocumentOneVersionAheadIsRefusedNamingTheSetting()
    {
        var refusal = Assert.Throws<UnknownConfigurationSchemaException>(
            () => ConfigurationSchema.ThrowIfUnknown(Read(PluginConfiguration.CurrentSchemaVersion + 1)));

        Assert.Equal(PluginConfiguration.CurrentSchemaVersion + 1, refusal.FoundSchemaVersion);
        Assert.StartsWith(nameof(PluginConfiguration.SchemaVersion), refusal.Message, StringComparison.Ordinal);
    }

    private static PluginConfiguration Read(int schemaVersion)
    {
        return ReadDocument(
            DocumentHead
            + schemaVersion.ToString(CultureInfo.InvariantCulture)
            + DocumentTail);
    }

    private static PluginConfiguration ReadDocument(string document)
    {
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

    private static string Write(PluginConfiguration configuration)
    {
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        using var text = new StringWriter(CultureInfo.InvariantCulture);
        serializer.Serialize(text, configuration);
        return text.ToString();
    }
}
