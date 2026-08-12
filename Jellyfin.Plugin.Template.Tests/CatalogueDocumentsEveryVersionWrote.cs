using System;
using System.Text;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// One catalogue document per format version, as bytes, so a reader can be
/// tested against documents this build did not write.
/// </summary>
/// <remarks>
/// A fixture produced by asking the store to write one proves that the current
/// build reads what the current build writes, which is the one thing neither
/// direction of #67 is about. These are literals instead, and a version's
/// literal is added when that version is added and is never rewritten
/// afterwards. Rewriting one to match a change in the writer is how a
/// compatibility test quietly becomes a round trip.
///
/// Base64 rather than the text, because the bytes have to be exact and the
/// document's own checksum is computed over them. A raw literal in a source
/// file is a line ending away from being a different document, and a checkout
/// on another platform is where that would show.
///
/// Every one of them carries the same payload and a checksum that is correct
/// for it. Only the first line differs. That is what makes a refusal here a
/// refusal on the version rather than one on the checksum that happened to
/// arrive first.
/// </remarks>
internal static class CatalogueDocumentsEveryVersionWrote
{
    /// <summary>
    /// The payload every fixture below carries.
    /// </summary>
    public const string Payload = "the shelves as they stood";

    /// <summary>
    /// A document whose first line names a version below the first one this
    /// store ever wrote.
    /// </summary>
    /// <remarks>
    /// No build wrote a version 0 document, and this is here anyway: without
    /// it, the branch that refuses a document from an older format has no
    /// proof that it bites, and that branch is what the day version 2 ships
    /// depends on. On that day the version 1 fixture below becomes the real
    /// case and this one has done its job.
    /// </remarks>
    public const string VersionZero =
        "ZGlzY292ZXItY2F0YWxvZ3VlLzAKMjAzOUZGQUY2MDQxMjcxNTAzN0M1Njc2RDVCM0NDQzUzREE4QUNFMjJENEQ1NjkxMzk4NzcwRDRBN0QzRERDOQoKdGhlIHNoZWx2ZXMgYXMgdGhleSBzdG9vZA==";

    /// <summary>
    /// A document written by a build of the format version this one reads.
    /// </summary>
    public const string VersionOne =
        "ZGlzY292ZXItY2F0YWxvZ3VlLzEKMjAzOUZGQUY2MDQxMjcxNTAzN0M1Njc2RDVCM0NDQzUzREE4QUNFMjJENEQ1NjkxMzk4NzcwRDRBN0QzRERDOQoKdGhlIHNoZWx2ZXMgYXMgdGhleSBzdG9vZA==";

    /// <summary>
    /// A document written by a build whose format is ahead of this one's.
    /// </summary>
    /// <remarks>
    /// Well formed in every respect except the version, on purpose. A fixture
    /// that was also truncated or altered would be refused by whichever leg ran
    /// first, and the test would pass with the version rule deleted.
    /// </remarks>
    public const string VersionTwo =
        "ZGlzY292ZXItY2F0YWxvZ3VlLzIKMjAzOUZGQUY2MDQxMjcxNTAzN0M1Njc2RDVCM0NDQzUzREE4QUNFMjJENEQ1NjkxMzk4NzcwRDRBN0QzRERDOQoKdGhlIHNoZWx2ZXMgYXMgdGhleSBzdG9vZA==";

    /// <summary>
    /// A document whose first line is in the family this store writes and names
    /// no version at all.
    /// </summary>
    /// <remarks>
    /// The near-miss between the two answers a reader can give. This is not a
    /// version anybody can install a build for, so it is reported as something
    /// this store never wrote rather than as a version it cannot read.
    /// </remarks>
    public const string VersionThatIsNotANumber =
        "ZGlzY292ZXItY2F0YWxvZ3VlL3gKMjAzOUZGQUY2MDQxMjcxNTAzN0M1Njc2RDVCM0NDQzUzREE4QUNFMjJENEQ1NjkxMzk4NzcwRDRBN0QzRERDOQoKdGhlIHNoZWx2ZXMgYXMgdGhleSBzdG9vZA==";

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
    /// The payload every fixture above carries, as the bytes a caller of the
    /// store gets back.
    /// </summary>
    /// <returns>The payload.</returns>
    public static byte[] PayloadBytes()
    {
        return Encoding.UTF8.GetBytes(Payload);
    }
}
