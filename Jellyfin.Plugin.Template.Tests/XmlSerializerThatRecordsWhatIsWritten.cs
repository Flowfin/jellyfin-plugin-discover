using System;
using System.IO;
using System.Runtime.CompilerServices;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The server's serialiser, answering the two calls a configuration write makes
/// and recording every call it receives.
/// </summary>
/// <remarks>
/// This is the fake for a test about how many times the plugin writes, which is
/// the question <see cref="XmlSerializerThatRefusesEveryCall"/> cannot be asked:
/// the first write throws there, so a second one is never reached and a change
/// that doubled the writes would look the same.
///
/// Nothing here touches disk. The document the plugin hands over is kept as the
/// object it was handed, so a test asserts what was written rather than what a
/// round trip through XML made of it. Whether that document survives being
/// serialised is a different question and
/// <see cref="ConfigurationSchemaTests"/> is where a real serialiser answers it.
///
/// Reading returns nothing. The base class treats a missing document as a fresh
/// configuration, which is the state a first install is in, and a fake that
/// handed back a document instead would make every test start from a server that
/// had already been configured once.
/// </remarks>
internal sealed class XmlSerializerThatRecordsWhatIsWritten : IXmlSerializer
{
    private readonly CallLog _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlSerializerThatRecordsWhatIsWritten"/> class.
    /// </summary>
    /// <param name="log">The log this fake records into, shared with the other fakes in the run.</param>
    public XmlSerializerThatRecordsWhatIsWritten(CallLog log)
    {
        _log = log;
    }

    /// <summary>
    /// Gets the last object handed to <see cref="SerializeToFile"/>, or null if
    /// nothing has been written.
    /// </summary>
    public object? LastWritten { get; private set; }

    /// <inheritdoc />
    public object? DeserializeFromStream(Type type, Stream stream)
    {
        Record();
        return null;
    }

    /// <inheritdoc />
    public object? DeserializeFromFile(Type type, string file)
    {
        Record(Path.GetFileName(file));
        return null;
    }

    /// <inheritdoc />
    public object? DeserializeFromBytes(Type type, byte[] buffer)
    {
        Record();
        return null;
    }

    /// <inheritdoc />
    public void SerializeToStream(object obj, Stream stream) => Record();

    /// <inheritdoc />
    public void SerializeToFile(object obj, string file)
    {
        LastWritten = obj;
        Record(Path.GetFileName(file));
    }

    private void Record(string? argument = null, [CallerMemberName] string member = "")
    {
        _log.Record(argument is null
            ? $"IXmlSerializer.{member}"
            : $"IXmlSerializer.{member}({argument})");
    }
}
