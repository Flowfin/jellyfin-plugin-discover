using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Server;

/// <summary>
/// What this plugin asks the server's own library, in this plugin's words.
/// </summary>
/// <remarks>
/// <para>
/// A discover page shows titles a server does not have, so the first question
/// a refresh has to be able to ask is which of the titles a source just offered
/// the server already holds. That question reaches the server's library, and
/// this interface is where it stops being the server's vocabulary and becomes
/// this plugin's, for the reason <see cref="Surface.IDiscoverSurface"/> exists
/// one seam over: everything above the boundary is testable against a fake and
/// the server's assembly stays on one side of it.
/// </para>
/// <para>
/// THE SIGNATURE IS THE RULE THAT THE LOOKUP IS BY IDENTIFIER, rather than a
/// sentence somebody has to keep. #89's second condition asks that the
/// comparison never go by title text, and a method that is handed a
/// <see cref="DiscoverTitle"/> could read its name whatever the prose beside it
/// said. This one is handed an identity and a kind, so no name crosses the
/// seam at all and an implementation that wanted to match on text has nothing
/// to match with.
/// </para>
/// <para>
/// It answers with a count rather than with a yes, because the two kinds this
/// plugin carries are owned differently and one number expresses both. A film
/// is held or it is not. A series the server carries with no episode is a row
/// in a library rather than something a household can watch, and #2's answer of
/// 2026-08-24 is that a series counts as owned once the server carries it with
/// at least one episode. So a part is the film for a movie and an episode for a
/// series, and the rule above both is that a title is owned when the server
/// holds at least one part of it.
/// </para>
/// <para>
/// Nothing here says when the question is asked. The answer of 2026-08-24 puts
/// the price at refresh time rather than while a user is browsing, and
/// <see cref="Refresh.CatalogueRefresh"/> is what holds that.
/// </para>
/// </remarks>
public interface IServerLibrary
{
    /// <summary>
    /// Says how many parts of one title this server holds.
    /// </summary>
    /// <param name="identity">The title's identifiers, which is the whole of what is compared.</param>
    /// <param name="kind">Whether the title is a film or a series, which is what a part means.</param>
    /// <returns>
    /// The number of parts the server holds: one or zero for a film, and the
    /// number of episodes it carries for a series. Zero means the server does
    /// not have the title in the sense a user would mean it.
    /// </returns>
    int PartsHeld(DiscoverTitleIdentity identity, DiscoverTitleKind kind);
}
