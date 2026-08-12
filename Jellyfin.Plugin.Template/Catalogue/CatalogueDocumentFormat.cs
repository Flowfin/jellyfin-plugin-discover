using System;
using System.Globalization;

namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// Decides whether a catalogue document is one this build can read, and says
/// why when it is not.
/// </summary>
/// <remarks>
/// Separate from <see cref="CatalogueDocumentStore"/> for the reason
/// <c>ConfigurationSchema</c> is separate from the configuration class: every
/// route that takes a document from outside this build refuses on one rule
/// rather than each growing its own. The two documents this plugin reads are
/// judged by rules that read alike, and where they differ the difference is
/// deliberate and is written below.
///
/// The version lives in the document's first line rather than inside its
/// payload, because a payload can only be parsed once its format is known and
/// that is the thing in question. So the first line names the format and
/// everything after it is read on that line's terms.
///
/// The asymmetry the configuration does not have yet. A version above
/// <see cref="CurrentVersion"/> was written by a build that knew something this
/// one does not, and reading it as though the formats agreed is how a
/// downgrade silently drops half a document. A version below it is a migration
/// this build would have to carry, and it carries none, so it is refused with
/// the same clarity rather than read field by field and hoped over. Both
/// directions are refused today; only the reasons differ, and #106 is where the
/// configuration grows the same split.
///
/// What refusing means here is that the catalogue is absent rather than wrong.
/// That decision is <see cref="CatalogueDocumentStore.Read"/>'s and the reason
/// is written there.
/// </remarks>
public static class CatalogueDocumentFormat
{
    /// <summary>
    /// What every marker line begins with, up to and including the separator
    /// the version follows.
    /// </summary>
    /// <remarks>
    /// A document whose first line does not begin with this was not written by
    /// this store at all, which is a different answer from a version this build
    /// cannot read, and the two are reported differently because an operator
    /// does different things about them.
    /// </remarks>
    public const string Family = "discover-catalogue/";

    /// <summary>
    /// The format version this build writes and the only one it reads.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Gets the most characters a marker line can occupy, so a reader looking
    /// for the end of it stops rather than scanning a file of any size.
    /// </summary>
    /// <remarks>
    /// Ten digits is what an <see cref="int"/> occupies at its widest. A first
    /// line longer than this is not a marker line whatever it holds.
    /// </remarks>
    public static int LongestMarkerLength => Family.Length + 10;

    /// <summary>
    /// Gets the marker line this build writes.
    /// </summary>
    public static string CurrentMarker => MarkerFor(CurrentVersion);

    /// <summary>
    /// The marker line a given version writes.
    /// </summary>
    /// <param name="version">The format version.</param>
    /// <returns>The line that names it.</returns>
    /// <remarks>
    /// Composed rather than written out per version, so the marker and the
    /// number cannot drift apart. A test naming a version writes the same bytes
    /// a build of that version would.
    /// </remarks>
    public static string MarkerFor(int version)
    {
        return Family + version.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads the format version out of a document's first line.
    /// </summary>
    /// <param name="markerLine">The document's first line, without its newline.</param>
    /// <param name="version">The version the line names, where it names one.</param>
    /// <returns>
    /// <see langword="true"/> where the line is a marker line naming a version,
    /// and <see langword="false"/> where it is not a marker line at all.
    /// </returns>
    /// <remarks>
    /// <see cref="NumberStyles.None"/> rather than a plain parse, and that is
    /// the whole of what stands between a marker line and a false reading. A
    /// parse that allows a sign or surrounding space reads
    /// <c>discover-catalogue/+1</c> as this build's own format, and no build
    /// wrote that line. Refusing it as not a marker line at all is the honest
    /// answer.
    ///
    /// A number too large for an <see cref="int"/> answers the same way. It
    /// names no version any build could have written, so calling it a version
    /// this build cannot read would tell an operator to go and find a build
    /// that does.
    ///
    /// A digit from another script is refused by the parse itself and by
    /// nothing written here. A check for ASCII digits stood in this method and
    /// was removed: deleting it reddened no test, because the parse never
    /// accepted one, and a guard nothing can prove bites is a guard a reader
    /// counts for more than it is worth.
    /// </remarks>
    public static bool TryReadVersion(string? markerLine, out int version)
    {
        version = 0;

        if (markerLine is null || !markerLine.StartsWith(Family, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = markerLine.AsSpan(Family.Length);

        if (digits.IsEmpty)
        {
            return false;
        }

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }

    /// <summary>
    /// Why a document naming a version this build does not read was refused,
    /// naming both versions and what to do about it.
    /// </summary>
    /// <param name="foundVersion">The version the document declared.</param>
    /// <returns>The reason, as a sentence a log line ends with.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the version handed in is the one this build reads, because
    /// there is then nothing to explain and a caller asking has lost track of
    /// which branch it is on.
    /// </exception>
    /// <remarks>
    /// Both directions name both numbers, because an operator holding one of
    /// them cannot act: "this build reads version 1" does not say whether the
    /// document is ahead or behind, and the fix differs.
    /// </remarks>
    public static string WhyItCannotBeRead(int foundVersion)
    {
        if (foundVersion == CurrentVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(foundVersion),
                foundVersion,
                "This is the version this build reads, so there is no reason to give for refusing it.");
        }

        if (foundVersion > CurrentVersion)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "it declares catalogue format version {0} and this build reads version {1}. A newer build wrote it, and reading it as if it were version {1} could hand back shelves that mean something else. Install a build that reads version {0} to keep it, or leave it where it is: the next refresh this build completes replaces it with a version {1} document.",
                foundVersion,
                CurrentVersion);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "it declares catalogue format version {0} and this build reads version {1}. Nothing here migrates a version {0} document, so it is refused rather than read field by field as though the two formats agreed. The next refresh replaces it with a version {1} document, and there is nothing to do about it unless that catalogue was worth keeping, in which case a build that reads version {0} is where it can still be read.",
            foundVersion,
            CurrentVersion);
    }
}
