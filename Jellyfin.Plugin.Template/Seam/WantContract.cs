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
    /// THIS SAID VERSION 1 IS NOT FROZEN YET, on the ground that nothing had
    /// been published from this repository and no sibling exists.
    /// 0.1.0.0-stable was published on 2026-09-04, so half of that ground is
    /// gone and the other half is not: the archive carries this type and
    /// carries no receiver, and no sibling exists, so nothing anywhere holds a
    /// version 1 want for an edit to break. Whether a release that ships the
    /// type without a receiver is the release that freezes the contract is
    /// #94's and #10's, and until one of them says, a change to the field set
    /// is not to be taken as free.
    ///
    /// The replay marker on <see cref="Want"/> is the first field to arrive
    /// that way, under #335. It arrived at version 1 for two reasons that hold
    /// separately, and only one of them has survived the release: it is a field
    /// a receiver may ignore, which the rule above says does not raise the
    /// number, and that one is untouched. The other was that no release had
    /// shipped. The note under `## How this contract changes` carries both
    /// halves and the commands behind them.
    /// </remarks>
    public const int CurrentVersion = 1;
}
