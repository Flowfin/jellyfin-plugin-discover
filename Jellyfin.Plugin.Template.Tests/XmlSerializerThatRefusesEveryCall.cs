using System;
using System.IO;
using System.Runtime.CompilerServices;
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
///
/// Each refusal is recorded first, so a test can state that constructing the
/// plugin touched the serialiser not at all. That is the claim
/// <see cref="XmlSerializerThatRecordsWhatIsWritten"/> cannot make, because a
/// fake that answers cannot tell a call it was built for from one nobody meant
/// to make.
/// </remarks>
internal sealed class XmlSerializerThatRefusesEveryCall : IXmlSerializer
{
    private readonly CallLog _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlSerializerThatRefusesEveryCall"/> class,
    /// recording into a log of its own.
    /// </summary>
    public XmlSerializerThatRefusesEveryCall()
        : this(new CallLog())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlSerializerThatRefusesEveryCall"/> class.
    /// </summary>
    /// <param name="log">The log this fake records into, shared with the other fakes in the run.</param>
    public XmlSerializerThatRefusesEveryCall(CallLog log)
    {
        _log = log;
    }

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

    private InvalidOperationException Refused([CallerMemberName] string member = "")
    {
        _log.Record($"IXmlSerializer.{member}");
        return new InvalidOperationException(
            "A test reached the server's serialiser. Nothing here should be reading or writing a configuration document.");
    }
}
