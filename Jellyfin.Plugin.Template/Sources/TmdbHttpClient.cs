namespace Jellyfin.Plugin.Template.Sources;

/// <summary>
/// The one client this plugin's calls to TMDb leave through, named.
/// </summary>
/// <remarks>
/// <para>
/// #45's second condition, on the reading taken on 2026-09-04: one injection
/// point, which is a single named client whose primary handler is the one thing
/// a test replaces. An unnamed client cannot be that. Every caller in a server
/// shares it, so a registration configuring its handler would configure
/// everybody's, and a registration configuring this plugin's would configure a
/// client this code never asks for.
/// </para>
/// <para>
/// It is a type of its own rather than a constant on either side, because both
/// sides read it and neither owns it: <see cref="TmdbSourceAdapter"/> asks the
/// factory for the client and <see cref="PluginServiceRegistrator"/> registers
/// it, and the failure of two spellings drifting apart is silent. A factory
/// answers a name nobody configured with a DEFAULT client rather than refusing,
/// so the plugin would go on calling out through a handler nobody chose while
/// every test supplying one still passed.
/// </para>
/// <para>
/// The name is not the assembly's. A rename is #14, and this string is a key in
/// a container rather than anything that reaches a source, an operator or a
/// disk, so it survives one without being derived from something that moves.
/// </para>
/// </remarks>
public static class TmdbHttpClient
{
    /// <summary>
    /// The name the factory hands this plugin's TMDb client back under.
    /// </summary>
    public const string Name = "discover.tmdb";
}
