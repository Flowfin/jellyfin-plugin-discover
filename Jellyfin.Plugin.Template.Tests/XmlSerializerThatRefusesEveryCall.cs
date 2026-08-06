using System;
using System.IO;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The server's serialiser, refusing every call.
/// </summary>
/// <remarks>
/// A test that constructs the plugin to read something the plugin states about
/// itself must not also be reading or writing a configuration document. Every
/// member throws, so a test that started doing so fails with the member name
/// rather than quietly touching disk.
/// </remarks>
internal sealed class XmlSerializerThatRefusesEveryCall : IXmlSerializer
{
    /// <inheritdoc />
    public object? DeserializeFromStream(Type type, Stream stream) => throw Refused();

    /// <inheritdoc />
    public object? DeserializeFromFile(Type type, string file) => throw Refused();

    /// <inheritdoc />
    public object? DeserializeFromBytes(Type type, byte[] buffer) => throw Refused();

    /// <inheritdoc />
    public void SerializeToStream(object obj, Stream stream) => throw Refused();

    /// <inheritdoc />
    public void SerializeToFile(object obj, string file) => throw Refused();

    private static InvalidOperationException Refused()
    {
        return new InvalidOperationException(
            "A test reached the server's serialiser. Nothing here should be reading or writing a configuration document.");
    }
}
