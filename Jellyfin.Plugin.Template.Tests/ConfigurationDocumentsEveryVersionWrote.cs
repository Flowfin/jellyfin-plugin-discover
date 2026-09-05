using System;
using System.Text;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// One configuration document per released version, as bytes, so this build can
/// be tested against a document it did not write.
/// </summary>
/// <remarks>
/// #106's third condition. A fixture produced by asking this build to serialise
/// a fresh configuration proves that the current build reads what the current
/// build writes, which is the one thing an upgrade is never about. These are
/// literals instead: a version's literal is captured when that version is
/// released and is never rewritten afterwards. Rewriting one to match a change
/// in the writer is how a compatibility test quietly becomes a round trip.
///
/// The shape is <see cref="CatalogueDocumentsEveryVersionWrote"/>'s, and the
/// argument for it is written there rather than a second time here. What
/// differs is the population: that one carries a literal per FORMAT version
/// including versions no build ever wrote, because its reader has a branch for
/// each; this one carries a literal per RELEASED version, because what a
/// configuration reader meets on disk is whatever an operator installed before,
/// and nothing else has ever existed.
///
/// Base64 rather than the text. The document is written with carriage returns,
/// which is what the serialiser emits, and a raw literal in a source file would
/// be normalised by this repository's own <c>.gitattributes</c> on the way into git.
/// That would silently turn the fixture into a document no release wrote.
/// </remarks>
internal static class ConfigurationDocumentsEveryVersionWrote
{
    /// <summary>
    /// The document <c>0.1.0.0-stable</c> writes for a configuration nobody has
    /// touched, which is the state of every server that installed the first
    /// release and left the settings alone.
    /// </summary>
    /// <remarks>
    /// Captured from the source at that tag rather than typed, by serialising a
    /// fresh configuration there and taking the bytes. The pull request that
    /// added it carries the run.
    ///
    /// What it is a document of is the released SOURCE. Nothing here reads the
    /// shipped assembly or a configuration off a server that installed it, so a
    /// packaging step that altered the type between the tag and the artefact
    /// would not show, and no route in this repository would say so.
    /// </remarks>
    public const string Version0100Stable =
        "PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz4NCjxQbHVnaW5Db25maWd1cmF0aW9uIHhtbG5zOnhzaT0iaHR0cDovL3d3dy53My5vcmcvMjAwMS9YTUxTY2hlbWEtaW5zdGFuY2UiIHhtbG5zOnhzZD0iaHR0cDovL3d3dy53My5vcmcvMjAwMS9YTUxTY2hlbWEiPg0KICA8U2NoZW1hVmVyc2lvbj4xPC9TY2hlbWFWZXJzaW9uPg0KICA8TWF4aW11bVRpdGxlc1BlclNoZWxmPjIwPC9NYXhpbXVtVGl0bGVzUGVyU2hlbGY+DQogIDxNYXhpbXVtVGl0bGVzQWNyb3NzQWxsU2hlbHZlcz4xMjA8L01heGltdW1UaXRsZXNBY3Jvc3NBbGxTaGVsdmVzPg0KICA8VXNlcnNSZWZ1c2VkVGhlQXNrIC8+DQogIDxFbmFibGVkPnRydWU8L0VuYWJsZWQ+DQo8L1BsdWdpbkNvbmZpZ3VyYXRpb24+";

    /// <summary>
    /// The bytes of one of the documents above.
    /// </summary>
    /// <param name="document">The fixture, as it is written down here.</param>
    /// <returns>The document, as it would sit on a disk.</returns>
    public static byte[] Bytes(string document)
    {
        return Convert.FromBase64String(document);
    }

    /// <summary>
    /// One of the documents above as text, for a reading that is about what the
    /// document says rather than about the bytes it is made of.
    /// </summary>
    /// <param name="document">The fixture, as it is written down here.</param>
    /// <returns>The document, decoded.</returns>
    public static string Text(string document)
    {
        return Encoding.UTF8.GetString(Bytes(document));
    }
}
