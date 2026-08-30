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
    /// Reads the two bounds as one value, refusing a pair that contradicts itself.
    /// </summary>
    /// <returns>The bounds this document declares.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown by <see cref="CatalogueBounds.Of"/> when either number is zero or
    /// negative, or when the total is smaller than one shelf's bound.
    /// </exception>
    /// <remarks>
    /// A method rather than a property, so that a reflection walk over this
    /// class's settings, which is what <c>docs/configuration.md</c> is held
    /// against, sees the two numbers an operator sets and not a third thing
    /// derived from them.
    /// </remarks>
    public CatalogueBounds Bounds() =>
        CatalogueBounds.Of(MaximumTitlesPerShelf, MaximumTitlesAcrossAllShelves);
}
