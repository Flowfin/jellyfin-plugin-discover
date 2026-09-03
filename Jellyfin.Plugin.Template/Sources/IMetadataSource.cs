using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;

namespace Jellyfin.Plugin.Template.Sources;

/// <summary>
/// Everything a metadata source has to answer, in this plugin's own vocabulary.
/// </summary>
/// <remarks>
/// Small on purpose. A shelf asks one source for one page of titles matching a
/// named question and gets <see cref="DiscoverTitle"/> records back. Everything
/// the sources differ in - authentication, paging, field names, language
/// handling, what a rate-limit response looks like, what an error body is - is
/// the adapter's problem, because any of it reaching a caller means adding a
/// second source changes the first one's callers.
///
/// Nothing in the signatures here comes from a source's client library or from
/// the server. That is the property #73's first condition asks for, and
/// <c>SourceInterfaceTests</c> holds it by reading the interface with reflection
/// rather than by anybody looking: a parameter or a return type from another
/// assembly reds the suite the day it is added, which is the day a wire type
/// starts spreading.
///
/// Implementing this interface is what makes a type a source, and nothing about
/// it turns on what the file is called. That was not true until #387 and #394:
/// `no-network-outside-source-adapter` excepted the suffix <c>*SourceAdapter.cs</c>
/// and `source-terms` read the same suffix to decide which terms page was owed,
/// so an implementation landing under another name widened one rule and walked
/// out of the other. Both read a path or this interface now. The first
/// implementation is #74.
///
/// Each adapter carries the constraints its own source imposes, which is #75.
/// The key it uses is #77 and never the server's, refused by
/// `no-server-provider-key`.
/// </remarks>
public interface IMetadataSource
{
    /// <summary>
    /// Gets which body this source speaks for.
    /// </summary>
    /// <remarks>
    /// Named rather than inferred from the identifiers in a response, because a
    /// response commonly carries identifiers from bodies other than the one
    /// that answered. What a caller needs this for is the key, the terms and
    /// the rate budget, all of which belong to whoever was asked.
    /// </remarks>
    MetadataSource Source { get; }

    /// <summary>
    /// Gets the longest this source's terms allow anything it answered with to be kept.
    /// </summary>
    /// <remarks>
    /// Here rather than in a table beside the retention setting, because a
    /// second source with a different clause would otherwise be a second row
    /// somebody has to remember to add, and the failure of forgetting is that
    /// this plugin keeps a source's content longer than that source allows.
    /// #68's fifth condition is that this lives with the adapter, and the
    /// adapter is what has read the terms.
    ///
    /// A ceiling and not a plan. What is actually kept is a configured number
    /// under this, which is the rest of #68, and a source that imposes no limit
    /// still answers with one so that a caller reads a duration rather than an
    /// absence it has to interpret.
    /// </remarks>
    TimeSpan RetentionCeiling { get; }

    /// <summary>
    /// Asks this source one question.
    /// </summary>
    /// <param name="query">What is being asked for, and how much of it.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>What the source gave, or which of the three ways it gave nothing.</returns>
    /// <remarks>
    /// This does not throw to report that the source could not answer. A rate
    /// limit, a timeout and a source that has not been set up are answers a
    /// refresh has to tell apart and act differently on, and an exception
    /// carries none of that to a caller that is refreshing four shelves and
    /// has to keep the previous contents of the one that failed. That is #79,
    /// and <see cref="SourceOutcome"/> is where the four cases are set out.
    ///
    /// What is still thrown is a fault rather than an answer: a query that
    /// could not be asked, refused by <see cref="SourceQuery.Validated"/>, and
    /// cancellation.
    /// </remarks>
    Task<SourceAnswer> FetchAsync(SourceQuery query, CancellationToken cancellationToken);
}
