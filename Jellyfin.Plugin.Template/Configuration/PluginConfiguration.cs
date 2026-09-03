using System;
using System.Collections.ObjectModel;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Template.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
/// <remarks>
/// The settings this plugin needs arrive with the features that read them. What
/// this class carried from its first commit is the schema version, because a
/// configuration written before there is a version rule is a configuration
/// nobody can migrate afterwards.
///
/// The two bounds beside it are #58's, and they are the first settings here that
/// cost an operator something. What holds them together is
/// <see cref="CatalogueBounds"/> rather than this class: a pair of integers with
/// nothing comparing them is the shape that lets a contradiction sit on disk, so
/// the numbers are stored here and read through <see cref="Bounds"/>.
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// The schema version this build writes, and the only one it is able to read.
    /// </summary>
    /// <remarks>
    /// Raise this when a change to this class cannot be read by the build before
    /// it, and say in CHANGELOG.md what moved.
    /// </remarks>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        SchemaVersion = CurrentSchemaVersion;
        MaximumTitlesPerShelf = CatalogueBounds.DefaultTitlesPerShelf;
        MaximumTitlesAcrossAllShelves = CatalogueBounds.DefaultTitlesAcrossAllShelves;
    }

    /// <summary>
    /// Gets or sets the schema version of this configuration document.
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the most titles one shelf may hold.
    /// </summary>
    /// <remarks>
    /// Settable and unchecked here, because this is the property an XML
    /// deserialiser writes into and a deserialiser that threw would leave an
    /// operator with a plugin that cannot start rather than one that refuses a
    /// number. The refusal is <see cref="Bounds"/>, which is where every reader
    /// of these two goes.
    /// </remarks>
    public int MaximumTitlesPerShelf { get; set; }

    /// <summary>
    /// Gets or sets the most titles every shelf may hold between them.
    /// </summary>
    /// <remarks>
    /// The number an operator reads when they want to know what this plugin
    /// costs their library database, and the one this plugin is answerable for:
    /// every title under it becomes a row the server keeps. Unchecked here for
    /// the same reason as the per-shelf bound beside it.
    /// </remarks>
    public int MaximumTitlesAcrossAllShelves { get; set; }

    /// <summary>
    /// Gets the server's identifiers of the users this plugin may not pass a want on for.
    /// </summary>
    /// <remarks>
    /// #98's first condition, and it is a list of who may NOT rather than of who
    /// may. Question 2 of that permission was answered on #2 on 2026-08-24 with
    /// asking following browsing, so an empty list is a server where whoever may
    /// browse may ask, and a list of who may would make a fresh install one
    /// where nobody can.
    ///
    /// Empty by default, which is that answer expressed as bytes rather than as
    /// a sentence: the setting an operator changes is one that takes something
    /// away, and a first install has taken nothing away from anybody.
    ///
    /// Get-only, which is what an XML deserialiser needs from a collection and
    /// what <c>CA2227</c> needs from a property. What reads it is
    /// <see cref="Seam.WhoMayAsk"/>, and nothing else: a second reader parsing
    /// these strings would be a second answer to the question that type exists
    /// to answer once.
    ///
    /// Strings rather than <see cref="Guid"/>s. The document is XML an operator
    /// edits by hand until #103 builds the page, and a value this build cannot
    /// read is refused with the entry quoted back rather than deserialised into
    /// something silently wrong.
    /// </remarks>
    public Collection<string> UsersRefusedTheAsk { get; } = new Collection<string>();

    /// <summary>
    /// Gets or sets a value indicating whether this plugin does its work at all.
    /// </summary>
    /// <remarks>
    /// #109's first condition. One setting stops every outbound call and every
    /// fetch the scheduler starts, and keeps the configuration and the
    /// catalogue: an operator diagnosing something, or sitting on a source's
    /// rate limit, or going away for a month, turns this off instead of
    /// uninstalling, which is what #108 is for and which takes the catalogue
    /// with it.
    ///
    /// What reads it is <see cref="Refresh.DiscoverRefreshTask.ShelvesFor"/>,
    /// and nothing else: a run under a configuration with this off is handed no
    /// shelves, so it asks no source and writes no document. The scheduled task
    /// itself still runs, and that is a reading of "all scheduled work" rather
    /// than an oversight. What a run does with no shelves is take what the
    /// documents already on disk hold past the retention, and the retention is
    /// a source's terms, which do not stop applying because an operator turned
    /// the plugin off. What stops is what spends: the requests and the writes.
    ///
    /// The surface stays, with what the catalogue holds. Off stops what costs a
    /// source and a database, and a surface reading what is already on disk
    /// costs neither; an operator who wants users to stop seeing it has #57's
    /// per-user control or the uninstall. Nothing in the surface reads a
    /// catalogue document yet, so today the surface answers empty either way,
    /// and the sentence above is the decision rather than an observation.
    ///
    /// Turning it back on resumes the schedule. Nothing is refetched because it
    /// was off: the documents were never taken, so the next run is the ordinary
    /// one, and the catalogue is as it was up to what the retention took while
    /// it was off.
    ///
    /// True by default and true for a document written before this property
    /// existed, because an XML deserialiser leaves an absent element at the
    /// initialiser's value. It is an initialiser rather than a line in the
    /// constructor because three pages under docs/ quote the properties above
    /// by line number, and a line added to the constructor moves every one of
    /// them. A document with the element spelled wrongly is a
    /// different case and is #105's third condition.
    ///
    /// There is no control for it on the configuration page, which carries no
    /// controls at all, so until #103 lands it is a hand edit of the document
    /// on disk like the settings above it. That is the same temporary reason
    /// the bounds are kept off the page, recorded in the same list.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Reads the two bounds as one value, refusing a pair that contradicts itself.
    /// </summary>
    /// <returns>The bounds this document declares.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown by <see cref="CatalogueBounds.Of"/> when either number is zero or
    /// negative, or when the total is smaller than one shelf's bound. The
    /// message names the setting as this document spells it, under the rule
    /// stated on this class.
    /// </exception>
    /// <remarks>
    /// A method rather than a property, so that a reflection walk over this
    /// class's settings, which is what <c>docs/configuration.md</c> is held
    /// against, sees the two numbers an operator sets and not a third thing
    /// derived from them.
    ///
    /// WHAT A REFUSAL OF A SETTING NAMES, and it is one rule for every refusal
    /// rather than a wording each one chooses. It names the setting as this
    /// document spells it, which is the property's own name and the word an
    /// operator finds in the file they edited; the value that was offered; and
    /// the range that is accepted. Where that range is read off something
    /// outside this plugin's own constants, a source's terms or the shipped set
    /// of shelves, the refusal names that thing too, because an operator with
    /// two sources cannot act on a ceiling that does not say whose it is. A
    /// refusal naming a method's parameter instead of the setting sends an
    /// operator searching the document for a word that is not in it, which is
    /// #105's first condition. The rule sits here, on the member that reads
    /// settings into a refusal, below the properties it is about: several pages
    /// under <c>docs/</c> quote those properties by line number, and a
    /// paragraph above them moves every one of those quotations.
    /// <see cref="CatalogueBounds"/> and <see cref="Seam.WhoMayAsk"/> are the
    /// refusals that follow it today.
    ///
    /// Where the message is read is the server's log rather than the dashboard,
    /// and that is a fact about the server rather than a choice here: a save the
    /// plugin refuses is answered with a status code and, outside a development
    /// host, with a fixed sentence, while the exception's own message is written
    /// to the log. The reading is on #105.
    /// </remarks>
    public CatalogueBounds Bounds() =>
        CatalogueBounds.Of(MaximumTitlesPerShelf, MaximumTitlesAcrossAllShelves);
}
