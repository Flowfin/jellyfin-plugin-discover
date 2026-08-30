using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Template.Configuration;

namespace Jellyfin.Plugin.Template.Seam;

/// <summary>
/// Which users this plugin refuses to pass a want on for.
/// </summary>
/// <remarks>
/// #98 is what this exists for, and the first thing to say is why it is this
/// plugin's own configuration rather than a server permission. Seeing the
/// surface is a per-user server setting and this plugin builds nothing for it,
/// which is #57. Asking cannot be one: the server's permission set is a closed
/// enumeration, a plugin cannot add a member to it, and the one member about
/// channels is the visibility above. That reading is on #98 with the commands
/// that produced it and is not repeated here.
///
/// So the two are separate in a stronger sense than "two settings". One is the
/// server's and one is this plugin's, and this type is only the second.
///
/// THE DEFAULT IS PERMISSIVE AND IT IS NOT THIS TYPE'S CHOICE. Question 2 of the
/// permission was answered on #2 on 2026-08-24: asking follows browsing, so
/// whoever may browse may ask, and the operator's control restricts rather than
/// enables. That is why what is stored is a list of the users who may NOT ask: a
/// list of the users who may would make a fresh install one where nobody can,
/// which is the plugin appearing broken rather than being cautious.
///
/// It follows that this type never answers whether a user may BROWSE. Nothing
/// here reads the server's permission, and a user this type does not refuse is
/// one whose ability to ask is exactly their ability to browse, because this
/// plugin adds no second restriction to them.
///
/// ADMINISTRATOR STATUS IS NOT USED AS A PROXY, which is #98's fifth condition
/// and is a decision rather than an omission. The reasoning is one line: on a
/// household server there is one administrator, so a permission keyed on that
/// flag would admit one person, and admitting one person is the opposite of what
/// an operator installs this for.
///
/// A LIST THIS BUILD CANNOT READ REFUSES EVERYBODY. An entry that is not a user
/// identifier, and a configuration that is absent altogether, both leave this
/// plugin unable to say whom the operator refused. Honouring a want under a list
/// it cannot read is the "silently honoured" half of #98's second condition, so
/// the answer fails closed and says which of the two it was.
/// </remarks>
public sealed class WhoMayAsk
{
    private readonly HashSet<Guid> _refused;
    private readonly string? _refusesEverybodyBecause;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhoMayAsk"/> class.
    /// </summary>
    /// <param name="refused">The users the operator listed.</param>
    /// <param name="refusesEverybodyBecause">Why no want may be passed on at all, or null where one may.</param>
    private WhoMayAsk(HashSet<Guid> refused, string? refusesEverybodyBecause)
    {
        _refused = refused;
        _refusesEverybodyBecause = refusesEverybodyBecause;
    }

    /// <summary>
    /// Gets the answer a fresh install carries, where the operator has listed nobody.
    /// </summary>
    /// <remarks>
    /// Named rather than left as an empty list a caller assembles, because this
    /// is the state every install starts in and the state
    /// <see cref="WantHandover"/> falls back to when nobody handed it a source.
    /// A reader meeting it should read "the operator has refused nobody", not
    /// "the permission was not wired up".
    /// </remarks>
    public static WhoMayAsk NobodyIsRefused { get; } = new WhoMayAsk(new HashSet<Guid>(), null);

    /// <summary>
    /// Gets why no want may be passed on at all, or null where the list was read.
    /// </summary>
    /// <remarks>
    /// A sentence rather than a flag, because the two causes are different
    /// things to an operator: one is a mistyped entry they can correct and one
    /// is a plugin with no configuration to read. <see cref="WantHandover"/>
    /// puts it in the log, so what an operator is told names the cause rather
    /// than only the refusal.
    /// </remarks>
    public string? RefusesEverybodyBecause => _refusesEverybodyBecause;

    /// <summary>
    /// Gets the users the operator listed, which is empty on a fresh install.
    /// </summary>
    public IReadOnlyCollection<Guid> Refused => _refused;

    /// <summary>
    /// Reads the list off a configuration, or refuses everybody where there is none.
    /// </summary>
    /// <param name="configuration">What the operator saved, or null where there is none to read.</param>
    /// <returns>The answer this build can give for that document.</returns>
    /// <remarks>
    /// Static and taking the document rather than reaching for the plugin
    /// instance, for the reason <see cref="Refresh.DiscoverRefreshTask.ShelvesFor"/>
    /// gives for the same shape: the instance is a static shared by every test
    /// in a run and no test can hold it still, so the part of the server's own
    /// path that can be asserted is the part that takes a document.
    ///
    /// It never throws. What refuses a mistyped entry is
    /// <see cref="ThrowIfAnEntryIsUnreadable"/> at the moment the document is
    /// saved; a document that reached disk another way is met here at the moment
    /// a want is offered, and a handover that threw would break the gesture over
    /// somebody's typing.
    /// </remarks>
    public static WhoMayAsk From(PluginConfiguration? configuration)
    {
        if (configuration is null)
        {
            return new WhoMayAsk(
                new HashSet<Guid>(),
                "there is no configuration on this server to read the list from");
        }

        var refused = new HashSet<Guid>();

        foreach (var entry in configuration.UsersRefusedTheAsk)
        {
            if (!Guid.TryParse(entry, out var user) || user == Guid.Empty)
            {
                return new WhoMayAsk(
                    new HashSet<Guid>(),
                    FormattableString.Invariant(
                        $"the list carries {Unreadable(entry)}, which is not a user identifier, so this build cannot tell whom it names"));
            }

            refused.Add(user);
        }

        return new WhoMayAsk(refused, null);
    }

    /// <summary>
    /// Refuses a configuration whose list this build could not act on.
    /// </summary>
    /// <param name="configuration">The configuration being saved.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when an entry is not a user identifier, or is the empty one. The
    /// message names the entry, because an operator's next action is to correct
    /// it and they cannot without seeing which of several it was.
    /// </exception>
    /// <remarks>
    /// The same shape <see cref="ConfigurationSchema.ThrowIfUnknown"/> takes and
    /// for the same reason: a document that cannot be acted on is refused before
    /// it reaches disk rather than at the moment a user makes a gesture. The two
    /// are separate calls rather than one, because they refuse different things
    /// about the same document and a caller reading the stack should see which.
    ///
    /// It is the save path only. A file edited on disk while the server is down
    /// is never handed to this, which is why <see cref="From"/> fails closed on
    /// the same bytes rather than trusting that this ran.
    /// </remarks>
    public static void ThrowIfAnEntryIsUnreadable(PluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (var entry in configuration.UsersRefusedTheAsk)
        {
            if (!Guid.TryParse(entry, out var user) || user == Guid.Empty)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"{nameof(PluginConfiguration.UsersRefusedTheAsk)} carries {Unreadable(entry)}, which is not a user identifier. Every entry is the server's own identifier for one user, and a list holding one this build cannot read is a list none of whose refusals can be applied."),
                    nameof(configuration));
            }
        }
    }

    /// <summary>
    /// Answers whether this plugin refuses to pass a want on for one user.
    /// </summary>
    /// <param name="user">The server's identifier for the user who made the gesture.</param>
    /// <returns><see langword="true"/> where the want may not cross the seam.</returns>
    /// <remarks>
    /// Total, and it says nothing about whether the user may browse. A false
    /// here is this plugin declining to restrict rather than this plugin
    /// granting anything, which is what "asking follows browsing" means once
    /// there is code to point at.
    /// </remarks>
    public bool Refuses(Guid user) => _refusesEverybodyBecause is not null || _refused.Contains(user);

    /// <summary>
    /// How an entry that could not be read is quoted back at an operator.
    /// </summary>
    /// <param name="entry">The entry as the document carried it.</param>
    /// <returns>The entry in quotation marks, or a word for an absent one.</returns>
    /// <remarks>
    /// A null or blank entry quoted as <c>""</c> reads as an entry that is not
    /// there at all, and an operator looking for it in the document would find
    /// nothing to correct. It is named instead.
    /// </remarks>
    private static string Unreadable(string? entry) =>
        string.IsNullOrWhiteSpace(entry)
            ? "a blank entry"
            : string.Create(CultureInfo.InvariantCulture, $"the entry \"{entry}\"");
}
