using System.Collections.Generic;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Server;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A server library, holding what a test put in it and recording every question it was asked.
/// </summary>
/// <remarks>
/// Hand written rather than produced by a mocking framework, for the reason the
/// other fakes here are: the interface has one member, and what a test reads is
/// C# rather than a second vocabulary.
///
/// It records the questions as well as answering them, because #89's second
/// condition is about what was asked rather than about what came back. A test
/// proving that two titles sharing a name are two different questions has to be
/// able to read the questions.
///
/// A title it was given nothing for is answered with zero, which is a server
/// that does not hold it.
/// </remarks>
internal sealed class LibraryThatHoldsWhatATestGaveIt : IServerLibrary
{
    private readonly Dictionary<(DiscoverTitleIdentity Identity, DiscoverTitleKind Kind), int> _held
        = new Dictionary<(DiscoverTitleIdentity Identity, DiscoverTitleKind Kind), int>();

    private readonly List<(DiscoverTitleIdentity Identity, DiscoverTitleKind Kind)> _asked
        = new List<(DiscoverTitleIdentity Identity, DiscoverTitleKind Kind)>();

    /// <summary>
    /// Gets every question this library was asked, in the order it was asked them.
    /// </summary>
    public IReadOnlyList<(DiscoverTitleIdentity Identity, DiscoverTitleKind Kind)> Asked => _asked;

    /// <summary>
    /// Says this server holds a title, and how many parts of it.
    /// </summary>
    /// <param name="title">The title the server has.</param>
    /// <param name="parts">
    /// How many parts of it: the film itself for a movie, and the episode count
    /// for a series. Zero is a server carrying the row and nothing under it,
    /// which is the case #2's answer of 2026-08-24 separates from owning it.
    /// </param>
    /// <returns>The same fake, so a test can arrange several titles in one expression.</returns>
    public LibraryThatHoldsWhatATestGaveIt Holding(DiscoverTitle title, int parts = 1)
    {
        _held[(title.Identity, title.Kind)] = parts;

        return this;
    }

    /// <inheritdoc />
    public int PartsHeld(DiscoverTitleIdentity identity, DiscoverTitleKind kind)
    {
        _asked.Add((identity, kind));

        return _held.TryGetValue((identity, kind), out var parts) ? parts : 0;
    }
}
