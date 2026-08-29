namespace Jellyfin.Plugin.Template.Seam;

/// <summary>
/// The version of the handover contract this build writes.
/// </summary>
/// <remarks>
/// One whole number, counting breaking changes only, decided in
/// `docs/decisions/0004-what-crosses-the-seam-to-a-requests-plugin.md` under
/// #101. It is not this plugin's version, not the catalogue record's schema
/// version, and it carries no second part: a receiver has exactly one question,
/// whether it can read the message, and one number answers it.
///
/// A field a receiver may ignore does not raise it. Removing a field, changing
/// what one means, making an optional field required or the reverse, or
/// changing the alphabet of an existing field's value are what raise it, and
/// the note above is where that list is authoritative.
///
/// This plugin writes this number and reads nothing back except the
/// acknowledgement, so it never sees a receiver's version and never adapts to
/// one. There is no negotiation across this seam.
/// </remarks>
public static class WantContract
{
    /// <summary>
    /// The contract version this build writes onto every want it hands over.
    /// </summary>
    /// <remarks>
    /// Version 1 is not frozen yet. Nothing has been published from this
    /// repository and no sibling exists, so a change to the field set before
    /// the first release edits version 1 rather than minting version 2. From
    /// the first release that ships this seam, the rule above applies as
    /// written.
    ///
    /// The replay marker on <see cref="Want"/> is the first field to arrive
    /// that way, under #335. It arrives at version 1 rather than minting a
    /// second one for two reasons that hold separately: it is a field a
    /// receiver may ignore, which the rule above says does not raise the
    /// number, and no release has shipped, which the paragraph above says
    /// makes the field set version 1's rather than a successor's. The note
    /// under `## How this contract changes` carries the argument and the
    /// commands behind both.
    /// </remarks>
    public const int CurrentVersion = 1;
}
