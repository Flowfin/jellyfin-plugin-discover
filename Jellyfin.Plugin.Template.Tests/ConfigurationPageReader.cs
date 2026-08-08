using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Reads out of the configuration page's markup the three things a browser was
/// going to be asked about: which identifiers it names, which controls it
/// offers, and which hosts outside the server it reaches for.
/// </summary>
/// <remarks>
/// A pattern reader rather than an HTML parser. Adding a parser would put a
/// package in the graph for a file this repository writes itself, so the bound
/// is stated instead of bought: each reader below matches text, and a reference
/// assembled at run time out of parts is invisible to all three of them. That
/// bound is why ConfigurationPageReaderTests exists. A reader that found
/// nothing would make every assertion in ConfigurationPageTests pass for ever,
/// so each one is shown finding the thing it looks for.
/// </remarks>
internal static partial class ConfigurationPageReader
{
    /// <summary>
    /// Every identifier the markup names, in the eight-four-four-four-twelve
    /// spelling the server and the packaging metadata both use.
    /// </summary>
    /// <param name="markup">The page's markup.</param>
    /// <returns>The identifiers, in the order they appear, without repeats.</returns>
    internal static IReadOnlyList<string> IdentifiersIn(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);

        return Identifier()
            .Matches(markup)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// The name of every control on the page a user could set, taken from the
    /// id and name attributes of every input, select and textarea.
    /// </summary>
    /// <param name="markup">The page's markup.</param>
    /// <returns>The control names, without repeats.</returns>
    /// <remarks>
    /// Both attributes are read because the server's own pages address a
    /// control by either, so a page written with one spelling and checked for
    /// the other would report no controls and pass.
    /// </remarks>
    internal static IReadOnlyList<string> NamedControlsIn(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);

        return ControlElement()
            .Matches(markup)
            .SelectMany(element => ControlName().Matches(element.Value))
            .Select(attribute => attribute.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Every host the markup reaches for that is not the server serving the
    /// page.
    /// </summary>
    /// <param name="markup">The page's markup.</param>
    /// <returns>The host names, without repeats.</returns>
    /// <remarks>
    /// Any absolute reference counts, and so does a protocol-relative one,
    /// because a page served by the server has no reason to name a host at all.
    /// That makes the answer a list rather than a comparison against a list of
    /// hosts somebody would have to keep current.
    /// </remarks>
    internal static IReadOnlyList<string> HostsOutsideTheServerIn(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);

        var absolute = AbsoluteReference().Matches(markup).Select(match => match.Groups[1].Value);
        var protocolRelative = ProtocolRelativeReference().Matches(markup).Select(match => match.Groups[1].Value);

        return absolute
            .Concat(protocolRelative)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex Identifier();

    [GeneratedRegex("<(?:input|select|textarea)\\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ControlElement();

    [GeneratedRegex("\\b(?:id|name)\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex ControlName();

    [GeneratedRegex("https?://([^\\s\"'<>/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex AbsoluteReference();

    [GeneratedRegex("(?:src|href|action|formaction|data-src)\\s*=\\s*[\"']//([^/\"'\\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ProtocolRelativeReference();
}
