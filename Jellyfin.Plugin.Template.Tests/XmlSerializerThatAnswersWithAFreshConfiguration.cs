using System;
using System.IO;
using Jellyfin.Plugin.Template.Configuration;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A serialiser that answers a configuration read with the document a fresh
/// install would have, and keeps what it was asked to write.
/// </summary>
/// <remarks>
/// The third of this suite's serialisers, and it exists because the other two
/// cannot answer a read. <see cref="XmlSerializerThatRefusesEveryCall"/> throws,
/// which is the right answer for a test asserting that nothing reads a
/// configuration at all; <see cref="XmlSerializerThatRecordsWhatIsWritten"/>
/// answers a read with nothing, which the base plugin class turns into a null
/// configuration rather than into a default one.
///
/// That last part is worth stating rather than working around silently. The
/// base class reads its configuration through this interface and hands back
/// whatever the interface returned, so a test that needs a plugin whose
/// <c>Configuration</c> is readable needs a serialiser that answers. Nothing in
/// this class is a claim about what a real server's serialiser does with a
/// missing file; it is what a plugin with a fresh configuration looks like.
/// </remarks>
internal sealed class XmlSerializerThatAnswersWithAFreshConfiguration : IXmlSerializer
{
    /// <summary>
    /// Gets the last object this serialiser was asked to write, or null where
    /// it has not been asked.
    /// </summary>
    public object? LastWritten { get; private set; }

    /// <inheritdoc/>
    public object? DeserializeFromStream(Type type, Stream stream) => Fresh(type);

    /// <inheritdoc/>
    public object? DeserializeFromFile(Type type, string file) => Fresh(type);

    /// <inheritdoc/>
    public object? DeserializeFromBytes(Type type, byte[] buffer) => Fresh(type);

    /// <inheritdoc/>
    public void SerializeToStream(object obj, Stream stream) => LastWritten = obj;

    /// <inheritdoc/>
    public void SerializeToFile(object obj, string file) => LastWritten = obj;

    /// <summary>
    /// The document a fresh install would hold, for the one type this suite
    /// asks about.
    /// </summary>
    /// <param name="type">What is being read.</param>
    /// <returns>A fresh configuration.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown for anything else, so a test that starts reading a second kind of
    /// document meets this rather than a plausible answer nobody wrote.
    /// </exception>
    private static PluginConfiguration Fresh(Type type) =>
        type == typeof(PluginConfiguration)
            ? new PluginConfiguration()
            : throw new InvalidOperationException(
                "This serialiser answers for the plugin's own configuration and for nothing else. Whatever asked for something else needs a fake that was written for it.");
}
