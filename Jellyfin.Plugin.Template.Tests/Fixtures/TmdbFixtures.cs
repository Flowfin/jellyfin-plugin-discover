using System;
using System.Text;

namespace Jellyfin.Plugin.Template.Tests.Fixtures;

/// <summary>
/// The response shapes the first adapter has to survive, as bytes a test can hand it.
/// </summary>
/// <remarks>
/// Every one of these was written by hand rather than captured. Nothing here
/// came off the source, so no provenance line is owed on any of them and the
/// question of what claim would let a real value sit in a public repository
/// does not arise. What is real is the shape: the field names, the nesting and
/// the status the shape arrives beside, which is what a parser is judged
/// against. Every value is synthetic, and the titles are named so that a reader
/// finding one in an error message can tell at once it is not a film.
///
/// The position these follow is in <see href="README.md">the page beside this
/// file</see>, which is #48's, and the reason they are base64 rather than JSON
/// on disk is the same page's: this repository declares no normalisation, so
/// what a clone does to a carriage return between the working tree and the
/// index is a property of that clone. A truncated body is the case where the
/// difference is the whole fixture, and base64 is the form no line-ending rule
/// can reach.
///
/// The set covers the six shapes the fifth condition on #48 asks for and three
/// more that the adapter's own mapping needs: a series page, whose fields the
/// source spells differently from a film's; a page whose reported total
/// contradicts what it carries; and a body from something that answered instead
/// of the source.
/// </remarks>
internal static class TmdbFixtures
{
    /// <summary>
    /// A well-formed page of films.
    /// </summary>
    /// <remarks>
    /// Two entries, and the second is the one worth having. Every optional
    /// field on it is present and says nothing: an empty description, a null
    /// artwork path, an empty release date, and an original title equal to the
    /// title. Those are the four absences the source spells as presence, and a
    /// mapping that carried them through would put an empty panel and a broken
    /// picture in front of a user.
    /// </remarks>
    public const string MoviePage = "eyJwYWdlIjoxLCJyZXN1bHRzIjpbeyJhZHVsdCI6ZmFsc2UsImlkIjoxMDAwMDEsInRpdGxlIjoiQSBGaWxtIFRoYXQgRG9lcyBOb3QgRXhpc3QiLCJvcmlnaW5hbF90aXRsZSI6IkEgRmlsbSBUaGF0IERvZXMgTm90IEV4aXN0LCBBcyBJdHMgT3duIExhbmd1YWdlIFNwZWxscyBJdCIsIm92ZXJ2aWV3IjoiQSBzeW50aGV0aWMgZGVzY3JpcHRpb24gc3RhbmRpbmcgaW4gZm9yIHRoZSBvbmUgdGhlIHNvdXJjZSByZXR1cm5lZC4iLCJwb3N0ZXJfcGF0aCI6Ii9zeW50aGV0aWMtcG9zdGVyLW9uZS5qcGciLCJyZWxlYXNlX2RhdGUiOiIyMDE5LTA3LTA0Iiwidm90ZV9hdmVyYWdlIjo2LjR9LHsiYWR1bHQiOmZhbHNlLCJpZCI6MTAwMDAyLCJ0aXRsZSI6IkFub3RoZXIgRmlsbSBUaGF0IERvZXMgTm90IEV4aXN0Iiwib3JpZ2luYWxfdGl0bGUiOiJBbm90aGVyIEZpbG0gVGhhdCBEb2VzIE5vdCBFeGlzdCIsIm92ZXJ2aWV3IjoiIiwicG9zdGVyX3BhdGgiOm51bGwsInJlbGVhc2VfZGF0ZSI6IiIsInZvdGVfYXZlcmFnZSI6MH1dLCJ0b3RhbF9wYWdlcyI6MSwidG90YWxfcmVzdWx0cyI6Mn0K";

    /// <summary>
    /// A well-formed page of series.
    /// </summary>
    /// <remarks>
    /// The same page as a film's with three field names changed, which is the
    /// whole of why it is a fixture of its own. A mapping that read a film's
    /// names on this page would return a page of titles with no name and drop
    /// every one of them, and the outcome would still be
    /// <c>Answered</c> with nothing in it, which is indistinguishable from a
    /// shelf the source genuinely has nothing for.
    /// </remarks>
    public const string SeriesPage = "eyJwYWdlIjoxLCJyZXN1bHRzIjpbeyJpZCI6MjAwMDAxLCJuYW1lIjoiQSBTZXJpZXMgVGhhdCBEb2VzIE5vdCBFeGlzdCIsIm9yaWdpbmFsX25hbWUiOiJBIFNlcmllcyBUaGF0IERvZXMgTm90IEV4aXN0LCBBcyBJdHMgT3duIExhbmd1YWdlIFNwZWxscyBJdCIsIm92ZXJ2aWV3IjoiQSBzeW50aGV0aWMgZGVzY3JpcHRpb24gc3RhbmRpbmcgaW4gZm9yIHRoZSBvbmUgdGhlIHNvdXJjZSByZXR1cm5lZC4iLCJwb3N0ZXJfcGF0aCI6Ii9zeW50aGV0aWMtcG9zdGVyLXR3by5qcGciLCJmaXJzdF9haXJfZGF0ZSI6IjIwMjEtMTEtMzAifV0sInRvdGFsX3BhZ2VzIjoxLCJ0b3RhbF9yZXN1bHRzIjoxfQo=";

    /// <summary>
    /// A page the source answered with nothing on.
    /// </summary>
    /// <remarks>
    /// The shape #63 turns on. The source was asked, it answered, and it has
    /// nothing for this question, which is a legitimately empty shelf rather
    /// than any of the three failures.
    /// </remarks>
    public const string EmptyPage = "eyJwYWdlIjoxLCJyZXN1bHRzIjpbXSwidG90YWxfcGFnZXMiOjAsInRvdGFsX3Jlc3VsdHMiOjB9Cg==";

    /// <summary>
    /// A page carrying a field this plugin does not know, and three entries it cannot map.
    /// </summary>
    /// <remarks>
    /// Four entries and one title comes out. The first carries a field added
    /// after this parser was written, nested so that a reader that walked the
    /// object would meet it. The second has no identifier, the third has no
    /// title, and the fourth is a string where the source puts an object. Each
    /// of the last three is dropped, and the answer is a shorter set rather
    /// than a hole in one or a refusal for the whole page.
    /// </remarks>
    public const string PageWithAnUnknownFieldAndEntriesThatCannotBeMapped = "eyJwYWdlIjoxLCJyZXN1bHRzIjpbeyJpZCI6MTAwMDAzLCJ0aXRsZSI6IkEgRmlsbSBDYXJyeWluZyBBIEZpZWxkIFRoaXMgUGx1Z2luIERvZXMgTm90IEtub3ciLCJhX2ZpZWxkX2FkZGVkX2xhdGVyIjp7Im5lc3RlZCI6dHJ1ZX0sIm92ZXJ2aWV3IjoiQSBzeW50aGV0aWMgZGVzY3JpcHRpb24uIiwicmVsZWFzZV9kYXRlIjoiMjAyNC0wMS0wMiJ9LHsidGl0bGUiOiJBIEZpbG0gVGhlIFNvdXJjZSBHYXZlIE5vIElkZW50aWZpZXIgRm9yIiwicmVsZWFzZV9kYXRlIjoiMjAyNC0wMS0wMyJ9LHsiaWQiOjEwMDAwNCwicmVsZWFzZV9kYXRlIjoiMjAyNC0wMS0wNCJ9LCJhIHN0cmluZyB3aGVyZSB0aGUgc291cmNlIHVzdWFsbHkgcHV0cyBhbiBvYmplY3QiXSwidG90YWxfcGFnZXMiOjEsInRvdGFsX3Jlc3VsdHMiOjR9Cg==";

    /// <summary>
    /// A body that stops in the middle of a title.
    /// </summary>
    /// <remarks>
    /// What a connection dropped mid-response leaves behind. It is valid JSON
    /// for its first seventy bytes, which is the case a parser written to look
    /// at the start of a body passes and a parser written to read the whole of
    /// one refuses.
    /// </remarks>
    public const string TruncatedBody = "eyJwYWdlIjoxLCJyZXN1bHRzIjpbeyJpZCI6MTAwMDA1LCJ0aXRsZSI6IkEgRmlsbSBXaG9zZSBCb2R5IFdhcyBDdXQgT2ZmIiwicmVsZQ==";

    /// <summary>
    /// The body the source sends beside a refusal to answer at all.
    /// </summary>
    /// <remarks>
    /// The source states a refusal as a message inside an object rather than as
    /// a status alone. It arrives beside 401 where the credential is the
    /// problem, which is the case this adapter reads as a source that has not
    /// been set up.
    /// </remarks>
    public const string RefusalBody = "eyJzdWNjZXNzIjpmYWxzZSwic3RhdHVzX2NvZGUiOjcsInN0YXR1c19tZXNzYWdlIjoiQSBzeW50aGV0aWMgcmVmdXNhbCBzdGFuZGluZyBpbiBmb3IgdGhlIHdvcmRzIHRoZSBzb3VyY2UgdXNlcy4ifQo=";

    /// <summary>
    /// The body the source sends when it is being asked too often.
    /// </summary>
    /// <remarks>
    /// The same shape as a refusal with a different code in it, which is why
    /// the outcome is decided by the status rather than by the body. What the
    /// body is good for is the source's own words, and #79 asks for those where
    /// there are any.
    /// </remarks>
    public const string RateLimitBody = "eyJzdWNjZXNzIjpmYWxzZSwic3RhdHVzX2NvZGUiOjI1LCJzdGF0dXNfbWVzc2FnZSI6IkEgc3ludGhldGljIHJhdGUgcmVmdXNhbCBzdGFuZGluZyBpbiBmb3IgdGhlIHdvcmRzIHRoZSBzb3VyY2UgdXNlcy4ifQo=";

    /// <summary>
    /// A page whose reported total is smaller than the page it describes.
    /// </summary>
    /// <remarks>
    /// A source contradicting itself. It is a fixture because the record that
    /// carries an answer refuses that pair rather than storing it, so a mapping
    /// passing it through would throw out of the adapter, and #73 asks that
    /// nothing about a source's answer arrive at a caller as an exception.
    /// </remarks>
    public const string PageWhoseTotalContradictsIt = "eyJwYWdlIjoxLCJyZXN1bHRzIjpbeyJpZCI6MTAwMDA2LCJ0aXRsZSI6IkEgRmlsbSBPbiBBIFBhZ2UgTG9uZ2VyIFRoYW4gSXRzIE93biBUb3RhbCJ9LHsiaWQiOjEwMDAwNywidGl0bGUiOiJBIFNlY29uZCBGaWxtIE9uIFRoYXQgUGFnZSJ9XSwidG90YWxfcGFnZXMiOjEsInRvdGFsX3Jlc3VsdHMiOjF9Cg==";

    /// <summary>
    /// A page from something that answered instead of the source.
    /// </summary>
    /// <remarks>
    /// A gateway, a proxy or a captive portal, which is what a server behind
    /// one meets rather than anything the source sent. It is here so that the
    /// message shown to an operator is the source's words or nothing, and never
    /// the first line of somebody else's markup.
    /// </remarks>
    public const string BodyFromSomethingThatIsNotTheSource = "PGh0bWw+PGhlYWQ+PHRpdGxlPjUwMjwvdGl0bGU+PC9oZWFkPjxib2R5PkFuc3dlcmVkIGJ5IHNvbWV0aGluZyB0aGF0IGlzIG5vdCB0aGUgc291cmNlLjwvYm9keT48L2h0bWw+";

    /// <summary>
    /// Reads a fixture back into the bytes a source would have sent.
    /// </summary>
    /// <param name="fixture">One of the constants above.</param>
    /// <returns>The body as text.</returns>
    /// <remarks>
    /// The decode is here rather than in each test so that the bytes reaching
    /// the adapter come from one place. It reads the body as UTF-8, which is
    /// what the source sends and what the transport in the adapter produces
    /// from a response.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fixture"/> is null.</exception>
    public static string Body(string fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        return Encoding.UTF8.GetString(Convert.FromBase64String(fixture));
    }
}
