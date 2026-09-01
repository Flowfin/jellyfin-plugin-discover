using System;

namespace Jellyfin.Plugin.Template.Sources;

/// <summary>
/// Which language a source is asked to answer in, and which region it is asked about.
/// </summary>
/// <remarks>
/// <para>
/// #81's first condition asks that a language and a region reach the source.
/// They are one value here rather than two arguments because an adapter that
/// took two strings would be an adapter every caller could hand a language to
/// and forget the region beside it, and because the shape a source will accept
/// is the same judgement for both: a tag, not free text.
/// </para>
/// <para>
/// It is not on <see cref="SourceQuery"/>. That type carries what a shelf asks
/// for, and this is not the shelf's to decide: the origin is the server's own
/// metadata language, which #81 records and this type does not read. Putting a
/// field on the query would fix the wrong origin, which is what the remark on
/// that type already says.
/// </para>
/// <para>
/// A MALFORMED VALUE IS REFUSED RATHER THAN DROPPED, and that is the opposite
/// of how this plugin treats a source's own bytes. A score that is not a score
/// becomes an absence because it arrived from a third party and a refresh that
/// threw on one would lose the page. A language tag arrives from this server's
/// own configuration, so dropping it silently would put every title back in the
/// source's default language with nothing anywhere saying that a setting was
/// ignored, which is the failure #81 exists against. Refusing it is #105's
/// direction applied one type earlier.
/// </para>
/// <para>
/// The shape admitted is deliberately narrower than the standard it comes from.
/// What this plugin needs is a value that cannot change the meaning of a URL,
/// and the whole of <c>BCP 47</c> is a great deal more than a source asks for,
/// so a script subtag or a private-use extension is refused here rather than
/// escaped on the way into a query. What that costs is an operator on a server
/// whose metadata language carries one, and the message says which part was
/// refused.
/// </para>
/// </remarks>
public sealed record SourceLocale
{
    private SourceLocale(string? language, string? region)
    {
        Language = language;
        Region = region;
    }

    /// <summary>
    /// Gets the value that asks for neither, which is what every source answers today.
    /// </summary>
    /// <remarks>
    /// Named rather than left as a null, because a caller that has no language
    /// to give and a caller that forgot to pass one write the same thing
    /// otherwise. What a source does when it is asked for no language is its
    /// own default, which for the addresses in <c>docs/source-api/tmdb.md</c>
    /// is <c>en-US</c> on all six.
    /// </remarks>
    public static SourceLocale Unstated { get; } = new SourceLocale(null, null);

    /// <summary>
    /// Gets the language tag a source is asked to answer in, or null where none is stated.
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// Gets the region a source is asked about, or null where none is stated.
    /// </summary>
    public string? Region { get; }

    /// <summary>
    /// Reads a language and a region into the value an adapter is handed.
    /// </summary>
    /// <param name="language">
    /// The language to ask in, as <c>ll</c> or <c>ll-CC</c>, or null to state none.
    /// </param>
    /// <param name="region">
    /// The region to ask about, as <c>CC</c>, or null to state none.
    /// </param>
    /// <returns>The pair, with <see cref="Unstated"/> where neither was given.</returns>
    /// <remarks>
    /// A region with no language is admitted. The two are separate parameters
    /// at the source, a region narrows what is popular rather than what it is
    /// called, and an operator who wants the source's default language and
    /// their own country is asking for something the source offers.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when either value is given and is not of the shape above.
    /// </exception>
    public static SourceLocale Of(string? language, string? region)
    {
        var wanted = Blank(language) ? null : language;
        var about = Blank(region) ? null : region;

        if (wanted is null && about is null)
        {
            return Unstated;
        }

        if (wanted is not null && !IsLanguageTag(wanted))
        {
            throw new ArgumentException(
                $"A source is asked to answer in a language written as 'll' or 'll-CC'. '{wanted}' is neither, and a value this plugin cannot vouch for would reach a query string.",
                nameof(language));
        }

        if (about is not null && !IsCountryCode(about))
        {
            throw new ArgumentException(
                $"A source is asked about a region written as 'CC'. '{about}' is not, and a value this plugin cannot vouch for would reach a query string.",
                nameof(region));
        }

        return new SourceLocale(wanted, about);
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Says whether a value is a language tag this plugin is willing to send.
    /// </summary>
    /// <param name="value">What was given.</param>
    /// <returns><see langword="true"/> where it is <c>ll</c> or <c>ll-CC</c>.</returns>
    /// <remarks>
    /// Read character by character rather than by a pattern, because the
    /// characters admitted are the whole of the property: every one of them is
    /// already safe inside a query string, so the check and the escaping are
    /// one thing rather than two that can disagree.
    /// </remarks>
    private static bool IsLanguageTag(string value)
    {
        if (value.Length != 2 && value.Length != 5)
        {
            return false;
        }

        if (!Lower(value[0]) || !Lower(value[1]))
        {
            return false;
        }

        return value.Length == 2
            || (value[2] == '-' && Upper(value[3]) && Upper(value[4]));
    }

    /// <summary>
    /// Says whether a value is a country code this plugin is willing to send.
    /// </summary>
    /// <param name="value">What was given.</param>
    /// <returns><see langword="true"/> where it is two upper-case letters.</returns>
    private static bool IsCountryCode(string value) =>
        value.Length == 2 && Upper(value[0]) && Upper(value[1]);

    private static bool Lower(char character) => character is >= 'a' and <= 'z';

    private static bool Upper(char character) => character is >= 'A' and <= 'Z';
}
