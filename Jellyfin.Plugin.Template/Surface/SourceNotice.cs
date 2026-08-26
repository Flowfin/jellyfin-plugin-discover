namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// The notice a metadata source's terms require an application to display,
/// held once so that every rendering of it is the same bytes.
/// </summary>
/// <remarks>
/// One home rather than a copy per rendering, which is #76's last condition.
/// The failure it exists against is not a missing notice, which somebody
/// notices: it is two renderings that drifted apart, where the one a user reads
/// is the one nobody looked at, and where the text is a clause rather than
/// wording anybody is free to improve.
///
/// It sits beside the surface rather than beside the adapter because the
/// surface is the rendering that can take the text at run time, and because
/// #76 declares the configuration page, the surface and the test project as its
/// boundary. The day a second source ships a notice, this becomes a lookup
/// keyed by source and moves to where the sources live.
///
/// THE CONFIGURATION PAGE CANNOT TAKE IT FROM HERE AND CARRIES A COPY. That
/// page is a static asset embedded in the assembly, with no substitution step
/// between this constant and the bytes an administrator's browser receives, so
/// what stands between the two copies is a test rather than a construction:
/// <c>ConfigurationPageTests.ThePageCarriesTheSourcesNoticeVerbatim</c> reads
/// the shipped page and asserts this text is in it. That is a weaker thing than
/// one rendering and it is stated rather than papered over.
/// </remarks>
public static class SourceNotice
{
    /// <summary>
    /// The notice TMDB's API terms of use require, section 3, read on
    /// 2026-08-06 and quoted on <c>docs/sources/tmdb.md</c>.
    /// </summary>
    /// <remarks>
    /// The clause writes the subject as a choice - website, program, service,
    /// application, product - and one of the five has to be taken for the
    /// sentence to be a sentence. It is `application`, because that is the word
    /// the same section uses for the thing being licensed when it states the
    /// prominence requirement beside this one.
    ///
    /// Nothing else is edited. The commas, the three verbs and the absence of
    /// the article before the second TMDB are the clause's, and a sentence that
    /// reads better is a sentence that is no longer the one the terms ask for.
    /// </remarks>
    public const string Tmdb =
        "This application uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.";
}
